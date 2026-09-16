using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public sealed record TransitionSignature(ImmutableArray<float> Visual, float? BeforeAudio, float? AfterAudio, float? PeakAudio)
{
    public const int Dimensions = 32;
    public void Validate()
    {
        if (Visual.IsDefault || Visual.Length != Dimensions || Visual.Any(x => !float.IsFinite(x) || x < -1.00001f || x > 1.00001f))
            throw new InvalidDataException("場面切替の特徴形式が対応していません。");
        if ((BeforeAudio.HasValue != AfterAudio.HasValue) || (BeforeAudio.HasValue != PeakAudio.HasValue)) throw new InvalidDataException("音声特徴が部分的に欠けています。");
        foreach (float? value in new[] { BeforeAudio, AfterAudio, PeakAudio }) if (value.HasValue) Guard.Unit(value.Value);
    }
}
public sealed record TransitionCandidate(double CenterSeconds, TimeRange Before, TimeRange Transition, TimeRange After,
    bool FullContext, float Strength, TransitionSignature Signature);

/// <summary>Local before/after statistics: training clips and long recordings use exactly the same clock and normalization.</summary>
public sealed class TransitionIndex
{
    public const string Algorithm = "local-transition-v1";
    public FeatureHeader Header { get; }
    public ImmutableArray<TransitionCandidate> Candidates { get; }
    private TransitionIndex(FeatureHeader header, ImmutableArray<TransitionCandidate> candidates) { Header = header; Candidates = candidates; }
    public static TransitionIndex Build(FeaturePack pack, CancellationToken token = default)
    {
        pack.Validate();
        var video = pack.Video; int n = video.Length, fps = pack.Header.VideoFps;
        int radius = Math.Max(1, 2 * fps);
        // Prefix sums keep long-recording cost linear rather than scanning the whole clip for every point.
        const int axes = 39; // 7 scalars + histogram16 + grid16
        var sums = new double[axes][];
        for (int j = 0; j < axes; j++) sums[j] = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            token.ThrowIfCancellationRequested(); var v = video[i];
            float[] scalars = [v.Delta, v.Luma, v.Contrast, v.Chroma, v.Edges, v.Extremes, v.GridDelta.Average()];
            for (int j = 0; j < 7; j++) sums[j][i + 1] = sums[j][i] + scalars[j];
            for (int j = 0; j < 16; j++) { sums[j + 7][i + 1] = sums[j + 7][i] + v.Histogram[j]; sums[j + 23][i + 1] = sums[j + 23][i] + v.GridLuma[j]; }
        }
        double Mean(int axis, int from, int to) => (sums[axis][to] - sums[axis][from]) / (to - from);
        var audioRms = new float[n]; var audioPeak = new float[n]; int audioIndex = 0;
        if (pack.Header.HasAudio)
        {
            for (int i = 0; i < n; i++)
            {
                double end = Math.Min(pack.Header.EndSeconds, video[i].TimeSeconds + 1d / fps);
                while (audioIndex < pack.Audio.Length && pack.Audio[audioIndex].TimeSeconds <= end + 1e-7)
                { audioRms[i] = Math.Max(audioRms[i], pack.Audio[audioIndex].Rms); audioPeak[i] = Math.Max(audioPeak[i], pack.Audio[audioIndex].Peak); audioIndex++; }
            }
        }
        var proposals = new List<TransitionCandidate>();
        // i=0 has no observed before-state and MUST NOT manufacture a transition at the cut boundary.
        for (int i = 1; i < n - 1; i++)
        {
            token.ThrowIfCancellationRequested();
            int b = Math.Max(0, i - radius), a = Math.Min(n, i + radius);
            var x = new float[TransitionSignature.Dimensions];
            x[0] = (float)Mean(0, b, i); x[1] = (float)Mean(0, i, a); x[2] = video[i].Delta;
            for (int j = 1; j <= 5; j++) x[j + 2] = (float)(Mean(j, i, a) - Mean(j, b, i));
            double hist = 0;
            for (int j = 0; j < 16; j++)
            {
                hist += Math.Abs(Mean(j + 7, i, a) - Mean(j + 7, b, i)) / 2;
                x[10 + j] = (float)(Mean(j + 23, i, a) - Mean(j + 23, b, i));
            }
            x[8] = (float)Math.Clamp(hist, 0, 1); x[9] = x.Skip(10).Take(16).Count(v => Math.Abs(v) >= .05) / 16f;
            // Six aligned time bins retain the shape, not just a whole-clip average.
            for (int k = 0; k < 6; k++)
            {
                int begin = Math.Clamp(b + (a - b) * k / 6, b, a - 1);
                int end = Math.Clamp(b + (a - b) * (k + 1) / 6, begin + 1, a);
                x[26 + k] = (float)Mean(0, begin, end);
            }
            float immediateHistogram = 0;
            for (int j = 0; j < 16; j++) immediateHistogram += Math.Abs(video[i].Histogram[j] - video[i - 1].Histogram[j]) / 2;
            float strength = Math.Clamp(Math.Max(video[i].Delta, Math.Max(immediateHistogram,
                Math.Max(Math.Abs(x[3]), Math.Max(0, x[1] - x[0])))), 0, 1);
            // Low proposal floor; never use a top-K-per-clip limit that silently caps long-video recall.
            if (strength < .02f) continue;
            float? beforeAudio = null, afterAudio = null, peakAudio = null;
            if (pack.Header.HasAudio)
            {
                beforeAudio = audioRms.Skip(b).Take(i - b).Average(); afterAudio = audioRms.Skip(i).Take(a - i).Average();
                peakAudio = audioPeak.Skip(b).Take(a - b).Max();
            }
            double t = video[i].TimeSeconds, transitionEnd = Math.Min(pack.Header.EndSeconds, t + 1d / fps);
            var signature = new TransitionSignature(x.ToImmutableArray(), beforeAudio, afterAudio, peakAudio); signature.Validate();
            proposals.Add(new(t, new(video[b].TimeSeconds, t), new(t, transitionEnd), new(transitionEnd, Math.Min(pack.Header.EndSeconds, video[a - 1].TimeSeconds + 1d / fps)),
                i - b >= radius && a - i >= radius, strength, signature));
        }
        // Stable, sensitivity-independent local maximum suppression. Indexing is done once; slider queries only match signatures.
        var occupied = new HashSet<long>(); var chosen = new List<TransitionCandidate>();
        foreach (var p in proposals.OrderByDescending(p => p.Strength).ThenBy(p => p.CenterSeconds))
        {
            long bin = (long)Math.Floor(p.CenterSeconds * fps);
            int spacing = Math.Max(1, fps / 2);
            if (Enumerable.Range(-spacing + 1, spacing * 2 - 1).Any(d => occupied.Contains(bin + d))) continue;
            occupied.Add(bin); chosen.Add(p);
        }
        return new(pack.Header, chosen.OrderBy(p => p.CenterSeconds).ToImmutableArray());
    }
}
