using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public enum FeatureAxis { Luma, Contrast, Chroma, Delta, Edges, Extremes, AudioRms, AudioPeak }
public enum MatchMode { Any, AtLeast, All }
public sealed record Condition(FeatureAxis Axis, float SalienceThreshold, float RawFloor, bool Enabled = true);
public sealed record SceneProfile(string Id, string Name, ImmutableArray<Condition> Conditions, MatchMode Mode = MatchMode.Any,
    int RequiredMatches = 1, double MinimumSeconds = 0, double MergeGapSeconds = 3, double PreRollSeconds = 4, double PostRollSeconds = 2);

// Built once per Index. On/off and sensitivity changes only query these arrays, never the decoder.
public sealed class FeatureTable
{
    public FeaturePack Pack { get; }
    private readonly Dictionary<FeatureAxis, ImmutableArray<float>> raw = [];
    private readonly Dictionary<FeatureAxis, ImmutableArray<float>> ranked = [];
    public FeatureTable(FeaturePack pack)
    {
        pack.Validate(); Pack = pack;
        Add(FeatureAxis.Luma, pack.Video.Select(x => x.Luma));
        Add(FeatureAxis.Contrast, pack.Video.Select(x => x.Contrast));
        Add(FeatureAxis.Chroma, pack.Video.Select(x => x.Chroma));
        Add(FeatureAxis.Delta, pack.Video.Select(x => x.Delta));
        Add(FeatureAxis.Edges, pack.Video.Select(x => x.Edges));
        Add(FeatureAxis.Extremes, pack.Video.Select(x => x.Extremes));
        if (pack.Header.HasAudio)
        {
            var rms = new float[pack.Video.Length]; var peak = new float[pack.Video.Length]; int j = 0;
            for (int i = 0; i < pack.Video.Length; i++)
            {
                double end = Math.Min(pack.Header.EndSeconds, pack.Video[i].TimeSeconds + 1d / pack.Header.VideoFps);
                while (j < pack.Audio.Length && pack.Audio[j].TimeSeconds <= end + 1e-7)
                {
                    rms[i] = Math.Max(rms[i], pack.Audio[j].Rms); peak[i] = Math.Max(peak[i], pack.Audio[j].Peak); j++;
                }
            }
            Add(FeatureAxis.AudioRms, rms); Add(FeatureAxis.AudioPeak, peak);
        }
    }
    private void Add(FeatureAxis axis, IEnumerable<float> values)
    {
        var v = values.ToImmutableArray(); raw[axis] = v; ranked[axis] = Salience.Rank(v);
    }
    public bool Supports(FeatureAxis axis) => raw.ContainsKey(axis);
    public ImmutableArray<float> Raw(FeatureAxis axis) => raw.TryGetValue(axis, out var values) ? values : throw new InvalidDataException("Feature absent; cannot silently substitute zero.");
    public ImmutableArray<float> Rank(FeatureAxis axis) => ranked.TryGetValue(axis, out var values) ? values : throw new InvalidDataException("Feature absent.");
}

public sealed record ProfileEvaluation(string ProfileId, bool Compatible, string? Reason, ImmutableArray<ProfileHit> Hits);
public static class ProfileEvaluator
{
    public static ProfileEvaluation Evaluate(FeatureTable table, SceneProfile profile, double sensitivity = 1)
    {
        if (!double.IsFinite(sensitivity) || sensitivity < .25 || sensitivity > 2) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Conditions.IsDefault || !Enum.IsDefined(profile.Mode)) throw new ArgumentException("Invalid Profile.");
        foreach (double setting in new[] { profile.MinimumSeconds, profile.MergeGapSeconds, profile.PreRollSeconds, profile.PostRollSeconds })
            if (!double.IsFinite(setting) || setting < 0) throw new ArgumentException("Invalid temporal setting.");
        var conditions = profile.Conditions.Where(c => c.Enabled).ToArray();
        if (conditions.Length == 0) return new(profile.Id, true, null, []);
        foreach (var condition in conditions)
        {
            Guard.Unit(condition.SalienceThreshold); Guard.Unit(condition.RawFloor);
            if (!table.Supports(condition.Axis)) return new(profile.Id, false, "必要な特徴がありません: " + condition.Axis, []);
        }
        int required = profile.Mode switch { MatchMode.Any => 1, MatchMode.All => conditions.Length, _ => profile.RequiredMatches };
        if (required < 1 || required > conditions.Length) throw new ArgumentException("Invalid N-of-M condition count.");
        var intervals = new List<TimeRange>(); int start = -1;
        for (int i = 0; i <= table.Pack.Video.Length; i++)
        {
            bool hit = i < table.Pack.Video.Length && conditions.Count(c => table.Raw(c.Axis)[i] >= c.RawFloor / sensitivity && table.Rank(c.Axis)[i] >= c.SalienceThreshold / sensitivity) >= required;
            if (hit && start < 0) start = i;
            if (!hit && start >= 0)
            {
                double from = table.Pack.Video[start].TimeSeconds;
                double to = i == table.Pack.Video.Length ? table.Pack.Header.EndSeconds : table.Pack.Video[i].TimeSeconds;
                if (to - from >= profile.MinimumSeconds) intervals.Add(new(from, to));
                start = -1;
            }
        }
        var merged = TimeRange.Union(intervals, profile.MergeGapSeconds);
        var expanded = merged.Select(r => new TimeRange(Math.Max(table.Pack.Header.StartSeconds, r.Start - profile.PreRollSeconds), Math.Min(table.Pack.Header.EndSeconds, r.End + profile.PostRollSeconds)));
        return new(profile.Id, true, null, TimeRange.Union(expanded).Select(r => new ProfileHit(profile.Id, r)).ToImmutableArray());
    }
}
