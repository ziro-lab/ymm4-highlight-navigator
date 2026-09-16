using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public sealed class VisualExtractor
{
    private float[]? previous;

    // A new extractor is used for EACH disjoint source range. Never compare across an unobserved gap.
    public VideoFeature Extract(ReadOnlySpan<byte> rgb, double timeSeconds)
    {
        const int w = FeatureFormat.Width, h = FeatureFormat.Height;
        if (rgb.Length != w * h * 3 || !double.IsFinite(timeSeconds) || timeSeconds < 0)
            throw new ArgumentException("Expected one complete 64x36 RGB frame and a valid time.");
        var luma = new float[w * h];
        var hist = new float[16]; var grid = new float[16]; var gridDelta = new float[16];
        var descriptor = new byte[32 * 18];
        double sum = 0, squares = 0, chroma = 0, delta = 0;
        int extreme = 0, edges = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int i = y * w + x, p = i * 3;
            float r = rgb[p] / 255f, g = rgb[p + 1] / 255f, b = rgb[p + 2] / 255f;
            float v = Math.Clamp(.299f * r + .587f * g + .114f * b, 0, 1);
            luma[i] = v;
            float d = previous is null ? 0 : Math.Abs(v - previous[i]);
            sum += v; squares += v * v; delta += d;
            chroma += Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            hist[Math.Min(15, (int)(v * 16))]++;
            int cell = (y / 9) * 4 + x / 16;
            grid[cell] += v / 144f; gridDelta[cell] += d / 144f;
            if (v <= .05f || v >= .95f) extreme++;
            if (x > 0 && Math.Abs(v - luma[i - 1]) > .15f) edges++;
            if (y > 0 && Math.Abs(v - luma[i - w]) > .15f) edges++;
        }
        for (int i = 0; i < hist.Length; i++) hist[i] /= w * h;
        for (int y = 0; y < 18; y++) for (int x = 0; x < 32; x++)
        {
            int p = (2 * y) * w + 2 * x;
            descriptor[y * 32 + x] = (byte)Math.Clamp((int)Math.Round(255 * (luma[p] + luma[p + 1] + luma[p + w] + luma[p + w + 1]) / 4), 0, 255);
        }
        previous = luma;
        double mean = sum / (w * h);
        return new(timeSeconds, (float)mean, (float)Math.Sqrt(Math.Max(0, squares / (w * h) - mean * mean)),
            (float)(chroma / (w * h)), (float)(delta / (w * h)),
            edges / (float)(w * (h - 1) + h * (w - 1)), extreme / (float)(w * h),
            hist.ToImmutableArray(), grid.Select(v => Math.Clamp(v, 0, 1)).ToImmutableArray(),
            gridDelta.Select(v => Math.Clamp(v, 0, 1)).ToImmutableArray(), descriptor.ToImmutableArray());
    }
}

public sealed class AudioExtractor
{
    private readonly float[] window = new float[FeatureFormat.AudioWindow];
    private long total;
    private int used, cursor;
    private double squares;
    public AudioFeature? Push(float sample, double rangeStart)
    {
        if (!float.IsFinite(sample) || Math.Abs(sample) > 1.00001f) throw new InvalidDataException("Invalid PCM sample.");
        sample = Math.Clamp(sample, -1, 1);
        if (used == window.Length) squares -= window[cursor] * (double)window[cursor]; else used++;
        window[cursor] = sample; cursor = (cursor + 1) % window.Length;
        squares += sample * (double)sample; total++;
        return total % FeatureFormat.AudioHop == 0 ? Current(rangeStart) : null;
    }
    public AudioFeature? Finish(double rangeStart) => total > 0 && total % FeatureFormat.AudioHop != 0 ? Current(rangeStart) : null;
    private AudioFeature Current(double start) => new(start + total / (double)FeatureFormat.AudioRate,
        (float)Math.Sqrt(Math.Max(0, squares) / used), window.Take(used).Max(x => Math.Abs(x)), used);
}

public static class Salience
{
    // Strictly-lower empirical rank: an entirely constant signal has zero salience, not 100%.
    public static ImmutableArray<float> Rank(IReadOnlyList<float> values)
    {
        if (values.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Nonfinite normalization input.");
        if (values.Count == 0) return [];
        var sorted = values.Order().ToArray();
        var result = ImmutableArray.CreateBuilder<float>(values.Count);
        foreach (float value in values)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi) { int mid = lo + (hi - lo) / 2; if (sorted[mid] < value) lo = mid + 1; else hi = mid; }
            result.Add(sorted.Length <= 1 ? 0 : lo / (float)(sorted.Length - 1));
        }
        return result.MoveToImmutable();
    }
}
