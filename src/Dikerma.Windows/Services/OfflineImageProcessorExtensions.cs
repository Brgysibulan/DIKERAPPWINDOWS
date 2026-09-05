using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dikerma.Windows.Services;

/// <summary>
/// Compatibility layer for Layout Studio image cleanup. The record-photo workflow
/// continues to use OfflineImageProcessor's adaptive professional remover, while
/// Studio layers get adjustable tolerance/feather controls without replacing it.
/// </summary>
public static class OfflineImageProcessorExtensions
{
    public static string CleanBackground(this OfflineImageProcessor processor, string inputPath, bool white, double tolerance, double feather)
    {
        _ = processor;
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            throw new FileNotFoundException("Image file is missing.", inputPath);

        using var stream = File.OpenRead(inputPath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[checked(stride * height)];
        bitmap.CopyPixels(pixels, stride, 0);

        var background = EstimateCornerBackground(pixels, width, height, stride);
        BackgroundMask.Apply(
            pixels,
            width,
            height,
            background.R,
            background.G,
            background.B,
            white,
            Math.Clamp(tolerance, 10, 160),
            Math.Clamp(feather, 1, 60));

        var output = new AssetService().CreateOutputPath("studio-background-clean", ".png");
        var result = BitmapSource.Create(
            width,
            height,
            bitmap.DpiX <= 0 ? 96 : bitmap.DpiX,
            bitmap.DpiY <= 0 ? 96 : bitmap.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(result));
        using var outputStream = File.Create(output);
        encoder.Save(outputStream);
        return output;
    }

    private static (byte R, byte G, byte B) EstimateCornerBackground(byte[] pixels, int width, int height, int stride)
    {
        var patch = Math.Max(2, Math.Min(18, Math.Min(width, height) / 12));
        long r = 0, g = 0, b = 0, count = 0;
        var origins = new[]
        {
            (0, 0),
            (Math.Max(0, width - patch), 0),
            (0, Math.Max(0, height - patch)),
            (Math.Max(0, width - patch), Math.Max(0, height - patch))
        };

        foreach (var (ox, oy) in origins)
        {
            for (var y = oy; y < Math.Min(height, oy + patch); y++)
            {
                for (var x = ox; x < Math.Min(width, ox + patch); x++)
                {
                    var i = y * stride + x * 4;
                    b += pixels[i];
                    g += pixels[i + 1];
                    r += pixels[i + 2];
                    count++;
                }
            }
        }

        return count == 0
            ? ((byte)255, (byte)255, (byte)255)
            : ((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }
}
