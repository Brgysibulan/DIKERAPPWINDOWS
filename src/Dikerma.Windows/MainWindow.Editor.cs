using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Dikerma.Windows.Models;

namespace Dikerma.Windows;

public partial class MainWindow
{
    private readonly Stack<string> _layoutUndo = new();
    private readonly Stack<string> _layoutRedo = new();

    private bool CanEditLayout()
    {
        if (!_layout.Locked) return true;
        SetStatus("Layout is locked. Uncheck Lock layout first.");
        return false;
    }

    private void RememberLayout()
    {
        var snapshot = JsonSerializer.Serialize(_layout);
        if (_layoutUndo.Count == 0 || _layoutUndo.Peek() != snapshot) _layoutUndo.Push(snapshot);
        _layoutRedo.Clear();
    }

    private void UndoLayout_Click(object sender, RoutedEventArgs e) => RestoreHistory(_layoutUndo, _layoutRedo);
    private void RedoLayout_Click(object sender, RoutedEventArgs e) => RestoreHistory(_layoutRedo, _layoutUndo);

    private void RestoreHistory(Stack<string> source, Stack<string> destination)
    {
        if (!CanEditLayout() || source.Count == 0) return;
        var current = JsonSerializer.Serialize(_layout);
        while (source.Count > 0 && source.Peek() == current) source.Pop();
        if (source.Count == 0) return;
        destination.Push(current);
        _layout = JsonSerializer.Deserialize<LayoutProfile>(source.Pop())!;
        RefreshLayoutElementList();
        RefreshLayoutPreview();
        SetStatus("Layout restored • Save layout to keep");
    }

    private void ApplyTextContent()
    {
        if (_selectedLayoutElement?.Kind != IdLayoutKind.Text) return;
        // Do not freeze a live employee binding merely by applying font/position changes.
        if (ElementTextBox.Text != ResolvePreviewText(_selectedLayoutElement))
            _layout.Get(_selectedLayoutElement.Key).TextOverride = ElementTextBox.Text;
    }

