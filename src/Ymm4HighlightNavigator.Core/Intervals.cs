using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public readonly record struct TimeRange
{
    public double Start { get; }
    public double End { get; }
    public TimeRange(double start, double end) { Guard.Range(start, end); Start = start; End = end; }
    public TimeRange? Intersect(TimeRange other)
    {
        double start = Math.Max(Start, other.Start), end = Math.Min(End, other.End);
        return end > start ? new TimeRange(start, end) : null;
    }
    public static ImmutableArray<TimeRange> Union(IEnumerable<TimeRange> input, double gap = 0)
    {
        if (!double.IsFinite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
        var result = ImmutableArray.CreateBuilder<TimeRange>();
        foreach (var range in input.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            Guard.Range(range.Start, range.End);
            if (result.Count > 0 && range.Start <= result[^1].End + gap)
                result[^1] = new(result[^1].Start, Math.Max(result[^1].End, range.End));
            else result.Add(range);
        }
        return result.ToImmutable();
    }
}

// Detached immutable data; no YMM4 object, reflection handle or external file is retained in Core.
public sealed record TargetSnapshot(Guid Id, string SourceKey, int StartFrame, int LengthFrames, int Fps, double OffsetSeconds, double RatePercent)
{
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(SourceKey) || StartFrame < 0 || LengthFrames <= 0 || Fps <= 0 ||
            !double.IsFinite(OffsetSeconds) || OffsetSeconds < 0 || !double.IsFinite(RatePercent) || RatePercent <= 0 || (long)StartFrame + LengthFrames > int.MaxValue)
            throw new ArgumentException("Unsupported target snapshot.");
        if (!double.IsFinite(OffsetSeconds + LengthFrames / (double)Fps * RatePercent / 100))
            throw new ArgumentException("Source range overflow.");
    }
    public TimeRange SourceRange
    {
        get { Validate(); return new(OffsetSeconds, OffsetSeconds + LengthFrames / (double)Fps * RatePercent / 100); }
    }
    public double SourceTimeAt(int frame)
    {
        Validate();
        if (frame < StartFrame || frame >= (long)StartFrame + LengthFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        return OffsetSeconds + (frame - StartFrame) / (double)Fps * RatePercent / 100;
    }
    // Product rounding policy: first representable frame at/after the interval start, never the exclusive end.
    public int? FirstFrame(TimeRange source)
    {
        var clipped = SourceRange.Intersect(source);
        if (clipped is null) return null;
        double local = (clipped.Value.Start - OffsetSeconds) * 100 / RatePercent * Fps;
        int index = checked((int)Math.Ceiling(local - 1e-9));
        index = Math.Max(0, index);
        if (index >= LengthFrames) return null;
        int frame = checked(StartFrame + index);
        return SourceTimeAt(frame) < clipped.Value.End ? frame : null;
    }
}

public sealed record ProfileHit(string ProfileId, TimeRange Range);
public sealed record ReviewEpisode(TimeRange Range, ImmutableArray<ProfileHit> Hits)
{
    public ImmutableArray<string> ProfileIds => Hits.Select(x => x.ProfileId).Distinct(StringComparer.Ordinal).Order().ToImmutableArray();
}
public sealed record ReviewQueue(int HitTotal, ImmutableArray<ReviewEpisode> Episodes);
public static class EpisodeUnion
{
    public static ReviewQueue Build(IEnumerable<ProfileHit> source, double mergeGapSeconds = 0)
    {
        if (!double.IsFinite(mergeGapSeconds) || mergeGapSeconds < 0) throw new ArgumentOutOfRangeException(nameof(mergeGapSeconds));
        var hits = source.ToArray();
        if (hits.Any(h => string.IsNullOrWhiteSpace(h.ProfileId))) throw new ArgumentException("Profile identity is required.");
        var episodes = ImmutableArray.CreateBuilder<ReviewEpisode>();
        foreach (var hit in hits.OrderBy(h => h.Range.Start).ThenBy(h => h.Range.End).ThenBy(h => h.ProfileId, StringComparer.Ordinal))
        {
            Guard.Range(hit.Range.Start, hit.Range.End);
            if (episodes.Count > 0 && hit.Range.Start <= episodes[^1].Range.End + mergeGapSeconds)
            {
                var previous = episodes[^1];
                episodes[^1] = new(new(previous.Range.Start, Math.Max(previous.Range.End, hit.Range.End)), previous.Hits.Add(hit));
            }
            else episodes.Add(new(hit.Range, [hit]));
        }
        return new(hits.Length, episodes.ToImmutable());
    }
}
