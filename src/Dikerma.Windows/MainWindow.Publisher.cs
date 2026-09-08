using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dikerma.Windows.Models;

namespace Dikerma.Windows;

public partial class MainWindow
{
    private bool _publisherLoaded;
    private bool _syncingPublisherLayer;
    private EmployeeRecord? _publisherPreviewEmployee;

    private void PublisherStudio_Loaded(object sender, RoutedEventArgs e)
    {
        if (_publisherLoaded) return;
        _publisherLoaded = true;

        MigratePublisherLayout();
        PreviewEmployeeComboBox.ItemsSource = _employees;
        _publisherPreviewEmployee = _editingEmployee ?? _employees.FirstOrDefault();
        PreviewEmployeeComboBox.SelectedItem = _publisherPreviewEmployee;

        EmployeesGrid.SelectionChanged += (_, _) =>
        {
            if (EmployeesGrid.SelectedItem is EmployeeRecord employee)
            {
                _publisherPreviewEmployee = employee;
                PreviewEmployeeComboBox.SelectedItem = employee;
            }
        };
        LayoutSideComboBox.SelectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RenderPublisherPreview));
        LayoutCanvas.PreviewMouseLeftButtonUp += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                SyncPublisherLayerFromCanvas();
                LoadPublisherProperties();
                RenderPublisherPreview();
            }));

        LoadPublisherProperties();
        RenderPublisherPreview();
    }

    private void MigratePublisherLayout()
    {
        var changed = false;
        foreach (var definition in _layout.ForSide(IdLayoutSide.Front).Concat(_layout.ForSide(IdLayoutSide.Back)))
        {
            var p = _layout.Get(definition.Key);
            var bindingKey = p.BindingKey ?? definition.Key;
            if (!LayoutCatalog.IsRecordBoundKey(bindingKey)) continue;
            if (p.TextOverride is not null) { p.TextOverride = null; changed = true; }
            if (p.ImagePath is not null) { p.ImagePath = null; changed = true; }
        }

        if (_layout.SchemaVersion < 3)
        {
            var photo = _layout.Get("front_photo");
            if (_settings.PhotoOutlineEnabled)
            {
                photo.BorderEnabled = true;
                photo.BorderColor = "#00522D";
                photo.BorderThicknessPt = Math.Max(0.3, _settings.OutlineThicknessPt);
            }
            var qr = _layout.Get("front_qr");
            if (_settings.QrOutlineEnabled)
            {
                qr.BorderEnabled = true;
                qr.BorderColor = "#000000";
                qr.BorderThicknessPt = Math.Max(0.3, _settings.OutlineThicknessPt);
            }

            // From v0.3 onward image frames belong to the master layer, not to global PDF settings.
            _settings.PhotoOutlineEnabled = false;
            _settings.QrOutlineEnabled = false;
            _store.SaveSettings(_settings);
            PhotoOutlineCheckBox.IsChecked = false;
            QrOutlineCheckBox.IsChecked = false;
            _layout.SchemaVersion = 3;
            changed = true;
        }

        if (changed) _store.SaveLayout(_layout);
    }

    private void PreviewEmployeeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PreviewEmployeeComboBox.SelectedItem is EmployeeRecord employee)
            _publisherPreviewEmployee = employee;
        RenderPublisherPreview();
    }

    private void RenderPublisherPreview()
    {
        if (!_publisherLoaded || _publisherPreviewEmployee is null || !_employees.Contains(_publisherPreviewEmployee))
        {
            RefreshLayoutPreview();
            return;
        }

        // Existing preview resolver reads the first record. Swap only the field reference while rendering,
        // without reordering the DataGrid or changing persisted employee order.
        var original = _employees;
        try
        {
            _employees = new ObservableCollection<EmployeeRecord>(new[] { _publisherPreviewEmployee }
                .Concat(original.Where(x => x.Id != _publisherPreviewEmployee.Id)));
            RefreshLayoutPreview();
        }
        finally
        {
            _employees = original;
        }
    }

    private void LayersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPublisherLayer || LayersListBox.SelectedItem is not LayoutElementDefinition definition) return;
        _syncingPublisherLayer = true;
        try
        {
            _selectedLayoutElement = definition;
            _selection.Clear();
            _selection.Add(definition.Key);
            if (!Equals(LayoutElementComboBox.SelectedItem, definition)) LayoutElementComboBox.SelectedItem = definition;
            LoadLayoutProperties();
            LoadPublisherProperties();
            RenderPublisherPreview();
        }
        finally { _syncingPublisherLayer = false; }
    }

    private void SyncPublisherLayerFromCanvas()
    {
        if (_selectedLayoutElement is null || LayersListBox is null) return;
        _syncingPublisherLayer = true;
        try { LayersListBox.SelectedItem = LayersListBox.Items.OfType<LayoutElementDefinition>().FirstOrDefault(x => x.Key == _selectedLayoutElement.Key); }
        finally { _syncingPublisherLayer = false; }
    }

    private void LoadPublisherProperties()
    {
        if (!_publisherLoaded || _selectedLayoutElement is null || LayoutFillColorTextBox is null) return;
        var p = _layout.Get(_selectedLayoutElement.Key);
        var bindingKey = p.BindingKey ?? _selectedLayoutElement.Key;
        var recordBound = LayoutCatalog.IsRecordBoundKey(bindingKey);
        var custom = _layout.CustomElements.Any(x => x.Key == _selectedLayoutElement.Key);

        PublisherBindingText.Text = recordBound
            ? "RECORD DATA • value/image always comes from the employee being previewed or printed."
            : "STATIC DESIGN • safe to edit as part of the master template.";
        LayoutTextOverride.IsEnabled = !recordBound && _selectedLayoutElement.Kind == IdLayoutKind.Text;
        if (recordBound) LayoutTextOverride.Text = string.Empty;

        LayerNameTextBox.Text = _selectedLayoutElement.DisplayName;
        LayerNameTextBox.IsEnabled = custom;
        RenameLayerButton.IsEnabled = custom;
        LayoutElementLockedCheckBox.IsChecked = p.Locked;
        LayoutFillColorTextBox.Text = p.FillColor;
        LayoutBorderCheckBox.IsChecked = p.BorderEnabled;
        LayoutBorderColorTextBox.Text = p.BorderColor;
        LayoutBorderThicknessTextBox.Text = F(p.BorderThicknessPt);
        LayoutCornerRadiusTextBox.Text = F(p.CornerRadiusMm);
        LayoutOpacityTextBox.Text = F(p.Opacity);
    }

    private void ApplyPublisherAppearance_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        var p = _layout.Get(_selectedLayoutElement.Key);
        Remember();
        p.Locked = LayoutElementLockedCheckBox.IsChecked == true;
        p.FillColor = NormalizeHex(LayoutFillColorTextBox.Text, p.FillColor);
        p.BorderEnabled = LayoutBorderCheckBox.IsChecked == true;
        p.BorderColor = NormalizeHex(LayoutBorderColorTextBox.Text, p.BorderColor);
        p.BorderThicknessPt = Read(LayoutBorderThicknessTextBox, p.BorderThicknessPt);
        p.CornerRadiusMm = Read(LayoutCornerRadiusTextBox, p.CornerRadiusMm);
        p.Opacity = Read(LayoutOpacityTextBox, p.Opacity);
        if (LayoutCatalog.IsRecordBound(_selectedLayoutElement, p))
        {
            p.TextOverride = null;
            p.ImagePath = null;
        }
        p.Clamp();
        LoadPublisherProperties();
        RenderPublisherPreview();
        SetStatus("Appearance updated • save master design to persist");
    }

    private void PickFillColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutFillColorTextBox);
    private void PickBorderColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutBorderColorTextBox);

    private void ToggleLayerVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        var p = _layout.Get(_selectedLayoutElement.Key);
        p.Visible = !p.Visible;
        LayoutVisibleCheckBox.IsChecked = p.Visible;
        RenderPublisherPreview();
        SetStatus(p.Visible ? "Layer shown" : "Layer hidden");
    }

    private void ToggleLayerLock_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        var p = _layout.Get(_selectedLayoutElement.Key);
        p.Locked = !p.Locked;
        LayoutElementLockedCheckBox.IsChecked = p.Locked;
        RenderPublisherPreview();
        SetStatus(p.Locked ? "Layer locked" : "Layer unlocked");
    }

    private void RenameLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        var name = LayerNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        var index = _layout.CustomElements.FindIndex(x => x.Key == _selectedLayoutElement.Key);
        if (index < 0) { SetStatus("Built-in record/design layers keep their protected names"); return; }
        Remember();
        var renamed = _layout.CustomElements[index] with { DisplayName = name };
        _layout.CustomElements[index] = renamed;
        _selectedLayoutElement = renamed;
        RefreshLayoutElementList();
        LayoutElementComboBox.SelectedItem = renamed;
        LayersListBox.SelectedItem = renamed;
        LoadPublisherProperties();
        SetStatus("Custom layer renamed");
    }

    private void LayerStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked || _layout.Get(_selectedLayoutElement.Key).Locked) return;
        var direction = (sender as Button)?.Tag?.ToString();
        var ordered = _layout.ForSide(CurrentSide).OrderBy(x => _layout.Get(x.Key).ZIndex).ToList();
        var index = ordered.FindIndex(x => x.Key == _selectedLayoutElement.Key);
        var target = direction == "forward" ? index + 1 : index - 1;
        if (index < 0 || target < 0 || target >= ordered.Count) return;
        Remember();
        var a = _layout.Get(ordered[index].Key);
        var b = _layout.Get(ordered[target].Key);
        (a.ZIndex, b.ZIndex) = (b.ZIndex, a.ZIndex);
        RefreshLayoutElementList();
        LayoutElementComboBox.SelectedItem = _selectedLayoutElement;
        RenderPublisherPreview();
    }

    private void AlignSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked || _selection.Count == 0) return;
        var mode = (sender as Button)?.Tag?.ToString();
        var selected = SelectedPlacements().Where(x => !x.Value.Locked).Select(x => x.Value).ToList();
        if (selected.Count == 0) return;
        Remember();

        var minX = selected.Count == 1 ? 0 : selected.Min(p => p.XMm);
        var minY = selected.Count == 1 ? 0 : selected.Min(p => p.YMm);
        var maxX = selected.Count == 1 ? LayoutCatalog.CardWidthMm : selected.Max(p => p.XMm + p.WidthMm);
        var maxY = selected.Count == 1 ? LayoutCatalog.CardHeightMm : selected.Max(p => p.YMm + p.HeightMm);
        foreach (var p in selected)
        {
            switch (mode)
            {
                case "left": p.XMm = minX; break;
                case "center": p.XMm = (minX + maxX - p.WidthMm) / 2; break;
                case "right": p.XMm = maxX - p.WidthMm; break;
                case "top": p.YMm = minY; break;
                case "middle": p.YMm = (minY + maxY - p.HeightMm) / 2; break;
                case "bottom": p.YMm = maxY - p.HeightMm; break;
            }
            p.Clamp();
        }
        LoadLayoutProperties();
        RenderPublisherPreview();
    }
}
