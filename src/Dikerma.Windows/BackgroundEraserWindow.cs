using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dikerma.Windows.Services;

namespace Dikerma.Windows;

public sealed class BackgroundEraserWindow : Window
{
    public string? OutputPath { get; private set; }
    private readonly OfflineImageProcessor _processor;
    private readonly EraserSession _session;
    private readonly Image _image = new() { Stretch = Stretch.Fill, Cursor = Cursors.Cross };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly StackPanel _controls = new();
    private readonly Slider _strength = Slider(20, 150, 75);
    private readonly Slider _feather = Slider(0, 5, 3);
    private readonly Slider _brush = Slider(2, 150, 30);
    private readonly Slider _softness = Slider(0, 1, 0.5);
    private readonly RadioButton _restore = new() { Content = "Restore brush", GroupName = "brush", Margin = new Thickness(0, 6, 0, 6) };
    private readonly CheckBox _white = new() { Content = "White background", IsChecked = true };
    private readonly CheckBox _original = new() { Content = "Show original (comparison)", Margin = new Thickness(0, 10, 0, 10) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
    private Point? _last;
    private bool _busy;
    private double _zoom = 1;

    public BackgroundEraserWindow(string source, OfflineImageProcessor processor, bool white = true)
    {
        _processor = processor; _session = processor.CreateEraserSession(source);
        Title = "Advanced BG Eraser • DIKERMA"; Width = 1080; Height = 780; MinWidth = 780; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        var canvas = new Border { Child = _image, Background = Checkerboard(), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _scroll.Content = canvas; grid.Children.Add(_scroll);
        var settings = new ScrollViewer { Content = _controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16, 0, 0, 0) };
        Grid.SetColumn(settings, 1); grid.Children.Add(settings); Content = grid;
        _controls.Children.Add(new TextBlock { Text = "Advanced BG Eraser", FontSize = 22, FontWeight = FontWeights.Bold });
        _controls.Children.Add(new TextBlock { Text = "Auto-remove a plain backdrop, then brush to refine hair, clothing and difficult areas. Works offline.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        Label("Removal strength", _strength); Label("Edge feather (pixels)", _feather);
        AddButton("Auto remove background", async () =>
        {
            _busy = true; _controls.IsEnabled = false; _image.IsEnabled = false;
            var strength = _strength.Value; var feather = _feather.Value;
            _status.Text = "Processing…";
            try
            {
                var mask = await Task.Run(() => _processor.CreateMask(_session, strength, feather));
                _session.ReplaceMask(mask); _original.IsChecked = false; RefreshImage();
                _status.Text = "Inspect the edges. Use Restore to recover detail; Erase to remove leftovers.";
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { _busy = false; _controls.IsEnabled = true; _image.IsEnabled = true; }
        });
        _controls.Children.Add(new Separator());
        _controls.Children.Add(new RadioButton { Content = "Erase brush", GroupName = "brush", IsChecked = true });
        _controls.Children.Add(_restore); Label("Brush radius (image pixels)", _brush); Label("Brush softness", _softness);
        AddButton("Undo", () => { _session.Undo(); RefreshImage(); });
        AddButton("Redo", () => { _session.Redo(); RefreshImage(); });
        AddButton("Reset to original", () => { _session.Reset(); RefreshImage(); });
        _white.IsChecked = white; _controls.Children.Add(_white);
        _controls.Children.Add(new TextBlock { Text = "Unchecked = transparent PNG", Foreground = Brushes.DimGray });
        _controls.Children.Add(_original);
        _white.Click += (_, _) => RefreshImage(); _original.Click += (_, _) => RefreshImage();
        AddButton("Fit image", Fit); AddButton("Zoom +", () => SetZoom(_zoom * 1.25)); AddButton("Zoom −", () => SetZoom(_zoom / 1.25));
        AddButton("Apply image", () =>
        {
            try { OutputPath = _processor.SaveEraser(_session, _white.IsChecked == true); DialogResult = true; }
            catch (Exception ex) { _status.Text = ex.Message; }
        });
        _controls.Children.Add(new Button { Content = "Cancel", IsCancel = true });
        _controls.Children.Add(_status);
        _image.MouseLeftButtonDown += (_, e) =>
        {
            if (_busy || _original.IsChecked == true) return;
            _session.BeginEdit(); _image.CaptureMouse(); _last = ImagePoint(e); PaintTo(_last.Value); e.Handled = true;
        };
        _image.MouseMove += (_, e) => { if (_last is not null && e.LeftButton == MouseButtonState.Pressed) PaintTo(ImagePoint(e)); };
        _image.MouseLeftButtonUp += (_, _) => { _last = null; _image.ReleaseMouseCapture(); };
        _image.LostMouseCapture += (_, _) => _last = null;
        _scroll.PreviewMouseWheel += (_, e) =>
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
            SetZoom(_zoom * (e.Delta > 0 ? 1.25 : 0.8)); e.Handled = true;
        };
        Closing += (_, e) => { if (_busy) { e.Cancel = true; _status.Text = "Please wait for processing to finish."; } };
        Loaded += (_, _) => Fit(); RefreshImage();
    }
    private Point ImagePoint(MouseEventArgs e)
    {
        var p = e.GetPosition(_image);
        return new Point(p.X / _image.ActualWidth * _session.Width, p.Y / _image.ActualHeight * _session.Height);
    }
    private void PaintTo(Point target)
    {
        var start = _last ?? target; var length = (target - start).Length;
        var steps = Math.Max(1, (int)Math.Ceiling(length / Math.Max(1, _brush.Value / 3)));
        for (int i = 1; i <= steps; i++)
            _session.Paint(start.X + (target.X - start.X) * i / steps, start.Y + (target.Y - start.Y) * i / steps, _brush.Value, _restore.IsChecked == true, _softness.Value);
        _last = target; RefreshImage();
    }
    private void RefreshImage()
    {
        var pixels = _session.Composite(_white.IsChecked == true, _original.IsChecked == true);
        _image.Source = BitmapSource.Create(_session.Width, _session.Height, 96, 96, PixelFormats.Bgra32, null, pixels, _session.Width * 4);
    }
    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 0.02, 8); _image.Width = _session.Width * _zoom; _image.Height = _session.Height * _zoom;
        _status.Text = $"Zoom {_zoom:P0} • Ctrl+wheel to zoom";
    }
    private void Fit() => SetZoom(Math.Min((_scroll.ActualWidth - 24) / _session.Width, (_scroll.ActualHeight - 24) / _session.Height));
    private void Label(string text, Slider slider) { _controls.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 0) }); _controls.Children.Add(slider); }
    private void AddButton(string label, Action action) { var button = new Button { Content = label }; button.Click += (_, _) => action(); _controls.Children.Add(button); }
    private static Slider Slider(double min, double max, double value) => new() { Minimum = min, Maximum = max, Value = value, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
    private static Brush Checkerboard()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
        group.Children.Add(new GeometryDrawing(Brushes.LightGray, null, Geometry.Parse("M0,0 H10 V10 H0 Z M10,10 H20 V20 H10 Z")));
        return new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 20, 20), ViewportUnits = BrushMappingMode.Absolute };
    }
}
