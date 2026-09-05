using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dikerma.Windows.Models;
using Dikerma.Windows.Services;

namespace Dikerma.Windows;

public sealed class ImageToolsWindow : Window
{
    public ElementPlacement Result { get; }
    public string? ProcessedPath { get; private set; }
    private readonly string _original;
    private readonly OfflineImageProcessor _processor;
    private readonly Border _preview = new() { Background = Brushes.LightGray, Padding = new Thickness(8) };
    private readonly Slider[] _crop = new Slider[4];
    private readonly Slider _tolerance = new() { Minimum = 10, Maximum = 160, Value = 75, TickFrequency = 10 };
    private readonly Slider _feather = new() { Minimum = 1, Maximum = 60, Value = 20 };
    private readonly CheckBox _white = new() { Content = "Replace with white (unchecked = transparent)", IsChecked = true };
    private readonly Button _clean = new() { Content = "Preview background cleanup", Margin = new Thickness(0, 8, 0, 8) };

    public ImageToolsWindow(string path, ElementPlacement p, OfflineImageProcessor processor)
    {
        _original = path; _processor = processor; Result = p.Clone();
        Title = "Crop and background cleanup"; Width = 860; Height = 680; MinWidth = 700; MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        grid.Children.Add(_preview);
        var panel = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 1); grid.Children.Add(scroll); Content = grid;
        panel.Children.Add(new TextBlock { Text = "Non-destructive crop (%)", FontWeight = FontWeights.Bold });
        var values = new[] { p.CropLeft, p.CropTop, p.CropRight, p.CropBottom };
        var labels = new[] { "Left", "Top", "Right", "Bottom" };
        for (int i = 0; i < 4; i++)
        {
            panel.Children.Add(new TextBlock { Text = labels[i] });
            _crop[i] = new Slider { Minimum = 0, Maximum = 95, Value = values[i] * 100, TickFrequency = 1, IsSnapToTickEnabled = true, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
            panel.Children.Add(_crop[i]);
        }
        foreach (var slider in _crop) slider.ValueChanged += (_, _) => UpdatePreview();
        var reset = new Button { Content = "Reset crop" }; reset.Click += (_, _) => { foreach (var slider in _crop) slider.Value = 0; }; panel.Children.Add(reset);
        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock { Text = "Offline background cleanup", FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "Best for plain backgrounds. Only connected edge colors are removed. Check hair and clothing before applying.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Tolerance" }); panel.Children.Add(_tolerance);
        panel.Children.Add(new TextBlock { Text = "Edge softness" }); panel.Children.Add(_feather); panel.Children.Add(_white);
        _clean.Click += async (_, _) =>
        {
            _clean.IsEnabled = false;
            var tolerance = _tolerance.Value; var feather = _feather.Value; var white = _white.IsChecked == true;
            try { ProcessedPath = await Task.Run(() => _processor.CleanBackground(_original, white, tolerance, feather)); UpdatePreview(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Image cleanup"); }
            finally { _clean.IsEnabled = true; }
        };
        panel.Children.Add(_clean);
        var original = new Button { Content = "Restore original image" }; original.Click += (_, _) => { ProcessedPath = null; UpdatePreview(); }; panel.Children.Add(original);
        panel.Children.Add(new TextBlock { Text = "Cleanup creates a static image for this layer, shared by all IDs. Use record photo import for individual portraits.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        var apply = new Button { Content = "Apply crop / image", Margin = new Thickness(0, 8, 0, 0) }; apply.Click += (_, _) => { if (_clean.IsEnabled) { UpdatePreview(); DialogResult = true; } }; panel.Children.Add(apply);
        var cancel = new Button { Content = "Cancel", IsCancel = true }; panel.Children.Add(cancel);
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        Result.CropLeft = _crop[0].Value / 100; Result.CropTop = _crop[1].Value / 100;
        Result.CropRight = _crop[2].Value / 100; Result.CropBottom = _crop[3].Value / 100; Result.Clamp();
        var d = new LayoutElementDefinition("preview", IdLayoutSide.Front, "Image", IdLayoutKind.Image, 0, 0, Result.WidthMm, Result.HeightMm);
        _preview.Child = new Viewbox { Child = ElementRenderer.Create(d, Result, "", ProcessedPath ?? _original), Stretch = Stretch.Uniform };
    }
}
