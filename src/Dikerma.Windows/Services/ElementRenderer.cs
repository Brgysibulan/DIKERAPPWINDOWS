using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Dikerma.Windows.Models;

namespace Dikerma.Windows.Services;

// One renderer for the Studio and 300 dpi PDF output, including imported fonts and effects.
public sealed class ElementRenderer : FrameworkElement
{
    private const double DipPerMm = 96.0 / 25.4;
    private readonly LayoutElementDefinition _definition;
    private readonly ElementPlacement _p;
    private readonly string _text;
    private readonly string? _image;

    private ElementRenderer(LayoutElementDefinition definition, ElementPlacement p, string text, string? image)
    {
        _definition = definition; _p = p; _text = p.TextOverride ?? text; _image = image;
        Width = p.WidthMm * DipPerMm; Height = p.HeightMm * DipPerMm;
        ClipToBounds = true;
        if (p.ShadowEnabled) Effect = new DropShadowEffect
        {
            Color = ColorOf(p.ShadowColor), Opacity = p.ShadowOpacity,
            BlurRadius = p.ShadowRadiusPt * 96 / 72,
            ShadowDepth = Math.Sqrt(p.ShadowDxMm * p.ShadowDxMm + p.ShadowDyMm * p.ShadowDyMm) * DipPerMm,
            Direction = -Math.Atan2(p.ShadowDyMm, p.ShadowDxMm) * 180 / Math.PI
        };
    }

    public static ElementRenderer Create(LayoutElementDefinition d, ElementPlacement p, string text, string? image) => new(d, p, text, image);

    protected override void OnRender(DrawingContext dc)
    {
        var brush = new SolidColorBrush(ColorOf(_p.TextColor));
        var stroke = Math.Min(_p.StrokeWidthPt * 96 / 72, Math.Min(Width, Height));
        var pen = new Pen(brush, stroke);
        var rect = new Rect(0, 0, Width, Height);
        switch (_definition.Kind)
        {
            case IdLayoutKind.Image:
                if (!string.IsNullOrWhiteSpace(_image) && File.Exists(_image))
                {
                    BitmapSource bitmap;
                    try { bitmap = OfflineImageProcessor.LoadPreview(_image); }
                    catch { break; }
                    int left = (int)(_p.CropLeft * bitmap.PixelWidth), top = (int)(_p.CropTop * bitmap.PixelHeight);
                    int width = Math.Max(1, (int)((1 - _p.CropLeft - _p.CropRight) * bitmap.PixelWidth));
                    int height = Math.Max(1, (int)((1 - _p.CropTop - _p.CropBottom) * bitmap.PixelHeight));
                    dc.DrawImage(new CroppedBitmap(bitmap, new Int32Rect(left, top, Math.Min(width, bitmap.PixelWidth - left), Math.Min(height, bitmap.PixelHeight - top))), rect);
                }
                break;
            case IdLayoutKind.HorizontalLine: dc.DrawLine(pen, new Point(0, Height / 2), new Point(Width, Height / 2)); break;
            case IdLayoutKind.VerticalLine: dc.DrawLine(pen, new Point(Width / 2, 0), new Point(Width / 2, Height)); break;
            case IdLayoutKind.Rectangle: dc.DrawRectangle(brush, null, rect); break;
            case IdLayoutKind.Ellipse: dc.DrawEllipse(brush, null, new Point(Width / 2, Height / 2), Width / 2, Height / 2); break;
            default:
                var family = _p.FontFamilyKey switch { "sans" => "Arial", "serif" => "Times New Roman", "monospace" => "Consolas", _ => _p.FontFamilyKey };
                var text = new FormattedText(_text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily(family), _p.Italic ? FontStyles.Italic : FontStyles.Normal,
                        _p.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                    _p.FontSizePt * 96 / 72, brush, 1)
                {
                    MaxTextWidth = Width, MaxTextHeight = Height,
                    TextAlignment = _p.Alignment switch { IdTextAlignment.Center => TextAlignment.Center, IdTextAlignment.Right => TextAlignment.Right, _ => TextAlignment.Left },
                    Trimming = TextTrimming.None
                };
                var geometry = text.BuildGeometry(new Point(0, 0));
                dc.DrawGeometry(brush, _p.TextOutlineEnabled ? new Pen(new SolidColorBrush(ColorOf(_p.TextOutlineColor)), _p.TextOutlineWidthPt * 96 / 72) : null, geometry);
                if (_p.UnderlineEnabled)
                {
                    var w = _p.UnderlineWidthMode == IdUnderlineWidthMode.Element ? Width : Math.Min(Width, text.Width);
                    var x = _p.Alignment == IdTextAlignment.Center ? (Width - w) / 2 : _p.Alignment == IdTextAlignment.Right ? Width - w : 0;
                    var y = Math.Min(Height - _p.UnderlineThicknessPt, text.Baseline + _p.UnderlineOffsetMm * DipPerMm);
                    dc.DrawLine(new Pen(new SolidColorBrush(ColorOf(_p.UnderlineColor)), _p.UnderlineThicknessPt * 96 / 72), new Point(x, y), new Point(x + w, y));
                }
                break;
        }
    }

    public static MemoryStream Png(LayoutElementDefinition d, ElementPlacement p, string text, string? image)
    {
        var element = Create(d, p, text, image);
        element.Measure(new Size(element.Width, element.Height));
        element.Arrange(new Rect(0, 0, element.Width, element.Height));
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(p.WidthMm / 25.4 * 300)),
            Math.Max(1, (int)Math.Ceiling(p.HeightMm / 25.4 * 300)), 300, 300, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var stream = new MemoryStream(); encoder.Save(stream); stream.Position = 0; return stream;
    }

    private static Color ColorOf(string value)
    {
        try { return (Color)ColorConverter.ConvertFromString(value); } catch { return Colors.Black; }
    }
}
