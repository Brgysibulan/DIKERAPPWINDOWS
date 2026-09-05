namespace Dikerma.Windows.Models;

public enum IdLayoutSide { Front, Back }
public enum IdLayoutKind { Text, Image, HorizontalLine, VerticalLine, Rectangle, Ellipse }
public enum IdTextAlignment { Left, Center, Right }
public enum IdUnderlineWidthMode { Text, Element }

public sealed class ElementPlacement
{
    public string? BindingKey { get; set; }
    public string? TextOverride { get; set; }
    public string? ImagePath { get; set; }
    public string? GroupId { get; set; }
    public int ZIndex { get; set; }
    public bool Italic { get; set; }
    public double CropLeft { get; set; }
    public double CropTop { get; set; }
    public double CropRight { get; set; }
    public double CropBottom { get; set; }
    public double StrokeWidthPt { get; set; } = 1;
    public double XMm { get; set; }
    public double YMm { get; set; }
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
    public double FontSizePt { get; set; } = 7;
    public string FontFamilyKey { get; set; } = "sans";
    public bool Bold { get; set; }
    public IdTextAlignment Alignment { get; set; } = IdTextAlignment.Left;
    public string TextColor { get; set; } = "#000000";
    public bool UnderlineEnabled { get; set; }
    public string UnderlineColor { get; set; } = "#000000";
    public double UnderlineThicknessPt { get; set; } = 0.45;
    public double UnderlineOffsetMm { get; set; } = 0.7;
    public IdUnderlineWidthMode UnderlineWidthMode { get; set; } = IdUnderlineWidthMode.Text;
    public bool TextOutlineEnabled { get; set; }
    public string TextOutlineColor { get; set; } = "#FFFFFF";
    public double TextOutlineWidthPt { get; set; } = 0.35;
    public bool ShadowEnabled { get; set; }
    public string ShadowColor { get; set; } = "#000000";
    public double ShadowOpacity { get; set; } = 0.35;
    public double ShadowDxMm { get; set; } = 0.45;
    public double ShadowDyMm { get; set; } = 0.45;
    public double ShadowRadiusPt { get; set; } = 0;
    public bool Visible { get; set; } = true;

    public ElementPlacement Clone() => (ElementPlacement)MemberwiseClone();

    public void Clamp()
    {
        WidthMm = Math.Clamp(WidthMm, 0.3, LayoutCatalog.CardWidthMm);
        HeightMm = Math.Clamp(HeightMm, 0.3, LayoutCatalog.CardHeightMm);
        XMm = Math.Clamp(XMm, 0, Math.Max(0, LayoutCatalog.CardWidthMm - WidthMm));
        YMm = Math.Clamp(YMm, 0, Math.Max(0, LayoutCatalog.CardHeightMm - HeightMm));
        CropLeft = Math.Clamp(CropLeft, 0, 0.95);
        CropRight = Math.Clamp(CropRight, 0, 0.95 - CropLeft);
        CropTop = Math.Clamp(CropTop, 0, 0.95);
        CropBottom = Math.Clamp(CropBottom, 0, 0.95 - CropTop);
        StrokeWidthPt = Math.Clamp(StrokeWidthPt, 0.2, 12);
        FontSizePt = Math.Clamp(FontSizePt, 3.5, 36);
        UnderlineThicknessPt = Math.Clamp(UnderlineThicknessPt, 0.15, 2);
        UnderlineOffsetMm = Math.Clamp(UnderlineOffsetMm, 0, 3);
        TextOutlineWidthPt = Math.Clamp(TextOutlineWidthPt, 0.15, 2);
        ShadowOpacity = Math.Clamp(ShadowOpacity, 0, 1);
        ShadowDxMm = Math.Clamp(ShadowDxMm, -3, 3);
        ShadowDyMm = Math.Clamp(ShadowDyMm, -3, 3);
        ShadowRadiusPt = Math.Clamp(ShadowRadiusPt, 0, 4);
    }
}

public sealed record LayoutElementDefinition(
    string Key,
    IdLayoutSide Side,
    string DisplayName,
    IdLayoutKind Kind,
    double DefaultXmm,
    double DefaultYmm,
    double DefaultWidthMm,
    double DefaultHeightMm,
    double DefaultFontPt = 7,
    IdTextAlignment DefaultAlignment = IdTextAlignment.Left,
    string DefaultColor = "#000000",
    bool DefaultBold = false,
    string SampleText = "");

public sealed class LayoutProfile
{
    public int SchemaVersion { get; set; } = 2;
    public List<LayoutElementDefinition> CustomElements { get; set; } = new();
    public IEnumerable<LayoutElementDefinition> ForSide(IdLayoutSide side) =>
        LayoutCatalog.ForSide(side).Concat(CustomElements.Where(e => e.Side == side)).OrderBy(e => Get(e.Key).ZIndex);
    public LayoutElementDefinition? Find(string key) => LayoutCatalog.Find(key) ?? CustomElements.FirstOrDefault(e => e.Key == key);
    public bool Locked { get; set; }
    public double GridSizeMm { get; set; } = 1;
    public bool SnapToGrid { get; set; } = true;
    public Dictionary<string, ElementPlacement> Elements { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ElementPlacement Get(string key)
    {
        if (!Elements.TryGetValue(key, out var placement))
        {
            var definition = Find(key) ?? throw new InvalidOperationException($"Unknown layout element: {key}");
            placement = LayoutCatalog.DefaultPlacement(definition);
            Elements[key] = placement;
        }
        return placement;
    }
}
