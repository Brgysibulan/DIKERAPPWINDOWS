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

        foreach (var definition in layout.ForSide(side))
        {
            var placement = layout.Get(definition.Key);
            if (!placement.Visible) continue;

            var ex = x + Mm(placement.XMm);
            var ey = y + Mm(placement.YMm);
            var ew = Mm(placement.WidthMm);
            var eh = Mm(placement.HeightMm);

            using var png = ElementRenderer.Png(definition, placement,
                ResolveText(placement.BindingKey ?? definition.Key, definition.SampleText, employee, settings),
                placement.ImagePath ?? ResolveImage(placement.BindingKey ?? definition.Key, employee, settings));
            using var rendered = XImage.FromStream(png);
            gfx.DrawImage(rendered, ex, ey, ew, eh);
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

    private static double Mm(double value) => value * PointsPerMm;
}
