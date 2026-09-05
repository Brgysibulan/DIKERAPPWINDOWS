using System.Globalization;
using Dikerma.Windows.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Dikerma.Windows.Services;

public sealed class PdfExportService
{
    private const double PointsPerMm = 72.0 / 25.4;
    private const double LeftMarginMm = 20.0;
    private const double TopMarginMm = 23.0;
    private const double RowGapMm = 21.0;

    public void Export(string outputPath, EmployeeRecord person1, EmployeeRecord? person2, AppSettingsModel settings, LayoutProfile layout)
    {
        var document = new PdfDocument();
        document.Info.Title = "Barangay Sibulan Employee IDs";
        document.Info.Subject = "85 x 115 mm front/back ID print sheet";

        var page = document.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;

        using var graphics = XGraphics.FromPdfPage(page);

        RenderPair(graphics, person1, TopMarginMm, settings, layout);
        if (person2 is not null)
        {
            RenderPair(graphics, person2, TopMarginMm + LayoutCatalog.CardHeightMm + RowGapMm, settings, layout);
        }

        var folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        document.Save(outputPath);
    }

    private void RenderPair(XGraphics gfx, EmployeeRecord employee, double yMm, AppSettingsModel settings, LayoutProfile layout)
    {
        RenderCard(gfx, LeftMarginMm, yMm, IdLayoutSide.Front, employee, settings, layout);
        RenderCard(gfx, LeftMarginMm + LayoutCatalog.CardWidthMm, yMm, IdLayoutSide.Back, employee, settings, layout);
    }

    private void RenderCard(XGraphics gfx, double cardXmm, double cardYmm, IdLayoutSide side, EmployeeRecord employee, AppSettingsModel settings, LayoutProfile layout)
    {
        var x = Mm(cardXmm);
        var y = Mm(cardYmm);
        var w = Mm(LayoutCatalog.CardWidthMm);
        var h = Mm(LayoutCatalog.CardHeightMm);

        var background = side == IdLayoutSide.Front ? settings.FrontBackgroundPath : settings.BackBackgroundPath;
        if (!TryDrawImage(gfx, background, x, y, w, h))
        {
            DrawFallbackBackground(gfx, x, y, w, h, side);
        }

        foreach (var definition in LayoutCatalog.ForSide(side))
        {
            var placement = layout.Get(definition.Key);
            if (!placement.Visible) continue;

            var ex = x + Mm(placement.XMm);
            var ey = y + Mm(placement.YMm);
            var ew = Mm(placement.WidthMm);
            var eh = Mm(placement.HeightMm);

            if (definition.Kind == IdLayoutKind.Image)
            {
                var imagePath = ResolveImage(definition.Key, employee, settings);
                TryDrawImage(gfx, imagePath, ex, ey, ew, eh);
            }
            else
            {
                var text = ResolveText(definition.Key, definition.SampleText, employee, settings);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    DrawStyledText(gfx, text, ex, ey, ew, eh, placement);
                }
            }
        }

        DrawOptionalLines(gfx, x, y, settings, layout, side);

