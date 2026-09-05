using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Dikerma.Windows.Models;
using Dikerma.Windows.Services;
using Microsoft.Win32;
using WpfImage = System.Windows.Controls.Image;

namespace Dikerma.Windows;

public partial class MainWindow : Window
{
    private const double CanvasScale = 5.0;

    private readonly JsonStore _store = new();
    private readonly AssetService _assets = new();
    private readonly PdfExportService _pdf = new();
    private readonly OfflineImageProcessor _images;

    private ObservableCollection<EmployeeRecord> _employees = new();
    private AppSettingsModel _settings = new();
    private LayoutProfile _layout = LayoutCatalog.CreateDefaultProfile();
    private EmployeeRecord? _editingEmployee;
    private LayoutElementDefinition? _selectedLayoutElement;
    private FrameworkElement? _dragVisual;
    private Point _dragStartPoint;
    private double _dragStartXmm;
    private double _dragStartYmm;

    public MainWindow()
    {
        InitializeComponent();
        _images = new OfflineImageProcessor(_assets);

        _settings = _store.LoadSettings();
        _layout = _store.LoadLayout();
        _employees = new ObservableCollection<EmployeeRecord>(_store.LoadEmployees().OrderBy(x => x.FullName));

        EmployeesGrid.ItemsSource = _employees;
        LayoutSideComboBox.ItemsSource = Enum.GetValues<IdLayoutSide>();
        LayoutAlignmentComboBox.ItemsSource = Enum.GetValues<IdTextAlignment>();
        LayoutUnderlineModeComboBox.ItemsSource = Enum.GetValues<IdUnderlineWidthMode>();
        LayoutSideComboBox.SelectedItem = IdLayoutSide.Front;
        LayoutAlignmentComboBox.SelectedItem = IdTextAlignment.Left;
        LayoutUnderlineModeComboBox.SelectedItem = IdUnderlineWidthMode.Text;
        GridSizeComboBox.SelectedIndex = 1;
        SnapToGridCheckBox.IsChecked = _layout.SnapToGrid;
        LayoutLockedCheckBox.IsChecked = _layout.Locked;

        InitializeStudio();
        LoadSettingsIntoUi();
        RefreshGenerationPickers();
        RefreshLayoutElementList();
        NewRecordForm();
        RefreshLayoutPreview();
    }

    private void NavigateRecords_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;
    private void NavigateGenerate_Click(object sender, RoutedEventArgs e) { RefreshGenerationPickers(); MainTabs.SelectedIndex = 1; }
    private void NavigateLayout_Click(object sender, RoutedEventArgs e) { MainTabs.SelectedIndex = 2; RefreshLayoutPreview(); }
    private void NavigateSettings_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 3;

