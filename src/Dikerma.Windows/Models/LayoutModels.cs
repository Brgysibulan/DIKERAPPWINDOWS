namespace Dikerma.Windows.Models;

public enum IdLayoutSide { Front, Back }
public enum IdLayoutKind { Text, Image, Rectangle, Ellipse }
public enum IdTextAlignment { Left, Center, Right }
public enum IdUnderlineWidthMode { Text, Element }

public sealed class ElementPlacement
{
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
        WidthMm = Math.Clamp(WidthMm, 4, LayoutCatalog.CardWidthMm);
        HeightMm = Math.Clamp(HeightMm, 2, LayoutCatalog.CardHeightMm);
        XMm = Math.Clamp(XMm, 0, Math.Max(0, LayoutCatalog.CardWidthMm - WidthMm));
        YMm = Math.Clamp(YMm, 0, Math.Max(0, LayoutCatalog.CardHeightMm - HeightMm));
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
    public bool Locked { get; set; }
    public double GridSizeMm { get; set; } = 1;
    public bool SnapToGrid { get; set; } = true;
    public Dictionary<string, ElementPlacement> Elements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CustomLayoutElement> CustomElements { get; set; } = new();

    public ElementPlacement Get(string key)
    {
        if (!Elements.TryGetValue(key, out var placement))
        {
            var definition = LayoutCatalog.Find(key);
            placement = definition is null
                ? new ElementPlacement { XMm = 10, YMm = 10, WidthMm = 30, HeightMm = 10, FontSizePt = 10 }
                : LayoutCatalog.DefaultPlacement(definition);
            Elements[key] = placement;
        }
        return placement;
    }
}

public sealed class CustomLayoutElement
{
    public string Key { get; set; } = $"custom_{Guid.NewGuid():N}";
    public IdLayoutSide Side { get; set; }
    public string Name { get; set; } = "New element";
    public IdLayoutKind Kind { get; set; } = IdLayoutKind.Text;
    public string Content { get; set; } = "EDIT TEXT";
    public string? ImagePath { get; set; }
    public string FillColor { get; set; } = "#FFFFFF";
    public string BorderColor { get; set; } = "#00522D";
    public double BorderWidthPt { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public int ZIndex { get; set; } = 100;

    public LayoutElementDefinition ToDefinition() => new(
        Key, Side, Name, Kind, 10, 10, 30, 10, 10, IdTextAlignment.Center,
        "#000000", false, Content);
}
