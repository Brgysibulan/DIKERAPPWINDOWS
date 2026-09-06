using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dikerma.Windows.Services;

public sealed class OfflineImageProcessor
{
    private readonly AssetService _assets;

    public OfflineImageProcessor(AssetService assets) => _assets = assets;

    public string CleanBackground(string inputPath, bool white, double tolerance, double feather)
    {
        var session = CreateEraserSession(inputPath);
        session.ReplaceMask(CreateMask(session, tolerance, feather));
        return SaveEraser(session, white);
    }

    public EraserSession CreateEraserSession(string path)
    {
        var bitmap = LoadBgra32(path);
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 16_000_000)
            throw new InvalidOperationException("Please resize this image to 16 megapixels or less before editing.");
        var pixels = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return new EraserSession(pixels, bitmap.PixelWidth, bitmap.PixelHeight);
    }

    public byte[] CreateMask(EraserSession session, double tolerance, double feather) =>
        BuildAdaptiveBackgroundMatte(session.OriginalPixels, session.Width, session.Height,
            session.Width * 4, strength: tolerance / 75.0, featherRadius: (int)Math.Round(feather));

    public string SaveEraser(EraserSession session, bool white)
    {
        var output = _assets.CreateOutputPath("eraser", ".png");
        SavePng(session.Composite(white), session.Width, session.Height, session.Width * 4, 96, 96, output);
        return output;
    }

    public string CleanPhotoToWhite(string inputPath)
    {
        var bitmap = LoadBgra32(inputPath);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var matte = BuildAdaptiveBackgroundMatte(pixels, width, height, stride);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = y * width + x;
                var i = y * stride + x * 4;
                var keep = matte[pixelIndex] / 255.0 * pixels[i + 3] / 255.0;

                // Composite the retained foreground over clean white. The adaptive matte
                // keeps hair/clothing edges while removing only edge-connected background.
                pixels[i] = CompositeOverWhite(pixels[i], keep);
                pixels[i + 1] = CompositeOverWhite(pixels[i + 1], keep);
                pixels[i + 2] = CompositeOverWhite(pixels[i + 2], keep);
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

        var matte = BuildAdaptiveBackgroundMatte(pixels, width, height, stride, signatureMode: true);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = y * width + x;
                var i = y * stride + x * 4;
                pixels[i + 3] = (byte)(pixels[i + 3] * matte[pixelIndex] / 255);
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

    /// <summary>
    /// Builds a foreground matte (0 = background, 255 = foreground) without internet,
    /// cloud APIs, or ML packages. The remover learns a robust background color from
    /// the image border, measures border variation, then flood-fills only background
    /// that is connected to an outer edge. This protects similarly colored clothing,
    /// skin, hair, and objects inside the portrait much better than a global threshold.
    /// </summary>
    private static byte[] BuildAdaptiveBackgroundMatte(
        byte[] pixels,
        int width,
        int height,
        int stride,
        bool signatureMode = false,
        double strength = 1,
        int featherRadius = 3)
    {
        if (width <= 0 || height <= 0)
            return Array.Empty<byte>();

        var model = EstimateBorderBackground(pixels, width, height, stride);
        var strongThreshold = signatureMode
            ? Math.Clamp(model.Variation * 2.6 + 22.0, 30.0, 105.0)
            : Math.Clamp(model.Variation * 2.15 + 18.0, 26.0, 88.0);
        var weakThreshold = signatureMode
            ? Math.Clamp(strongThreshold + 58.0, 80.0, 165.0)
            : Math.Clamp(strongThreshold + 44.0, 62.0, 138.0);
        var localStepThreshold = signatureMode ? 82.0 : 70.0;
        strength = Math.Clamp(strength, 0.25, 2);
        strongThreshold *= strength;
        weakThreshold *= strength;
        localStepThreshold *= strength;

        var count = width * height;
        var background = new bool[count];
        var queue = new int[count];
        var head = 0;
        var tail = 0;

        void TrySeed(int x, int y)
        {
            var index = y * width + x;
            if (background[index]) return;
            if (DistanceAt(pixels, x, y, stride, model.R, model.G, model.B) > strongThreshold) return;
            background[index] = true;
            queue[tail++] = index;
        }

        // Seed the flood fill from the complete image perimeter, not just four corners.
        for (var x = 0; x < width; x++)
        {
            TrySeed(x, 0);
            if (height > 1) TrySeed(x, height - 1);
        }
        for (var y = 1; y < height - 1; y++)
        {
            TrySeed(0, y);
            if (width > 1) TrySeed(width - 1, y);
        }

        while (head < tail)
        {
            var current = queue[head++];
            var x = current % width;
            var y = current / width;

            TryNeighbor(x - 1, y, x, y);
            TryNeighbor(x + 1, y, x, y);
            TryNeighbor(x, y - 1, x, y);
            TryNeighbor(x, y + 1, x, y);
        }

        void TryNeighbor(int nx, int ny, int px, int py)
        {
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
            var index = ny * width + nx;
            if (background[index]) return;

            var modelDistance = DistanceAt(pixels, nx, ny, stride, model.R, model.G, model.B);
            if (modelDistance > weakThreshold) return;

            var localDistance = PixelDistance(pixels, nx, ny, px, py, stride);
            if (modelDistance > strongThreshold && localDistance > localStepThreshold) return;

            background[index] = true;
            queue[tail++] = index;
        }

        var matte = new byte[count];
        Array.Fill(matte, (byte)255);
        for (var i = 0; i < count; i++)
        {
            if (background[i]) matte[i] = 0;
        }

        // Feather only the immediate foreground boundary. Interior pixels remain fully
        // opaque so faces, text on shirts, and other details do not become washed out.
        var radius = signatureMode ? 2 : Math.Clamp(featherRadius, 0, 5);
        var feathered = (byte[])matte.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (background[index]) continue;

                var nearestBackground = radius + 1;
                for (var oy = -radius; oy <= radius; oy++)
                {
                    var yy = y + oy;
                    if (yy < 0 || yy >= height) continue;
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var xx = x + ox;
                        if (xx < 0 || xx >= width) continue;
                        if (!background[yy * width + xx]) continue;
                        var distance = Math.Abs(ox) + Math.Abs(oy);
                        if (distance < nearestBackground) nearestBackground = distance;
                    }
                }

                if (nearestBackground > radius) continue;

                var colorDistance = DistanceAt(pixels, x, y, stride, model.R, model.G, model.B);
                var colorKeep = SmoothStep(strongThreshold * 0.72, weakThreshold, colorDistance);
                var spatialKeep = Math.Clamp(nearestBackground / (double)(radius + 1), 0.18, 1.0);
                var keep = Math.Max(colorKeep, spatialKeep);
                feathered[index] = (byte)Math.Round(255 * Math.Clamp(keep, 0, 1));
            }
        }

        // Remove isolated one-pixel background noise left inside an otherwise clean
        // background region and gently restore isolated foreground specks near edges.
        return CleanupMatte(feathered, width, height);
    }

    private static byte[] CleanupMatte(byte[] matte, int width, int height)
    {
        if (width < 3 || height < 3) return matte;
        var cleaned = (byte[])matte.Clone();

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var index = y * width + x;
                var transparentNeighbors = 0;
                var opaqueNeighbors = 0;

                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        var value = matte[(y + oy) * width + (x + ox)];
                        if (value <= 24) transparentNeighbors++;
                        if (value >= 232) opaqueNeighbors++;
                    }
                }

                if (matte[index] >= 232 && transparentNeighbors >= 7)
                    cleaned[index] = 0;
                else if (matte[index] <= 24 && opaqueNeighbors >= 7)
                    cleaned[index] = 255;
            }
        }

        return cleaned;
    }

    private static (byte R, byte G, byte B, double Variation) EstimateBorderBackground(
        byte[] pixels,
        int width,
        int height,
        int stride)
    {
        var samples = new List<(byte R, byte G, byte B)>();
        var step = Math.Max(1, Math.Min(width, height) / 180);
        var band = Math.Max(1, Math.Min(6, Math.Min(width, height) / 40));

        for (var y = 0; y < height; y += step)
        {
            for (var xBand = 0; xBand < band; xBand++)
            {
                AddSample(xBand, y);
                if (width - 1 - xBand != xBand) AddSample(width - 1 - xBand, y);
            }
        }

        for (var x = 0; x < width; x += step)
        {
            for (var yBand = 0; yBand < band; yBand++)
            {
                AddSample(x, yBand);
                if (height - 1 - yBand != yBand) AddSample(x, height - 1 - yBand);
            }
        }

        void AddSample(int x, int y)
        {
            var i = y * stride + x * 4;
            samples.Add((pixels[i + 2], pixels[i + 1], pixels[i]));
        }

        if (samples.Count == 0) return (255, 255, 255, 0);

        var rs = samples.Select(s => s.R).OrderBy(v => v).ToArray();
        var gs = samples.Select(s => s.G).OrderBy(v => v).ToArray();
        var bs = samples.Select(s => s.B).OrderBy(v => v).ToArray();
        var r = rs[rs.Length / 2];
        var g = gs[gs.Length / 2];
        var b = bs[bs.Length / 2];

        var distances = samples
            .Select(s => ColorDistance(s.R, s.G, s.B, r, g, b))
            .OrderBy(v => v)
            .ToArray();

        // 75th percentile ignores a person/object touching part of the border while
        // still adapting to shadows and uneven lighting on a plain backdrop.
        var variationIndex = Math.Clamp((int)Math.Round((distances.Length - 1) * 0.75), 0, distances.Length - 1);
        var variation = distances[variationIndex];
        return (r, g, b, variation);
    }

    private static double DistanceAt(byte[] pixels, int x, int y, int stride, byte r, byte g, byte b)
    {
        var i = y * stride + x * 4;
        return ColorDistance(pixels[i + 2], pixels[i + 1], pixels[i], r, g, b);
    }

    private static double PixelDistance(byte[] pixels, int x1, int y1, int x2, int y2, int stride)
    {
        var i1 = y1 * stride + x1 * 4;
        var i2 = y2 * stride + x2 * 4;
        return ColorDistance(
            pixels[i1 + 2], pixels[i1 + 1], pixels[i1],
            pixels[i2 + 2], pixels[i2 + 1], pixels[i2]);
    }

    private static byte CompositeOverWhite(byte channel, double keep)
        => (byte)Math.Round(channel * keep + 255 * (1.0 - keep));

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (edge1 <= edge0) return value >= edge1 ? 1 : 0;
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double ColorDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        // Weighted RGB distance gives green differences slightly more importance,
        // which improves separation on common green/blue/plain-color photo backdrops.
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return Math.Sqrt(dr * dr * 0.30 + dg * dg * 0.59 + db * db * 0.11);
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