    private void EmployeesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmployeesGrid.SelectedItem is not EmployeeRecord employee) return;
        _editingEmployee = employee;
        FullNameTextBox.Text = employee.FullName;
        PositionTextBox.Text = employee.Position;
        ControlNumberTextBox.Text = employee.ControlNumber;
        BirthdateTextBox.Text = employee.Birthdate;
        AddressTextBox.Text = employee.Address;
        SexTextBox.Text = employee.Sex;
        CivilStatusTextBox.Text = employee.CivilStatus;
        SelectStatus(employee.Status);
        PhotoPathTextBox.Text = employee.PhotoPath ?? string.Empty;
        SignaturePathTextBox.Text = employee.SignaturePath ?? string.Empty;
        QrPathTextBox.Text = employee.QrImagePath ?? string.Empty;
    }

    private void NewRecord_Click(object sender, RoutedEventArgs e) => NewRecordForm();

    private void NewRecordForm()
    {
        _editingEmployee = null;
        EmployeesGrid.SelectedItem = null;
        FullNameTextBox.Clear();
        PositionTextBox.Clear();
        ControlNumberTextBox.Clear();
        BirthdateTextBox.Clear();
        AddressTextBox.Clear();
        SexTextBox.Clear();
        CivilStatusTextBox.Clear();
        PhotoPathTextBox.Clear();
        SignaturePathTextBox.Clear();
        QrPathTextBox.Clear();
        PhotoKeepOriginalCheckBox.IsChecked = false;
        SignatureKeepOriginalCheckBox.IsChecked = false;
        SelectStatus("Active");
        SetStatus("New record");
    }

    private void SaveRecord_Click(object sender, RoutedEventArgs e)
    {
        var name = FullNameTextBox.Text.Trim();
        var control = ControlNumberTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("Full name is required.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(control)) { MessageBox.Show("Employee / Control No. is required.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        if (_employees.Any(x => x.Id != _editingEmployee?.Id && x.ControlNumber.Equals(control, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("That Employee / Control No. is already used by another record.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var employee = _editingEmployee ?? new EmployeeRecord();
        employee.FullName = name;
        employee.Position = PositionTextBox.Text.Trim();
        employee.ControlNumber = control;
        employee.Birthdate = BirthdateTextBox.Text.Trim();
        employee.Address = AddressTextBox.Text.Trim();
        employee.Sex = SexTextBox.Text.Trim();
        employee.CivilStatus = CivilStatusTextBox.Text.Trim();
        employee.Status = GetSelectedStatus();
        employee.PhotoPath = NullIfBlank(PhotoPathTextBox.Text);
        employee.SignaturePath = NullIfBlank(SignaturePathTextBox.Text);
        employee.QrImagePath = NullIfBlank(QrPathTextBox.Text);

        if (_editingEmployee is null) _employees.Add(employee);
        _editingEmployee = employee;
        _store.SaveEmployees(_employees);
        EmployeesGrid.Items.Refresh();
        EmployeesGrid.SelectedItem = employee;
        RefreshGenerationPickers(employee);
        RefreshLayoutPreview();
        SetStatus($"Saved: {employee.FullName}");
    }

    private void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_editingEmployee is null) return;
        if (MessageBox.Show($"Delete {_editingEmployee.FullName}?", "DIKERMA", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _employees.Remove(_editingEmployee);
        _store.SaveEmployees(_employees);
        NewRecordForm();
        RefreshGenerationPickers();
        RefreshLayoutPreview();
        SetStatus("Record deleted");
    }

    private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
    {
        var source = ChooseImage();
        if (source is null) return;
        try
        {
            PhotoPathTextBox.Text = PhotoKeepOriginalCheckBox.IsChecked == true
                ? _assets.Import(source, "photos-original")
                : _images.CleanPhotoToWhite(source);
            SetStatus("Photo imported");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ChooseSignature_Click(object sender, RoutedEventArgs e)
    {
        var source = ChooseImage();
        if (source is null) return;
        try
        {
            SignaturePathTextBox.Text = SignatureKeepOriginalCheckBox.IsChecked == true
                ? _assets.Import(source, "signatures-original")
                : _images.CleanSignatureToTransparent(source);
            SetStatus("Signature imported");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ChooseQr_Click(object sender, RoutedEventArgs e)
    {
        var source = ChooseImage();
        if (source is null) return;
        try { QrPathTextBox.Text = _assets.Import(source, "qr"); SetStatus("QR image imported"); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RefreshGenerationPickers(EmployeeRecord? preferred = null)
    {
        var p1 = preferred ?? Person1ComboBox.SelectedItem as EmployeeRecord;
        var p2 = Person2ComboBox.SelectedItem as EmployeeRecord;
        Person1ComboBox.ItemsSource = _employees;
        Person2ComboBox.ItemsSource = _employees;
        if (p1 is not null && _employees.Contains(p1)) Person1ComboBox.SelectedItem = p1;
        else if (_employees.Count > 0) Person1ComboBox.SelectedIndex = 0;
        if (p2 is not null && _employees.Contains(p2)) Person2ComboBox.SelectedItem = p2;
        else Person2ComboBox.SelectedIndex = -1;
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (Person1ComboBox.SelectedItem is not EmployeeRecord person1)
        {
            MessageBox.Show("Select Person 1 first.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var person2 = Person2ComboBox.SelectedItem as EmployeeRecord;
        if (person2?.Id == person1.Id)
        {
            MessageBox.Show("Person 1 and Person 2 must be different records.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF document (*.pdf)|*.pdf",
            FileName = $"Barangay-Sibulan-IDs-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
            InitialDirectory = AppPaths.Exports
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _pdf.Export(dialog.FileName, person1, person2, _settings, _layout);
            SetStatus("PDF exported");
            MessageBox.Show($"PDF saved:\n{dialog.FileName}\n\nPrint at Actual Size / 100%.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private IdLayoutSide CurrentSide => LayoutSideComboBox.SelectedItem is IdLayoutSide side ? side : IdLayoutSide.Front;

    private void LayoutSideComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded && LayoutElementComboBox is null) return;
        _selection.Clear();
        RefreshLayoutElementList();
        RefreshLayoutPreview();
    }

    private void RefreshLayoutElementList()
    {
        if (LayoutElementComboBox is null) return;
        var items = _layout.ForSide(CurrentSide).ToList();
        LayoutElementComboBox.ItemsSource = items;
        if (items.Count > 0) LayoutElementComboBox.SelectedIndex = 0;
    }

    private void LayoutElementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedLayoutElement = LayoutElementComboBox.SelectedItem as LayoutElementDefinition;
        if (_selectedLayoutElement is not null && !_selection.Contains(_selectedLayoutElement.Key)) SelectCanvasElement(_selectedLayoutElement.Key, false);
        LoadLayoutProperties();
        RefreshLayoutPreview();
    }

    private void RefreshLayoutPreview()
    {
        if (LayoutCanvas is null) return;
        LayoutCanvas.Children.Clear();
        DrawPreviewBackground();
        DrawGuides();

        var layerOrder = 10;
        foreach (var definition in _layout.ForSide(CurrentSide))
        {
            var placement = _layout.Get(definition.Key);
            if (!placement.Visible && definition.Key != _selectedLayoutElement?.Key) continue;
            var wrapper = CreateElementVisual(definition, placement);
            LayoutCanvas.Children.Add(wrapper);
            Canvas.SetLeft(wrapper, placement.XMm * CanvasScale);
            Canvas.SetTop(wrapper, placement.YMm * CanvasScale);
            Panel.SetZIndex(wrapper, layerOrder++);
        }
    }

    private void DrawPreviewBackground()
    {
        var path = CurrentSide == IdLayoutSide.Front ? _settings.FrontBackgroundPath : _settings.BackBackgroundPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var image = new WpfImage
                {
                    Source = OfflineImageProcessor.LoadPreview(path), Width = LayoutCanvas.Width, Height = LayoutCanvas.Height,
                    Stretch = Stretch.Fill, IsHitTestVisible = false
                };
                LayoutCanvas.Children.Add(image);
                Panel.SetZIndex(image, 0);
                return;
            }
            catch { }
        }

        var fallback = new Border { Width = LayoutCanvas.Width, Height = LayoutCanvas.Height, Background = Brushes.White, IsHitTestVisible = false };
        LayoutCanvas.Children.Add(fallback);
        var bar = new Rectangle
        {
            Width = LayoutCanvas.Width,
            Height = (CurrentSide == IdLayoutSide.Front ? 25 : 13) * CanvasScale,
            Fill = BrushFromHex("#00522D"),
            IsHitTestVisible = false
        };
        LayoutCanvas.Children.Add(bar);
        Canvas.SetTop(bar, CurrentSide == IdLayoutSide.Front ? 0 : LayoutCanvas.Height - bar.Height);
    }

    private void DrawGuides()
    {
        var gridBrush = new SolidColorBrush(Color.FromArgb(45, 60, 80, 65));
        for (var mm = 5.0; mm < LayoutCatalog.CardWidthMm; mm += 5)
        {
            LayoutCanvas.Children.Add(new Line { X1 = mm * CanvasScale, X2 = mm * CanvasScale, Y1 = 0, Y2 = LayoutCanvas.Height, Stroke = gridBrush, StrokeThickness = 0.5, IsHitTestVisible = false });
        }
        for (var mm = 5.0; mm < LayoutCatalog.CardHeightMm; mm += 5)
        {
            LayoutCanvas.Children.Add(new Line { Y1 = mm * CanvasScale, Y2 = mm * CanvasScale, X1 = 0, X2 = LayoutCanvas.Width, Stroke = gridBrush, StrokeThickness = 0.5, IsHitTestVisible = false });
        }

        var centerBrush = new SolidColorBrush(Color.FromArgb(120, 0, 122, 68));
        LayoutCanvas.Children.Add(new Line { X1 = LayoutCanvas.Width / 2, X2 = LayoutCanvas.Width / 2, Y1 = 0, Y2 = LayoutCanvas.Height, Stroke = centerBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 4 }, IsHitTestVisible = false });
        LayoutCanvas.Children.Add(new Line { Y1 = LayoutCanvas.Height / 2, Y2 = LayoutCanvas.Height / 2, X1 = 0, X2 = LayoutCanvas.Width, Stroke = centerBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 4 }, IsHitTestVisible = false });

        var safe = new Rectangle
        {
            Width = (LayoutCatalog.CardWidthMm - LayoutCatalog.SafeMarginMm * 2) * CanvasScale,
            Height = (LayoutCatalog.CardHeightMm - LayoutCatalog.SafeMarginMm * 2) * CanvasScale,
            Stroke = centerBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 6, 4 }, IsHitTestVisible = false
        };
        LayoutCanvas.Children.Add(safe);
        Canvas.SetLeft(safe, LayoutCatalog.SafeMarginMm * CanvasScale);
        Canvas.SetTop(safe, LayoutCatalog.SafeMarginMm * CanvasScale);
        Panel.SetZIndex(safe, 2);
    }

    private FrameworkElement CreateElementVisual(LayoutElementDefinition definition, ElementPlacement p)
    {
        FrameworkElement child = new Viewbox { Stretch = Stretch.Fill,
            Child = ElementRenderer.Create(definition, p, ResolvePreviewText(definition), p.ImagePath ?? ResolvePreviewImage(p.BindingKey ?? definition.Key)) };

        var selected = _selection.Contains(definition.Key) || definition.Key == _selectedLayoutElement?.Key;
        var wrapper = new Grid
        {
            Width = p.WidthMm * CanvasScale,
            Height = p.HeightMm * CanvasScale,
            Background = Brushes.Transparent,
            Tag = definition.Key,
            Cursor = _layout.Locked ? Cursors.Arrow : Cursors.SizeAll,
            ToolTip = definition.DisplayName
        };
        wrapper.Children.Add(child);
        wrapper.Opacity = p.Visible ? 1 : 0.3;
        if (selected)
        {
            wrapper.Children.Add(new Border { BorderBrush = Brushes.Gold, BorderThickness = new Thickness(1.5), IsHitTestVisible = false });
            wrapper.Children.Add(new Rectangle { Width = 9, Height = 9, Fill = Brushes.Gold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false });
        }
        wrapper.MouseLeftButtonDown += LayoutVisual_MouseLeftButtonDown;
        wrapper.MouseMove += LayoutVisual_MouseMove;
        wrapper.MouseLeftButtonUp += LayoutVisual_MouseLeftButtonUp;
        return wrapper;
    }

    private static Border Placeholder(string text) => new()
    {
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1),
        Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
        Child = new TextBlock { Text = text, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 10, TextWrapping = TextWrapping.Wrap }
    };

    private void LayoutVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement visual || visual.Tag is not string key) return;
        var definition = _layout.Find(key);
        if (definition is null) return;
        var local = e.GetPosition(visual);
        var resizing = local.X >= visual.ActualWidth - 12 && local.Y >= visual.ActualHeight - 12;
        SelectCanvasElement(key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        LayoutElementComboBox.SelectedItem = _layout.ForSide(CurrentSide).FirstOrDefault(x => x.Key == key);
        _selectedLayoutElement = definition;
        LoadLayoutProperties();
        RefreshLayoutPreview();
        if (_layout.Locked) return;

        _dragVisual = LayoutCanvas.Children.OfType<FrameworkElement>().FirstOrDefault(x => Equals(x.Tag, key));
        if (_dragVisual is null) return;
        Remember();
        _dragStarts = SelectedPlacements().ToDictionary(x => x.Key, x => x.Value.Clone());
        _dragStartPoint = e.GetPosition(LayoutCanvas);
        _resizing = resizing;
        var p = _layout.Get(key);
        _dragStartXmm = p.XMm;
        _dragStartYmm = p.YMm;
        Keyboard.Focus(LayoutCanvas);
        _dragVisual.CaptureMouse();
        e.Handled = true;
    }

    private void LayoutVisual_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragVisual is null || _selectedLayoutElement is null || e.LeftButton != MouseButtonState.Pressed || _layout.Locked) return;
        var point = e.GetPosition(LayoutCanvas);
        var dx = (point.X - _dragStartPoint.X) / CanvasScale;
        var dy = (point.Y - _dragStartPoint.Y) / CanvasScale;
        TransformSelection(dx, dy, _resizing);
        foreach (var visual in LayoutCanvas.Children.OfType<FrameworkElement>().Where(v => v.Tag is string k && _dragStarts.ContainsKey(k)))
        {
            var p = _layout.Get((string)visual.Tag);
            Canvas.SetLeft(visual, p.XMm * CanvasScale); Canvas.SetTop(visual, p.YMm * CanvasScale);
            visual.Width = p.WidthMm * CanvasScale; visual.Height = p.HeightMm * CanvasScale;
        }
        LoadLayoutProperties();
    }

    private void LayoutVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragVisual is null) return;
        _dragVisual.ReleaseMouseCapture();
        _dragVisual = null;
        RefreshLayoutPreview();
        e.Handled = true;
    }

    private void LoadLayoutProperties()
    {
        if (_selectedLayoutElement is null || LayoutXTextBox is null) return;
        var p = _layout.Get(_selectedLayoutElement.Key);
        LayoutTextOverride.Text = p.TextOverride ?? "";
        LayoutItalicCheckBox.IsChecked = p.Italic;
        LayoutStrokeTextBox.Text = F(p.StrokeWidthPt);
        LayoutShadowBlurTextBox.Text = F(p.ShadowRadiusPt);
        SelectedElementText.Text = _selectedLayoutElement.DisplayName;
        LayoutXTextBox.Text = F(p.XMm); LayoutYTextBox.Text = F(p.YMm);
        LayoutWidthTextBox.Text = F(p.WidthMm); LayoutHeightTextBox.Text = F(p.HeightMm);
        LayoutFontSizeTextBox.Text = F(p.FontSizePt);
        SelectFontFamily(p.FontFamilyKey);
        LayoutAlignmentComboBox.SelectedItem = p.Alignment;
        LayoutBoldCheckBox.IsChecked = p.Bold; LayoutVisibleCheckBox.IsChecked = p.Visible;
        LayoutTextColorTextBox.Text = p.TextColor;
        LayoutUnderlineCheckBox.IsChecked = p.UnderlineEnabled;
        LayoutUnderlineColorTextBox.Text = p.UnderlineColor;
        LayoutUnderlineThicknessTextBox.Text = F(p.UnderlineThicknessPt);
        LayoutUnderlineOffsetTextBox.Text = F(p.UnderlineOffsetMm);
        LayoutUnderlineModeComboBox.SelectedItem = p.UnderlineWidthMode;
        LayoutOutlineCheckBox.IsChecked = p.TextOutlineEnabled;
        LayoutOutlineColorTextBox.Text = p.TextOutlineColor;
        LayoutOutlineThicknessTextBox.Text = F(p.TextOutlineWidthPt);
        LayoutShadowCheckBox.IsChecked = p.ShadowEnabled;
        LayoutShadowColorTextBox.Text = p.ShadowColor;
        LayoutShadowOpacityTextBox.Text = F(p.ShadowOpacity);
        LayoutShadowXTextBox.Text = F(p.ShadowDxMm);
        LayoutShadowYTextBox.Text = F(p.ShadowDyMm);
    }

    private void ApplyLayoutProperties_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null) return;
        if (_layout.Locked) { MessageBox.Show("Layout is locked. Turn off Lock layout first.", "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var p = _layout.Get(_selectedLayoutElement.Key);
        Remember();
        p.TextOverride = string.IsNullOrEmpty(LayoutTextOverride.Text) ? null : LayoutTextOverride.Text;
        p.Italic = LayoutItalicCheckBox.IsChecked == true;
        p.StrokeWidthPt = Read(LayoutStrokeTextBox, p.StrokeWidthPt);
        p.ShadowRadiusPt = Read(LayoutShadowBlurTextBox, p.ShadowRadiusPt);
        p.XMm = Read(LayoutXTextBox, p.XMm); p.YMm = Read(LayoutYTextBox, p.YMm);
        p.WidthMm = Read(LayoutWidthTextBox, p.WidthMm); p.HeightMm = Read(LayoutHeightTextBox, p.HeightMm);
        p.FontSizePt = Read(LayoutFontSizeTextBox, p.FontSizePt);
        p.FontFamilyKey = SelectedFontFamily();
        if (LayoutAlignmentComboBox.SelectedItem is IdTextAlignment alignment) p.Alignment = alignment;
        p.Bold = LayoutBoldCheckBox.IsChecked == true; p.Visible = LayoutVisibleCheckBox.IsChecked == true;
        p.TextColor = NormalizeHex(LayoutTextColorTextBox.Text, p.TextColor);
        p.UnderlineEnabled = LayoutUnderlineCheckBox.IsChecked == true;
        p.UnderlineColor = NormalizeHex(LayoutUnderlineColorTextBox.Text, p.UnderlineColor);
        p.UnderlineThicknessPt = Read(LayoutUnderlineThicknessTextBox, p.UnderlineThicknessPt);
        p.UnderlineOffsetMm = Read(LayoutUnderlineOffsetTextBox, p.UnderlineOffsetMm);
        if (LayoutUnderlineModeComboBox.SelectedItem is IdUnderlineWidthMode mode) p.UnderlineWidthMode = mode;
        p.TextOutlineEnabled = LayoutOutlineCheckBox.IsChecked == true;
        p.TextOutlineColor = NormalizeHex(LayoutOutlineColorTextBox.Text, p.TextOutlineColor);
        p.TextOutlineWidthPt = Read(LayoutOutlineThicknessTextBox, p.TextOutlineWidthPt);
        p.ShadowEnabled = LayoutShadowCheckBox.IsChecked == true;
        p.ShadowColor = NormalizeHex(LayoutShadowColorTextBox.Text, p.ShadowColor);
        p.ShadowOpacity = Read(LayoutShadowOpacityTextBox, p.ShadowOpacity);
        p.ShadowDxMm = Read(LayoutShadowXTextBox, p.ShadowDxMm); p.ShadowDyMm = Read(LayoutShadowYTextBox, p.ShadowDyMm);
        ApplySnap(p); p.Clamp();
        LoadLayoutProperties(); RefreshLayoutPreview(); SetStatus("Layout element updated (save placement to persist)");
    }

    private void PickTextColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutTextColorTextBox);
    private void PickUnderlineColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutUnderlineColorTextBox);
    private void PickOutlineColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutOutlineColorTextBox);
    private void PickShadowColor_Click(object sender, RoutedEventArgs e) => PickColor(LayoutShadowColorTextBox);

    private static void PickColor(TextBox target)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            AnyColor = true,
            FullOpen = true,
            Color = System.Drawing.ColorTranslator.FromHtml(NormalizeHex(target.Text, "#000000"))
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            target.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private void NudgeLeft_Click(object sender, RoutedEventArgs e) => Nudge(-NudgeStep(), 0);
    private void NudgeRight_Click(object sender, RoutedEventArgs e) => Nudge(NudgeStep(), 0);
    private void NudgeUp_Click(object sender, RoutedEventArgs e) => Nudge(0, -NudgeStep());
    private void NudgeDown_Click(object sender, RoutedEventArgs e) => Nudge(0, NudgeStep());

    private void Nudge(double dx, double dy)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        _dragStarts = SelectedPlacements().ToDictionary(x => x.Key, x => x.Value.Clone());
        TransformSelection(dx, dy, false);
        LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void CenterX_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        var p = _layout.Get(_selectedLayoutElement.Key); p.XMm = (LayoutCatalog.CardWidthMm - p.WidthMm) / 2; p.Clamp(); LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void CenterY_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        var p = _layout.Get(_selectedLayoutElement.Key); p.YMm = (LayoutCatalog.CardHeightMm - p.HeightMm) / 2; p.Clamp(); LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void ResetSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutElement is null || _layout.Locked) return;
        Remember();
        _layout.Elements[_selectedLayoutElement.Key] = LayoutCatalog.DefaultPlacement(_selectedLayoutElement);
        LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void ResetSide_Click(object sender, RoutedEventArgs e)
    {
        if (_layout.Locked) return;
        Remember();
        foreach (var def in _layout.ForSide(CurrentSide)) _layout.Elements[def.Key] = LayoutCatalog.DefaultPlacement(def);
        LoadLayoutProperties(); RefreshLayoutPreview();
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        _layout.SnapToGrid = SnapToGridCheckBox.IsChecked == true;
        _layout.GridSizeMm = SelectedGridSize();
        _layout.Locked = LayoutLockedCheckBox.IsChecked == true;
        _store.SaveLayout(_layout);
        SetStatus("Placement saved • applies to all IDs");
    }

    private void SnapSettingsChanged(object sender, RoutedEventArgs e)
    {
        if (SnapToGridCheckBox is null || GridSizeComboBox is null) return;
        _layout.SnapToGrid = SnapToGridCheckBox.IsChecked == true;
        _layout.GridSizeMm = SelectedGridSize();
    }

    private void LayoutLockChanged(object sender, RoutedEventArgs e)
    {
        if (LayoutLockedCheckBox is null) return;
        _layout.Locked = LayoutLockedCheckBox.IsChecked == true;
        _store.SaveLayout(_layout);
        RefreshLayoutPreview();
    }

    private void ApplySnap(ElementPlacement p)
    {
        if (!_layout.SnapToGrid) return;
        var step = Math.Max(0.1, _layout.GridSizeMm);
        p.XMm = Math.Round(p.XMm / step) * step;
        p.YMm = Math.Round(p.YMm / step) * step;
    }

    private double NudgeStep() => _layout.SnapToGrid ? Math.Max(0.1, _layout.GridSizeMm) : 0.5;

    private void ChooseFrontBackground_Click(object sender, RoutedEventArgs e) => ChooseSettingAsset("front-background", p => _settings.FrontBackgroundPath = p, FrontBackgroundTextBox, cleanSignature: false);
    private void ChooseBackBackground_Click(object sender, RoutedEventArgs e) => ChooseSettingAsset("back-background", p => _settings.BackBackgroundPath = p, BackBackgroundTextBox, cleanSignature: false);
    private void ChooseLogo1_Click(object sender, RoutedEventArgs e) => ChooseSettingAsset("logo1", p => _settings.Logo1Path = p, Logo1TextBox, cleanSignature: false);
    private void ChooseLogo2_Click(object sender, RoutedEventArgs e) => ChooseSettingAsset("logo2", p => _settings.Logo2Path = p, Logo2TextBox, cleanSignature: false);
    private void ChooseCaptainSignature_Click(object sender, RoutedEventArgs e) => ChooseSettingAsset("captain-signature", p => _settings.CaptainSignaturePath = p, CaptainSignatureTextBox, cleanSignature: true);

    private void ChooseSettingAsset(string category, Action<string> setter, TextBox target, bool cleanSignature)
    {
        var source = ChooseImage(); if (source is null) return;
        try
        {
            var saved = cleanSignature ? _images.CleanSignatureToTransparent(source) : _assets.Import(source, category);
            setter(saved); target.Text = saved; _store.SaveSettings(_settings); RefreshLayoutPreview(); SetStatus("Asset imported");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void LoadSettingsIntoUi()
    {
        FrontBackgroundTextBox.Text = _settings.FrontBackgroundPath ?? string.Empty;
        BackBackgroundTextBox.Text = _settings.BackBackgroundPath ?? string.Empty;
        Logo1TextBox.Text = _settings.Logo1Path ?? string.Empty;
        Logo2TextBox.Text = _settings.Logo2Path ?? string.Empty;
        CaptainSignatureTextBox.Text = _settings.CaptainSignaturePath ?? string.Empty;
        CaptainNameTextBox.Text = _settings.CaptainName; CaptainTitleTextBox.Text = _settings.CaptainTitle;
        IssuerTextBox.Text = _settings.IssuerName; FooterAddressTextBox.Text = _settings.FooterAddress; FooterContactTextBox.Text = _settings.FooterContact;
        OuterCutGuideCheckBox.IsChecked = _settings.OuterCutGuideEnabled;
        PhotoOutlineCheckBox.IsChecked = _settings.PhotoOutlineEnabled; EmployeeDividerCheckBox.IsChecked = _settings.EmployeeDividerEnabled;
        SignatureLineCheckBox.IsChecked = _settings.SignatureLineEnabled; QrOutlineCheckBox.IsChecked = _settings.QrOutlineEnabled;
        BackDividerCheckBox.IsChecked = _settings.BackDividerEnabled; OutlineThicknessTextBox.Text = F(_settings.OutlineThicknessPt);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.CaptainName = CaptainNameTextBox.Text.Trim(); _settings.CaptainTitle = CaptainTitleTextBox.Text.Trim();
        _settings.IssuerName = IssuerTextBox.Text.Trim(); _settings.FooterAddress = FooterAddressTextBox.Text.Trim(); _settings.FooterContact = FooterContactTextBox.Text.Trim();
        _settings.OuterCutGuideEnabled = OuterCutGuideCheckBox.IsChecked == true; _settings.PhotoOutlineEnabled = PhotoOutlineCheckBox.IsChecked == true;
        _settings.EmployeeDividerEnabled = EmployeeDividerCheckBox.IsChecked == true; _settings.SignatureLineEnabled = SignatureLineCheckBox.IsChecked == true;
        _settings.QrOutlineEnabled = QrOutlineCheckBox.IsChecked == true; _settings.BackDividerEnabled = BackDividerCheckBox.IsChecked == true;
        _settings.OutlineThicknessPt = Math.Clamp(Read(OutlineThicknessTextBox, _settings.OutlineThicknessPt), 0.3, 1.5);
        _store.SaveSettings(_settings); RefreshLayoutPreview(); SetStatus("Settings saved");
    }

    private string ResolvePreviewText(LayoutElementDefinition definition)
    {
        if (_layout.Get(definition.Key).BindingKey is string bindingKey) definition = definition with { Key = bindingKey };
        var employee = _employees.FirstOrDefault();
        if (employee is null) return definition.SampleText;
        return definition.Key switch
        {
            "front_name_value" => employee.FullName,
            "front_designation_value" => employee.Position,
            "front_employee_no_value" => employee.ControlNumber,
            "back_dob_value" => PdfExportService.FormatDate(employee.Birthdate),
            "back_sex_value" => employee.Sex,
            "back_civil_value" => employee.CivilStatus,
            "back_address_value" => employee.Address,
            "back_issuer_value" => _settings.IssuerName,
            "back_captain_name" => _settings.CaptainName,
            "back_captain_title" => _settings.CaptainTitle,
            "back_footer_address" => _settings.FooterAddress,
            "back_footer_contact" => _settings.FooterContact,
            _ => definition.SampleText
        };
    }

    private string? ResolvePreviewImage(string key)
    {
        var employee = _employees.FirstOrDefault();
        return key switch
        {
            "front_logo_1" => _settings.Logo1Path,
            "front_logo_2" => _settings.Logo2Path,
            "front_photo" => employee?.PhotoPath,
            "front_signature" => employee?.SignaturePath,
            "front_qr" => employee?.QrImagePath,
            "back_captain_signature" => _settings.CaptainSignaturePath,
            _ => null
        };
    }

    private static string? ChooseImage()
    {
        var dialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static Brush BrushFromHex(string value) => new SolidColorBrush(MediaColorFromHex(value));
    private static Color MediaColorFromHex(string value)
    {
        try { return (Color)ColorConverter.ConvertFromString(NormalizeHex(value, "#000000")); }
        catch { return Colors.Black; }
    }

    private static string NormalizeHex(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim(); if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length != 7) return fallback;
        return text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
    }

    private static string MapPreviewFont(string key) => key switch { "serif" => "Times New Roman", "monospace" => "Consolas", "sans" => "Arial", _ => key };
    private void SelectFontFamily(string key)
    {
        foreach (var item in LayoutFontFamilyComboBox.Items.OfType<ComboBoxItem>()) if (Equals(item.Tag?.ToString(), key)) { LayoutFontFamilyComboBox.SelectedItem = item; return; }
        var extra = new ComboBoxItem { Tag = key, Content = key };
        LayoutFontFamilyComboBox.Items.Add(extra); LayoutFontFamilyComboBox.SelectedItem = extra;
    }
    private string SelectedFontFamily() => (LayoutFontFamilyComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "sans";

    private double SelectedGridSize()
    {
        var text = (GridSizeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 1;
    }

    private void SelectStatus(string status)
    {
        foreach (var item in RecordStatusComboBox.Items.OfType<ComboBoxItem>()) if (string.Equals(item.Content?.ToString(), status, StringComparison.OrdinalIgnoreCase)) { RecordStatusComboBox.SelectedItem = item; return; }
        RecordStatusComboBox.SelectedIndex = 0;
    }
    private string GetSelectedStatus() => (RecordStatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Active";

    private static double Read(TextBox box, double fallback) => double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;
    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private void SetStatus(string text) => StatusText.Text = $"{text}\nOffline • Windows v0.2.0";
    private void ShowError(Exception ex) { SetStatus("Operation failed"); MessageBox.Show(ex.Message, "DIKERMA", MessageBoxButton.OK, MessageBoxImage.Error); }
}
