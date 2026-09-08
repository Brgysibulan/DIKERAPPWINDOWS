using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dikerma.Windows;
using Dikerma.Windows.Models;
using Dikerma.Windows.Services;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
        var pixels = Enumerable.Repeat((byte)255, 7 * 7 * 4).ToArray();
        // A dark closed ring encloses white clothing; only outside white may disappear.
        for (int y = 1; y < 6; y++) for (int x = 1; x < 6; x++)
            if (x == 1 || x == 5 || y == 1 || y == 5) for (int c = 0; c < 3; c++) pixels[(y * 7 + x) * 4 + c] = 0;
        BackgroundMask.Apply(pixels, 7, 7, 255, 255, 255, false, 75, 20);
        Check(pixels[3] == 0 && pixels[(3 * 7 + 3) * 4 + 3] == 255, "Background cleanup preserves enclosed white clothing");
        var transparent = new byte[] { 0, 0, 0, 0 };
        BackgroundMask.Apply(transparent, 1, 1, 255, 255, 255, true, 75, 20);
        Check(transparent.All(p => p == 255), "Transparent source composites onto white");

        var originalRgba = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
        originalRgba[3] = 0;
        var eraser = new EraserSession(originalRgba, 16, 16);
        eraser.BeginEdit(); eraser.Paint(8, 8, 3, false, 0);
        Check(eraser.Composite(false)[(8 * 16 + 8) * 4 + 3] == 0, "Erase brush removes selected pixels");
        Check(eraser.Composite(false)[(2 * 16 + 2) * 4 + 3] == 255, "Brush preserves pixels outside its radius");
        eraser.Undo();
        Check(eraser.Mask[8 * 16 + 8] == 255, "Undo restores complete brush stroke");
        eraser.Redo();
        Check(eraser.Mask[8 * 16 + 8] == 0, "Redo reapplies brush stroke");
        eraser.BeginEdit(); eraser.Paint(8, 8, 3, true, 0);
        Check(eraser.Mask[8 * 16 + 8] == 255 && eraser.Composite(false)[3] == 0, "Restore recovers original alpha without filling transparent source pixels");
        Check(eraser.OriginalPixels.SequenceEqual(originalRgba), "Original RGBA remains unchanged after erasing and restoring");

        var layout = LayoutCatalog.CreateDefaultProfile();
        Check(layout.SchemaVersion == 3, "Publisher layout schema is v3");
        Check(LayoutCatalog.IsRecordBoundKey("front_photo") && LayoutCatalog.IsRecordBoundKey("back_address_value"), "Personal fields are explicitly record-bound");
        Check(!LayoutCatalog.IsRecordBoundKey("front_id_title"), "Static design text remains template-bound");
        var photoPlacement = layout.Get("front_photo");
        Check(photoPlacement.BorderEnabled && photoPlacement.BorderColor == "#00522D", "Default employee photo frame is part of the master layer");

        var d = new LayoutElementDefinition("custom_test", IdLayoutSide.Front, "Test line", IdLayoutKind.HorizontalLine, 5, 35, 30, 2);
        layout.CustomElements.Add(d); var p = layout.Get(d.Key); p.GroupId = "group"; p.CropLeft = 0.2; p.ZIndex = 8;
        p.FillColor = "#00FF00"; p.BorderEnabled = true; p.BorderColor = "#FF0000"; p.BorderThicknessPt = 1; p.CornerRadiusMm = 2; p.Opacity = 0.8;
        layout = JsonSerializer.Deserialize<LayoutProfile>(JsonSerializer.Serialize(layout))!;
        Check(layout.ForSide(IdLayoutSide.Front).Any(e => e.Key == d.Key) && layout.Get(d.Key).GroupId == "group", "Custom layers and groups survive save/reload");
        Check(layout.Get(d.Key).BorderEnabled && layout.Get(d.Key).BorderColor == "#FF0000", "Publisher appearance survives save/reload");
        var old = JsonSerializer.Deserialize<LayoutProfile>("{\"SchemaVersion\":1,\"Elements\":{}}")!;
        Check(old.ForSide(IdLayoutSide.Front).Count() > 0 && old.CustomElements.Count == 0, "Legacy layouts load with default fields");
        p.CropLeft = 0.9; p.CropRight = 0.9; p.BorderThicknessPt = 99; p.Opacity = 2; p.Clamp();
        Check(p.CropLeft + p.CropRight <= 0.950001, "Crop retains a positive image area");
        Check(p.BorderThicknessPt <= 8 && p.Opacity <= 1, "Publisher frame and opacity values are clamped safely");

        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        Check(window.Icon is not null, "Main window initializes with D application icon");
        foreach (var kind in Enum.GetValues<IdLayoutKind>())
        {
            var def = d with { Kind = kind };
            var placement = new ElementPlacement
            {
                WidthMm = 30, HeightMm = 15, FontFamilyKey = "Arial", ShadowEnabled = true,
                TextOutlineEnabled = true, UnderlineEnabled = true, Italic = true,
                FillColor = "#00AA55", BorderEnabled = true, BorderColor = "#00522D", BorderThicknessPt = 0.8, CornerRadiusMm = 1.5
            };
            using var stream = ElementRenderer.Png(def, placement, "SIBULAN", null);
            var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(bitmap.PixelWidth == 355 && bitmap.PixelHeight == 178, kind + " renders at 300 dpi");
        }

        var frameDef = new LayoutElementDefinition("frame_test", IdLayoutSide.Front, "Frame", IdLayoutKind.Image, 0, 0, 10, 10);
        var framePlacement = new ElementPlacement { WidthMm = 10, HeightMm = 10, BorderEnabled = true, BorderColor = "#FF0000", BorderThicknessPt = 1.5, CornerRadiusMm = 1 };
        using (var framed = ElementRenderer.Png(frameDef, framePlacement, "", null))
        {
            var frameBitmap = BitmapDecoder.Create(framed, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var frameBytes = new byte[frameBitmap.PixelWidth * frameBitmap.PixelHeight * 4];
            frameBitmap.CopyPixels(frameBytes, frameBitmap.PixelWidth * 4, 0);
            Check(frameBytes.Any(b => b != 0), "Picture frame renders even when image content is empty");
        }

        var folder = Path.Combine(Path.GetTempPath(), "dikerma-smoke-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var imagePath = Path.Combine(folder, "crop.png");
            var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(imagePath)) encoder.Save(file);
            var processor = new OfflineImageProcessor(new AssetService());
            var edit = processor.CreateEraserSession(imagePath);
            var eraserWindow = new BackgroundEraserWindow(imagePath, processor);
            Check(eraserWindow.Content is not null, "Advanced eraser window initializes");
            var mask = processor.CreateMask(edit, 75, 3);
            Check(mask.Length == 2, "Adaptive mask supports small images");
            edit.ReplaceMask(new byte[] { 0, 255 });
            var saved = processor.SaveEraser(edit, false);
            try
            {
                var savedBitmap = OfflineImageProcessor.LoadPreview(saved);
                var savedBytes = new byte[8]; savedBitmap.CopyPixels(savedBytes, 8, 0);
                Check(savedBytes[3] == 0 && savedBytes[7] == 255, "Saved transparent PNG preserves edited alpha");
            }
            finally { File.Delete(saved); }
            var crop = new ElementPlacement { WidthMm = 10, HeightMm = 10, CropLeft = 0.5 };
            using var rendered = ElementRenderer.Png(d with { Kind = IdLayoutKind.Image }, crop, "", imagePath);
            var frame = BitmapDecoder.Create(rendered, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var bytes = new byte[frame.PixelWidth * frame.PixelHeight * 4]; frame.CopyPixels(bytes, frame.PixelWidth * 4, 0);
            var center = ((frame.PixelHeight / 2) * frame.PixelWidth + frame.PixelWidth / 2) * 4;
            Check(bytes[center] > 240 && bytes[center + 2] < 15, "Image crop removes red half and retains blue half");

            // Deliberately place a stale image path on the master photo layer. PDF rendering must ignore it
            // for record-bound photos and resolve each card from its own EmployeeRecord instead.
            layout.Get("front_photo").ImagePath = imagePath;
            var output = Path.Combine(folder, "ids.pdf");
            new PdfExportService().Export(output,
                new EmployeeRecord { FullName = "PERSON ONE", PhotoPath = null },
                new EmployeeRecord { FullName = "PERSON TWO", PhotoPath = null },
                new AppSettingsModel(), layout);
            Check(new FileInfo(output).Length > 1000, "Two-person A4 PDF exports with protected record-bound layers");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("All Windows smoke checks passed.");
    }
}
