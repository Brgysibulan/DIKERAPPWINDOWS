using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dikerma.Windows.Models;
using Dikerma.Windows.Services;
using Microsoft.Win32;

namespace Dikerma.Windows;

public partial class MainWindow
{
    private readonly HashSet<string> _selection = new();
    private readonly List<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private Dictionary<string, ElementPlacement> _dragStarts = new();
    private bool _resizing;
    private double _zoom = 1;

    private void InitializeStudio()
    {
        PreviewKeyDown += StudioKeyDown;
        foreach (var font in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            LayoutFontFamilyComboBox.Items.Add(new ComboBoxItem { Content = font.Source, Tag = font.Source });
        Closing += (_, e) =>
        {
            if (JsonSerializer.Serialize(_layout) == JsonSerializer.Serialize(_store.LoadLayout())) return;
            var answer = MessageBox.Show(this, "Save layout changes before closing?", "Layout Studio", MessageBoxButton.YesNoCancel);
            if (answer == MessageBoxResult.Cancel) e.Cancel = true;
            else if (answer == MessageBoxResult.Yes) _store.SaveLayout(_layout);
        };
    }

    private IEnumerable<KeyValuePair<string, ElementPlacement>> SelectedPlacements() =>
        _layout.ForSide(CurrentSide).Where(d => _selection.Contains(d.Key)).Select(d => new KeyValuePair<string, ElementPlacement>(d.Key, _layout.Get(d.Key)));

    private void SelectCanvasElement(string key, bool additive)
    {
        if (!additive) _selection.Clear();
        _selection.Add(key);
        var group = _layout.Get(key).GroupId;
        if (group is not null)
            foreach (var d in _layout.ForSide(CurrentSide).Where(d => _layout.Get(d.Key).GroupId == group)) _selection.Add(d.Key);
    }

    private void Remember()
    {
        _undo.Add(JsonSerializer.Serialize(_layout));
        if (_undo.Count > 80) _undo.RemoveAt(0);
        _redo.Clear();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _undo.Count == 0) return;
        _redo.Push(JsonSerializer.Serialize(_layout));
        Restore(_undo[^1]); _undo.RemoveAt(_undo.Count - 1);
    }
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _redo.Count == 0) return;
        _undo.Add(JsonSerializer.Serialize(_layout)); Restore(_redo.Pop());
    }
    private void Restore(string json)
    {
        var key = _selectedLayoutElement?.Key;
        _layout = JsonSerializer.Deserialize<LayoutProfile>(json)!;
        _selection.Clear(); RefreshLayoutElementList();
        LayoutElementComboBox.SelectedItem = _layout.ForSide(CurrentSide).FirstOrDefault(d => d.Key == key) ?? _layout.ForSide(CurrentSide).FirstOrDefault();
        LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void TransformSelection(double dx, double dy, bool resize)
    {
        if (_dragStarts.Count == 0) return;
        var minX = _dragStarts.Values.Min(p => p.XMm); var minY = _dragStarts.Values.Min(p => p.YMm);
        var maxX = _dragStarts.Values.Max(p => p.XMm + p.WidthMm); var maxY = _dragStarts.Values.Max(p => p.YMm + p.HeightMm);
        if (!resize)
        {
            if (_layout.SnapToGrid)
            {
                var step = Math.Max(0.1, _layout.GridSizeMm);
                dx = Math.Round(dx / step) * step; dy = Math.Round(dy / step) * step;
            }
            dx = Math.Clamp(dx, -minX, LayoutCatalog.CardWidthMm - maxX);
            dy = Math.Clamp(dy, -minY, LayoutCatalog.CardHeightMm - maxY);
        }
        var sx = Math.Clamp((maxX - minX + dx) / (maxX - minX), Math.Max(0.05, _dragStarts.Values.Max(p => 0.3 / p.WidthMm)), (LayoutCatalog.CardWidthMm - minX) / (maxX - minX));
        var sy = Math.Clamp((maxY - minY + dy) / (maxY - minY), Math.Max(0.05, _dragStarts.Values.Max(p => 0.3 / p.HeightMm)), (LayoutCatalog.CardHeightMm - minY) / (maxY - minY));
        foreach (var (key, start) in _dragStarts)
        {
            var p = _layout.Get(key);
            p.XMm = resize ? minX + (start.XMm - minX) * sx : start.XMm + dx;
            p.YMm = resize ? minY + (start.YMm - minY) * sy : start.YMm + dy;
            if (resize) { p.WidthMm = start.WidthMm * sx; p.HeightMm = start.HeightMm * sy; }
            p.Clamp();
        }
    }

    private void AddElement_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || sender is not Button button || !Enum.TryParse<IdLayoutKind>(button.Tag?.ToString(), out var kind)) return;
        string? path = null;
        if (kind == IdLayoutKind.Image)
        {
            var source = ChooseImage(); if (source is null) return;
            try { path = _assets.Import(source, "studio-images"); } catch (Exception ex) { ShowError(ex); return; }
        }
        Remember();
        var d = new LayoutElementDefinition("custom_" + Guid.NewGuid().ToString("N"), CurrentSide, kind + " " + (_layout.CustomElements.Count + 1), kind,
            10, 30, kind == IdLayoutKind.VerticalLine ? 1 : 30, kind == IdLayoutKind.HorizontalLine ? 1 : 12, SampleText: kind == IdLayoutKind.Text ? "Your text" : "");
        _layout.CustomElements.Add(d);
        var p = _layout.Get(d.Key); p.ImagePath = path; p.ZIndex = _layout.Elements.Values.Max(x => x.ZIndex) + 1;
        RefreshLayoutElementList(); LayoutElementComboBox.SelectedItem = d;
        RefreshLayoutPreview();
    }

    private void Group_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _selection.Count < 2) { SetStatus("Shift-click two or more elements to group"); return; }
        Remember(); var id = Guid.NewGuid().ToString("N");
        foreach (var p in SelectedPlacements()) p.Value.GroupId = id;
        SetStatus("Grouped • drag or resize together");
    }
    private void Ungroup_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked) return;
        Remember(); foreach (var p in SelectedPlacements()) p.Value.GroupId = null;
        SetStatus("Elements ungrouped");
    }
    private void DeleteElement_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _selection.Count == 0) return;
        Remember();
        foreach (var key in _selection.ToList())
        {
            if (_layout.CustomElements.RemoveAll(d => d.Key == key) > 0) _layout.Elements.Remove(key);
            else _layout.Get(key).Visible = false;
        }
        _selection.Clear(); RefreshLayoutElementList(); RefreshLayoutPreview();
    }
    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _selection.Count == 0) return;
        Remember(); var selected = _layout.ForSide(CurrentSide).Where(d => _selection.Contains(d.Key)).ToList();
        var group = selected.Count > 1 ? Guid.NewGuid().ToString("N") : null;
        var copies = new List<LayoutElementDefinition>();
        foreach (var d in selected)
        {
            var copy = d with { Key = "custom_" + Guid.NewGuid().ToString("N"), DisplayName = d.DisplayName + " copy" };
            var p = _layout.Get(d.Key).Clone(); p.GroupId = group;
            if (LayoutCatalog.Find(d.Key) is not null) p.BindingKey = d.Key;
            p.XMm += 2; p.YMm += 2; p.Clamp();
            _layout.CustomElements.Add(copy); _layout.Elements[copy.Key] = p; copies.Add(copy);
        }
        RefreshLayoutElementList(); LayoutElementComboBox.SelectedItem = copies[0];
        _selection.Clear(); foreach (var d in copies) _selection.Add(d.Key);
        RefreshLayoutPreview();
    }
    private void LayerOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked) return;
        Remember(); var top = (sender as Button)?.Tag?.ToString() == "front";
        var z = top ? _layout.Elements.Values.Max(p => p.ZIndex) + 1 : _layout.Elements.Values.Min(p => p.ZIndex) - 1;
        foreach (var p in SelectedPlacements()) p.Value.ZIndex = z;
        RefreshLayoutPreview();
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.25, 8);
        LayoutCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomLabel.Text = $"{_zoom:P0}";
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.25);
    private void ZoomMax_Click(object sender, RoutedEventArgs e) => SetZoom(8);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => SetZoom(Math.Min((StudioScroll.ActualWidth - 32) / LayoutCanvas.Width, (StudioScroll.ActualHeight - 32) / LayoutCanvas.Height));
    private void StudioWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        SetZoom(_zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15)); e.Handled = true;
    }
    private void ImportFont_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Font files|*.ttf;*.otf" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var path = _assets.Import(dialog.FileName, "fonts");
            var families = Fonts.GetFontFamilies(new Uri(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar), "./");
            foreach (var family in families)
            {
                var name = family.FamilyNames.Values.First();
                var key = new Uri(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar).AbsoluteUri + "./#" + name;
                var item = new ComboBoxItem { Content = name + " (imported)", Tag = key };
                LayoutFontFamilyComboBox.Items.Add(item); LayoutFontFamilyComboBox.SelectedItem = item;
            }
            SetStatus("Font imported • Apply selected settings to use it");
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void Crop_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _selectedLayoutElement?.Kind != IdLayoutKind.Image) { SetStatus("Select an image layer to crop"); return; }
        var p = _layout.Get(_selectedLayoutElement.Key);
        var path = p.ImagePath ?? ResolvePreviewImage(p.BindingKey ?? _selectedLayoutElement.Key);
        if (string.IsNullOrEmpty(path)) { SetStatus("Upload an image first"); return; }
        try
        {
            var dialog = new ImageToolsWindow(path, p, _images) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            Remember(); p.CropLeft = dialog.Result.CropLeft; p.CropTop = dialog.Result.CropTop;
            p.CropRight = dialog.Result.CropRight; p.CropBottom = dialog.Result.CropBottom;
            // Keep record-bound portraits bound to each employee unless explicitly replacing this layer.
            if (dialog.ProcessedPath is not null) p.ImagePath = dialog.ProcessedPath;
            RefreshLayoutPreview();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void StudioKeyDown(object sender, KeyEventArgs e)
    {
        if (MainTabs.SelectedIndex != 2) return;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key == Key.S) { SaveLayout_Click(this, e); e.Handled = true; return; }
        if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase || Keyboard.FocusedElement is ComboBox) return;
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z: Undo_Click(this, e); break;
                case Key.Y: Redo_Click(this, e); break;
                case Key.D: Duplicate_Click(this, e); break;
                case Key.G: if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Ungroup_Click(this, e); else Group_Click(this, e); break;
                case Key.A: foreach (var d in _layout.ForSide(CurrentSide).Where(d => _layout.Get(d.Key).Visible)) _selection.Add(d.Key); RefreshLayoutPreview(); break;
                case Key.OemPlus: case Key.Add: ZoomIn_Click(this, e); break;
                case Key.OemMinus: case Key.Subtract: ZoomOut_Click(this, e); break;
                case Key.D0: ZoomFit_Click(this, e); break;
                default: return;
            }
        }
        else
        {
            var step = NudgeStep() * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
            switch (e.Key)
            {
                case Key.Delete: DeleteElement_Click(this, e); break;
                case Key.Left: Nudge(-step, 0); break;
                case Key.Right: Nudge(step, 0); break;
                case Key.Up: Nudge(0, -step); break;
                case Key.Down: Nudge(0, step); break;
                default: return;
            }
        }
        e.Handled = true;
    }
    private void Shortcuts_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "Shift-click: select multiple elements\nDrag: move selection\nDrag bottom-right corner: resize selection\nCtrl+G / Ctrl+Shift+G: group / ungroup\nCtrl+D: duplicate\nDelete: remove custom / hide standard field\nCtrl+Z / Ctrl+Y: undo / redo\nCtrl+A: select all visible elements\nArrows: move • Shift: 10× step\nCtrl+mouse wheel or +/−: zoom\nCtrl+0: fit\nCtrl+S: save placement\n\nText fields keep their normal editing shortcuts.", "Studio shortcuts");
}