    private void ApplyElementText_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || !CanEditLayout()) return;
        RememberLayout();
        ApplyTextContent();
        RefreshLayoutPreview();
        SetStatus("Template text updated • Save layout to keep");
    }

    private void UseOriginalContent_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || !CanEditLayout()) return;
        RememberLayout();
        var p = _layout.Get(_selectedLayoutElement.Key);
        p.TextOverride = null;
        p.ImageOverride = null;
        LoadLayoutProperties();
        RefreshLayoutPreview();
        SetStatus("Original content / record binding restored");
    }

    private void ReplaceElementImage_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement?.Kind != IdLayoutKind.Image || !CanEditLayout()) return;
        var source = ChooseImage();
        if (source is null) return;
        try
        {
            var imported = _assets.Import(source, "design-assets");
            RememberLayout();
            _layout.Get(_selectedLayoutElement.Key).ImageOverride = imported;
            RefreshLayoutPreview();
            SetStatus("Template image replaced • Applies to all IDs • Save layout to keep");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void EditSelectedContent()
    {
        if (_selectedLayoutElement is null || !CanEditLayout()) return;
        if (_selectedLayoutElement.Kind == IdLayoutKind.Image)
        {
            ReplaceElementImage_Click(this, new RoutedEventArgs());
            return;
        }
        if (_selectedLayoutElement.Kind != IdLayoutKind.Text) return;
        var editor = new TextBox { Text = ResolvePreviewText(_selectedLayoutElement), AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinHeight = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Margin = new Thickness(16) };
        var dialog = new Window { Owner = this, Title = "Edit text — " + _selectedLayoutElement.DisplayName,
            Width = 450, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        panel.Children.Add(new TextBlock { Text = "Template text applies to all IDs. Employee records are not changed.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(editor);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var apply = new Button { Content = "Apply text", MinWidth = 100 };
        apply.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(apply);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 100 });
        panel.Children.Add(buttons);
        dialog.Loaded += (_, _) => { editor.Focus(); editor.SelectAll(); };
        if (dialog.ShowDialog() != true) return;
        RememberLayout();
        ElementTextBox.Text = editor.Text;
        ApplyTextContent();
        LoadLayoutProperties();
        RefreshLayoutPreview();
        SetStatus("Text updated • Save layout to keep");
    }

    private ContextMenu CreateElementMenu(string key)
    {
        var menu = new ContextMenu();
        void Add(string label, Action action)
        {
            var item = new MenuItem { Header = label, IsEnabled = !_layout.Locked };
            item.Click += (_, _) =>
            {
                LayoutElementComboBox.SelectedItem = LayoutElementComboBox.Items.Cast<LayoutElementDefinition>().FirstOrDefault(x => x.Key == key);
                action();
            };
            menu.Items.Add(item);
        }
        Add("Edit content / replace image", EditSelectedContent);
        Add("Duplicate", () => DuplicateElement_Click(this, new RoutedEventArgs()));
        Add("Bring forward", () => MoveElementLayer(true));
        Add("Send backward", () => MoveElementLayer(false));
        Add("Delete from template", () => DeleteElement_Click(this, new RoutedEventArgs()));
        return menu;
    }

    private void MoveElementLayer(bool forward)
    {
        if (_selectedLayoutElement is null || !CanEditLayout()) return;
        RememberLayout();
        var list = CurrentDefinitions().ToList();
        var index = list.FindIndex(x => x.Key == _selectedLayoutElement.Key);
        var target = index + (forward ? 1 : -1);
        if (target < 0 || target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]);
        for (var i = 0; i < list.Count; i++) _layout.Get(list[i].Key).ZIndex = i;
        RefreshLayoutPreview();
    }

    private void RestoreElement_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditLayout()) return;
        var removed = LayoutCatalog.ForSide(CurrentSide)
            .Concat(_layout.CustomElements.Where(x => x.Side == CurrentSide).Select(x => x.ToDefinition()))
            .Where(x => _layout.Get(x.Key).Deleted).ToList();
        if (removed.Count == 0) { SetStatus("No deleted elements on this side"); return; }
        var list = new ListBox { ItemsSource = removed, DisplayMemberPath = "DisplayName", Height = 240, SelectedIndex = 0 };
        var panel = new StackPanel { Margin = new Thickness(16) };
        var dialog = new Window { Owner = this, Title = "Restore deleted element", Width = 420,
            SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        panel.Children.Add(list);
        var restore = new Button { Content = "Restore selected" };
        restore.Click += (_, _) => dialog.DialogResult = list.SelectedItem is not null;
        panel.Children.Add(restore);
        if (dialog.ShowDialog() != true || list.SelectedItem is not LayoutElementDefinition definition) return;
        RememberLayout();
        _layout.Get(definition.Key).Deleted = false;
        _layout.Get(definition.Key).Visible = true;
        _selectedLayoutElement = definition;
        RefreshLayoutElementList();
        RefreshLayoutPreview();
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (MainTabs.SelectedIndex != 2 || Keyboard.FocusedElement is TextBoxBase or ComboBox or PasswordBox) return;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z: UndoLayout_Click(this, e); break;
                case Key.Y: RedoLayout_Click(this, e); break;
                case Key.D: DuplicateElement_Click(this, e); break;
                case Key.S: SaveLayout_Click(this, e); break;
                default: return;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.Delete: DeleteElement_Click(this, e); break;
                case Key.F2: EditSelectedContent(); break;
                case Key.Left: Nudge(-NudgeStep(), 0); break;
                case Key.Right: Nudge(NudgeStep(), 0); break;
                case Key.Up: Nudge(0, -NudgeStep()); break;
                case Key.Down: Nudge(0, NudgeStep()); break;
                default: return;
            }
        }
        else return;
        e.Handled = true;
    }
}