        if (settings.OuterCutGuideEnabled)
        {
            var pen = new XPen(XColors.Black, Math.Max(0.25, settings.OutlineThicknessPt));
            gfx.DrawRectangle(pen, x, y, w, h);
        }
    }

    private static void DrawFallbackBackground(XGraphics gfx, double x, double y, double w, double h, IdLayoutSide side)
    {
        gfx.DrawRectangle(XBrushes.White, x, y, w, h);
        var green = new XSolidBrush(XColor.FromArgb(0, 82, 45));
        if (side == IdLayoutSide.Front)
        {
            gfx.DrawRectangle(green, x, y, w, Mm(25));
        }
        else
        {
            gfx.DrawRectangle(green, x, y + h - Mm(13), w, Mm(13));
        }
    }

    private static void DrawOptionalLines(XGraphics gfx, double x, double y, AppSettingsModel settings, LayoutProfile layout, IdLayoutSide side)
    {
        var pen = new XPen(XColors.Black, Math.Clamp(settings.OutlineThicknessPt, 0.3, 1.5));

        if (side == IdLayoutSide.Front)
        {
            if (settings.PhotoOutlineEnabled)
                DrawPlacementRectangle(gfx, pen, x, y, layout.Get("front_photo"));

            if (settings.QrOutlineEnabled)
                DrawPlacementRectangle(gfx, pen, x, y, layout.Get("front_qr"));

            if (settings.SignatureLineEnabled)
            {
                var p = layout.Get("front_signature");
                var yy = y + Mm(p.YMm + p.HeightMm);
                gfx.DrawLine(pen, x + Mm(p.XMm), yy, x + Mm(p.XMm + p.WidthMm), yy);
            }

            if (settings.EmployeeDividerEnabled)
            {
                foreach (var key in new[] { "front_name_value", "front_designation_value", "front_employee_no_value" })
                {
                    var p = layout.Get(key);
                    var yy = y + Mm(p.YMm + p.HeightMm);
                    gfx.DrawLine(pen, x + Mm(p.XMm), yy, x + Mm(p.XMm + p.WidthMm), yy);
                }
            }
        }
        else if (settings.BackDividerEnabled)
        {
            var address = layout.Get("back_address_value");
            var notice = layout.Get("back_notice_heading");
            gfx.DrawLine(pen, x + Mm(7), y + Mm(address.YMm + address.HeightMm + 1), x + Mm(78), y + Mm(address.YMm + address.HeightMm + 1));
            gfx.DrawLine(pen, x + Mm(7), y + Mm(notice.YMm - 1), x + Mm(78), y + Mm(notice.YMm - 1));
        }
    }

    private static void DrawPlacementRectangle(XGraphics gfx, XPen pen, double x, double y, ElementPlacement p) =>
        gfx.DrawRectangle(pen, x + Mm(p.XMm), y + Mm(p.YMm), Mm(p.WidthMm), Mm(p.HeightMm));

    private static void DrawStyledText(XGraphics gfx, string text, double x, double y, double width, double height, ElementPlacement p)
    {
        var fontStyle = p.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
        var font = new XFont(MapFontFamily(p.FontFamilyKey), p.FontSizePt, fontStyle);

        if (p.ShadowEnabled)
        {
            var shadowColor = ParseColor(p.ShadowColor, p.ShadowOpacity);
            DrawWrappedText(gfx, text, font, new XSolidBrush(shadowColor),
                x + Mm(p.ShadowDxMm), y + Mm(p.ShadowDyMm), width, height, p.Alignment);
        }

        if (p.TextOutlineEnabled)
        {
            var outlineBrush = new XSolidBrush(ParseColor(p.TextOutlineColor));
            var offset = Math.Max(0.15, p.TextOutlineWidthPt);
            foreach (var (dx, dy) in new[]
                     {
                         (-offset, -offset), (0d, -offset), (offset, -offset),
                         (-offset, 0d), (offset, 0d),
                         (-offset, offset), (0d, offset), (offset, offset)
                     })
            {
                DrawWrappedText(gfx, text, font, outlineBrush, x + dx, y + dy, width, height, p.Alignment);
            }
        }

        var mainBrush = new XSolidBrush(ParseColor(p.TextColor));
        var lines = DrawWrappedText(gfx, text, font, mainBrush, x, y, width, height, p.Alignment);

        if (p.UnderlineEnabled && lines.Count > 0)
        {
            var underlinePen = new XPen(ParseColor(p.UnderlineColor), p.UnderlineThicknessPt);
            var first = lines[0];
            var textWidth = gfx.MeasureString(first, font).Width;
            var underlineWidth = p.UnderlineWidthMode == IdUnderlineWidthMode.Element ? width : Math.Min(width, textWidth);
            var startX = p.Alignment switch
            {
                IdTextAlignment.Center => x + (width - underlineWidth) / 2,
                IdTextAlignment.Right => x + width - underlineWidth,
                _ => x
            };
            var lineY = y + Math.Min(height - 1, font.GetHeight() + Mm(p.UnderlineOffsetMm));
            gfx.DrawLine(underlinePen, startX, lineY, startX + underlineWidth, lineY);
        }
    }

    private static List<string> DrawWrappedText(XGraphics gfx, string text, XFont font, XBrush brush,
        double x, double y, double width, double height, IdTextAlignment alignment)
    {
        var lines = Wrap(gfx, text, font, width);
        var lineHeight = font.GetHeight() * 1.08;
        var maxLines = Math.Max(1, (int)Math.Floor(height / Math.Max(1, lineHeight)));
        if (lines.Count > maxLines) lines = lines.Take(maxLines).ToList();

        var format = new XStringFormat
        {
            Alignment = alignment switch
            {
                IdTextAlignment.Center => XStringAlignment.Center,
                IdTextAlignment.Right => XStringAlignment.Far,
                _ => XStringAlignment.Near
            },
            LineAlignment = XLineAlignment.Near
        };

        for (var i = 0; i < lines.Count; i++)
        {
            gfx.DrawString(lines[i], font, brush, new XRect(x, y + i * lineHeight, width, lineHeight), format);
        }
        return lines;
    }

    private static List<string> Wrap(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth || string.IsNullOrEmpty(current))
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = word;
                }
            }
            if (!string.IsNullOrEmpty(current)) result.Add(current);
        }
        return result;
    }

    private static bool TryDrawImage(XGraphics gfx, string? path, double x, double y, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            using var image = XImage.FromFile(path);
            gfx.DrawImage(image, x, y, width, height);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveImage(string key, EmployeeRecord employee, AppSettingsModel settings) => key switch
    {
        "front_logo_1" => settings.Logo1Path,
        "front_logo_2" => settings.Logo2Path,
        "front_photo" => employee.PhotoPath,
        "front_signature" => employee.SignaturePath,
        "front_qr" => employee.QrImagePath,
        "back_captain_signature" => settings.CaptainSignaturePath,
        _ => null
    };

    private static string ResolveText(string key, string fallback, EmployeeRecord employee, AppSettingsModel settings) => key switch
    {
        "front_name_value" => employee.FullName,
        "front_designation_value" => employee.Position,
        "front_employee_no_value" => employee.ControlNumber,
        "back_dob_value" => FormatDate(employee.Birthdate),
        "back_sex_value" => employee.Sex,
        "back_civil_value" => employee.CivilStatus,
        "back_address_value" => employee.Address,
        "back_issuer_value" => settings.IssuerName,
        "back_captain_name" => settings.CaptainName,
        "back_captain_title" => settings.CaptainTitle,
        "back_footer_address" => settings.FooterAddress,
        "back_footer_contact" => settings.FooterContact,
        _ => fallback
    };

    public static string FormatDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "–";
        var formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "MMMM d, yyyy", "MMM d, yyyy" };
        if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

        return "–";
    }

    private static string MapFontFamily(string key) => key.ToLowerInvariant() switch
    {
        "serif" => "Times New Roman",
        "monospace" => "Consolas",
        _ => "Arial"
    };

    private static XColor ParseColor(string? value, double opacity = 1)
    {
        try
        {
            var hex = (value ?? "#000000").Trim().TrimStart('#');
            if (hex.Length != 6) return XColors.Black;
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            var a = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
            return XColor.FromArgb(a, r, g, b);
        }
        catch
        {
            return XColors.Black;
        }
    }

    private static double Mm(double value) => value * PointsPerMm;
}
