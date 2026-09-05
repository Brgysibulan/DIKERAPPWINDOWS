using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dikerma.Windows.Services;

public sealed class OfflineImageProcessor
{
    private readonly AssetService _assets;

    public OfflineImageProcessor(AssetService assets) => _assets = assets;

    public string CleanPhotoToWhite(string inputPath)
    {
        var bitmap = LoadBgra32(inputPath);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var background = EstimateCornerBackground(pixels, width, height, stride);
        const double threshold = 92;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                var distance = ColorDistance(r, g, b, background.R, background.G, background.B);

                if (distance < threshold)
                {
                    var blend = Math.Clamp((threshold - distance) / 32.0, 0, 1);
                    pixels[i] = (byte)Math.Round(b + (255 - b) * blend);
                    pixels[i + 1] = (byte)Math.Round(g + (255 - g) * blend);
                    pixels[i + 2] = (byte)Math.Round(r + (255 - r) * blend);
                }
                pixels[i + 3] = 255;
            }
        }

        var output = _assets.CreateOutputPath("photos-clean", ".png");
        SavePng(pixels, width, height, stride, bitmap.DpiX, bitmap.DpiY, output);
        return output;
    }

    public string CleanSignatureToTransparent(string inputPath)
    {
        var bitmap = LoadBgra32(inputPath);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var background = EstimateCornerBackground(pixels, width, height, stride);
        const double transparentThreshold = 105;
        const double feather = 45;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                var distance = ColorDistance(r, g, b, background.R, background.G, background.B);

                if (distance <= transparentThreshold)
                {
                    pixels[i + 3] = 0;
                }
                else if (distance < transparentThreshold + feather)
                {
                    pixels[i + 3] = (byte)Math.Round(255 * ((distance - transparentThreshold) / feather));
                }
                else
                {
                    pixels[i + 3] = 255;
                }
            }
        }

        var output = _assets.CreateOutputPath("signatures-clean", ".png");
        SavePng(pixels, width, height, stride, bitmap.DpiX, bitmap.DpiY, output);
        return output;
    }

    public static BitmapSource LoadPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Image file is missing.", path);

        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapSource LoadBgra32(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
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

        if (count == 0) return (255, 255, 255);
        return ((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }

    private static double ColorDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static void SavePng(byte[] pixels, int width, int height, int stride, double dpiX, double dpiY, string output)
    {
        var bitmap = BitmapSource.Create(width, height, dpiX <= 0 ? 96 : dpiX, dpiY <= 0 ? 96 : dpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(output);
        encoder.Save(stream);
    }
}
