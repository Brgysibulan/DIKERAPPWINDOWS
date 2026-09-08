namespace Dikerma.Windows.Services;

// Original RGB and alpha never change. Edits affect only a separate retention mask.
public sealed class EraserSession
{
    public byte[] OriginalPixels { get; }
    public byte[] Mask { get; private set; }
    public int Width { get; }
    public int Height { get; }
    private readonly List<byte[]> _undo = new();
    private readonly Stack<byte[]> _redo = new();
    private int HistoryLimit => Math.Max(1, Math.Min(20, 64_000_000 / Mask.Length));

    public EraserSession(byte[] original, int width, int height)
    {
        if (width < 1 || height < 1 || original.Length != checked(width * height * 4))
            throw new ArgumentException("Invalid image size.");
        OriginalPixels = (byte[])original.Clone(); Width = width; Height = height;
        Mask = Enumerable.Repeat((byte)255, width * height).ToArray();
    }

    public void BeginEdit()
    {
        _undo.Add((byte[])Mask.Clone()); _redo.Clear();
        if (_undo.Count > HistoryLimit) _undo.RemoveAt(0);
    }
    public void ReplaceMask(byte[] mask)
    {
        if (mask.Length != Mask.Length) throw new ArgumentException("Invalid mask size.");
        BeginEdit(); Mask = (byte[])mask.Clone();
    }
    public void Reset() { BeginEdit(); Array.Fill(Mask, (byte)255); }
    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Mask); Mask = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
    }
    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(Mask); Mask = _redo.Pop();
    }
    public void Paint(double cx, double cy, double radius, bool restore, double softness)
    {
        radius = Math.Max(1, radius); softness = Math.Clamp(softness, 0, 1);
        for (var y = Math.Max(0, (int)Math.Floor(cy - radius)); y < Math.Min(Height, Math.Ceiling(cy + radius)); y++)
        for (var x = Math.Max(0, (int)Math.Floor(cx - radius)); x < Math.Min(Width, Math.Ceiling(cx + radius)); x++)
        {
            var distance = Math.Sqrt(Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2)) / radius;
            if (distance >= 1) continue;
            var amount = softness <= 0 ? 1 : Math.Clamp((1 - distance) / softness, 0, 1);
            var i = y * Width + x;
            Mask[i] = (byte)Math.Round(Mask[i] + ((restore ? 255 : 0) - Mask[i]) * amount);
        }
    }
    public byte[] Composite(bool white, bool original = false)
    {
        var result = (byte[])OriginalPixels.Clone();
        for (int n = 0; n < Mask.Length; n++)
        {
            var i = n * 4;
            var alpha = OriginalPixels[i + 3] / 255.0 * (original ? 1 : Mask[n] / 255.0);
            if (white)
                for (int c = 0; c < 3; c++) result[i + c] = (byte)Math.Round(result[i + c] * alpha + 255 * (1 - alpha));
            result[i + 3] = white ? (byte)255 : (byte)Math.Round(alpha * 255);
        }
        return result;
    }
}
