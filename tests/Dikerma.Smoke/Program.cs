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
        var d = new LayoutElementDefinition("custom_test", IdLayoutSide.Front, "Test line", IdLayoutKind.HorizontalLine, 5, 35, 30, 2);
        layout.CustomElements.Add(d); var p = layout.Get(d.Key); p.GroupId = "group"; p.CropLeft = 0.2; p.ZIndex = 8;
        layout = JsonSerializer.Deserialize<LayoutProfile>(JsonSerializer.Serialize(layout))!;
        Check(layout.ForSide(IdLayoutSide.Front).Any(e => e.Key == d.Key) && layout.Get(d.Key).GroupId == "group", "Custom layers and groups survive save/reload");
        var old = JsonSerializer.Deserialize<LayoutProfile>("{\"SchemaVersion\":1,\"Elements\":{}}")!;
        Check(old.ForSide(IdLayoutSide.Front).Count() > 0 && old.CustomElements.Count == 0, "Legacy layouts load with default fields");
        p.CropLeft = 0.9; p.CropRight = 0.9; p.Clamp();
        Check(p.CropLeft + p.CropRight <= 0.950001, "Crop retains a positive image area");

        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        Check(window.Icon is not null, "Main window initializes with D application icon");
        foreach (var kind in Enum.GetValues<IdLayoutKind>())
        {
            var def = d with { Kind = kind };
            var placement = new ElementPlacement { WidthMm = 30, HeightMm = 15, FontFamilyKey = "Arial", ShadowEnabled = true, TextOutlineEnabled = true, UnderlineEnabled = true, Italic = true };
            using var stream = ElementRenderer.Png(def, placement, "SIBULAN", null);
            var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(bitmap.PixelWidth == 355 && bitmap.PixelHeight == 178, kind + " renders at 300 dpi");
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
            var output = Path.Combine(folder, "ids.pdf");
            new PdfExportService().Export(output, new EmployeeRecord { FullName = "PERSON ONE" }, new EmployeeRecord { FullName = "PERSON TWO" }, new AppSettingsModel(), layout);
            Check(new FileInfo(output).Length > 1000, "Two-person A4 PDF exports with custom layers");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("All Windows smoke checks passed.");
    }
}
