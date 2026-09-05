namespace Dikerma.Windows.Services;

// Flood only colors connected to the outside; preserve similar colors enclosed inside a subject.
public static class BackgroundMask
{
    public static void Apply(byte[] pixels, int width, int height, byte r, byte g, byte b,
        bool white, double tolerance, double feather)
    {
        if (width < 1 || height < 1 || pixels.Length != checked(width * height * 4)) throw new ArgumentException("Invalid image dimensions.");
        tolerance = Math.Clamp(tolerance, 1, 200); feather = Math.Clamp(feather, 1, 100);
        var seen = new bool[width * height]; var queue = new Queue<int>();
        double Distance(int n)
        {
            var i = n * 4;
            return Math.Sqrt(Math.Pow(pixels[i] - b, 2) + Math.Pow(pixels[i + 1] - g, 2) + Math.Pow(pixels[i + 2] - r, 2));
        }
        void Visit(int n)
        {
            if (seen[n]) return;
            seen[n] = true;
            if (Distance(n) <= tolerance + feather || pixels[n * 4 + 3] == 0) queue.Enqueue(n);
        }
        for (int x = 0; x < width; x++) { Visit(x); Visit((height - 1) * width + x); }
        for (int y = 0; y < height; y++) { Visit(y * width); Visit(y * width + width - 1); }
        while (queue.Count > 0)
        {
            var n = queue.Dequeue(); var x = n % width; var y = n / width;
            if (x > 0) Visit(n - 1); if (x + 1 < width) Visit(n + 1);
            if (y > 0) Visit(n - width); if (y + 1 < height) Visit(n + width);
            var i = n * 4; var retain = Math.Clamp((Distance(n) - tolerance) / feather, 0, 1);
            pixels[i + 3] = (byte)Math.Round(pixels[i + 3] * retain);
        }
        if (!white) return;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3] / 255.0;
            for (int c = 0; c < 3; c++) pixels[i + c] = (byte)Math.Round(pixels[i + c] * alpha + 255 * (1 - alpha));
            pixels[i + 3] = 255;
        }
    }
}
