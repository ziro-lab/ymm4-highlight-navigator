using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public enum GenericFilterKind
{
    LargeSceneChange,
    DarkFade,
    QuietToActivity
}

public sealed record GenericFilter(string Id, string Name, GenericFilterKind Kind,
    double PreRollSeconds = 2, double PostRollSeconds = 2)
{
    public GenericFilter Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("generic.", StringComparison.Ordinal)
            || Id.Length > 160 || Id.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-'))
            || string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || Name.Any(char.IsControl)
            || !Enum.IsDefined(Kind)
            || !double.IsFinite(PreRollSeconds) || !double.IsFinite(PostRollSeconds)
            || PreRollSeconds < 0 || PostRollSeconds <= 0 || PreRollSeconds > 30 || PostRollSeconds > 30)
            throw new ArgumentException("汎用フィルターの定義が不正です。");
        return this with { Name = Name.Trim().Normalize(System.Text.NormalizationForm.FormC) };
    }
}

public static class GenericFilterCatalog
{
    public static GenericFilter LargeSceneChange { get; } =
        new GenericFilter("generic.scene-change", "大きな場面切替", GenericFilterKind.LargeSceneChange, 2, 2).Normalize();
    public static GenericFilter DarkFade { get; } =
        new GenericFilter("generic.dark-fade", "暗転 / フェード", GenericFilterKind.DarkFade, 3, 2).Normalize();
    public static GenericFilter QuietToActivity { get; } =
        new GenericFilter("generic.quiet-to-active", "静穏 → 高活動", GenericFilterKind.QuietToActivity, 3, 2).Normalize();

    public static ImmutableArray<GenericFilter> Basic { get; } =
        [LargeSceneChange, DarkFade, QuietToActivity];
}

public static class GenericFilterEvaluator
{
    private const double Epsilon = 1e-7;

    public static ProfileEvaluation Evaluate(FeatureTable table, TransitionIndex index, GenericFilter filter,
        double sensitivity = 1, CancellationToken token = default)
    {
        if (!double.IsFinite(sensitivity) || sensitivity < .25 || sensitivity > 2)
            throw new ArgumentOutOfRangeException(nameof(sensitivity));
        var rule = filter.Normalize();
        if (table.Pack.Header != index.Header)
            throw new ArgumentException("特徴表と場面切替Indexの対象が一致しません。");

        var hits = ImmutableArray.CreateBuilder<ProfileHit>();
        foreach (var candidate in index.Candidates)
        {
            token.ThrowIfCancellationRequested();
            if (!Matches(table, candidate, rule.Kind, sensitivity)) continue;
            double start = Math.Max(index.Header.StartSeconds, candidate.CenterSeconds - rule.PreRollSeconds);
            double end = Math.Min(index.Header.EndSeconds, candidate.CenterSeconds + rule.PostRollSeconds);
            if (end <= start) continue;
            hits.Add(new(rule.Id, new(start, end), [candidate.CenterSeconds]));
        }

        var episodes = EpisodeUnion.Build(hits).Episodes;
        return new(rule.Id, true, null,
            episodes.Select(e => new ProfileHit(rule.Id, e.Range, e.AnchorSourceTimes)).ToImmutableArray());
    }

    private static bool Matches(FeatureTable table, TransitionCandidate candidate, GenericFilterKind kind, double sensitivity)
    {
        var x = candidate.Signature.Visual;
        return kind switch
        {
            GenericFilterKind.LargeSceneChange => SceneChangeScore(x) >= .18 / sensitivity,
            GenericFilterKind.DarkFade => DarkFade(table, candidate, sensitivity),
            GenericFilterKind.QuietToActivity => QuietToActivity(table, candidate, sensitivity),
            _ => false
        };
    }

    private static double SceneChangeScore(ImmutableArray<float> x)
        => Math.Max(x[2], Math.Max(x[8], Math.Max(.75 * x[9], .8 * Math.Abs(x[3]))));

    private static bool DarkFade(FeatureTable table, TransitionCandidate candidate, double sensitivity)
    {
        if (!candidate.FullContext || candidate.Before.End - candidate.Before.Start < .5 || candidate.After.End - candidate.After.Start < .5)
            return false;
        float? before = AverageLuma(table, candidate.Before);
        float? after = AverageLuma(table, candidate.After);
        if (!before.HasValue || !after.HasValue) return false;

        double darkCeiling = Math.Min(.35, .16 * sensitivity);
        double minimumDrop = .10 / sensitivity;
        double minimumStrength = .025 / sensitivity;
        return after.Value <= darkCeiling
            && before.Value - after.Value >= minimumDrop
            && candidate.Strength >= minimumStrength;
    }

    private static bool QuietToActivity(FeatureTable table, TransitionCandidate candidate, double sensitivity)
    {
        if (!candidate.FullContext || candidate.Before.End - candidate.Before.Start < .5 || candidate.After.End - candidate.After.Start < .5)
            return false;
        float? before = Average(table, candidate.Before, row => row.Delta);
        float? after = Average(table, candidate.After, row => row.Delta);
        if (!before.HasValue || !after.HasValue) return false;
        double rise = after.Value - before.Value;
        return before.Value <= .04 * sensitivity
            && after.Value >= .06 / sensitivity
            && rise >= .035 / sensitivity;
    }

    private static float? AverageLuma(FeatureTable table, TimeRange range)
        => Average(table, range, row => row.Luma);

    private static float? Average(FeatureTable table, TimeRange range, Func<VideoFeature, float> selector)
    {
        float sum = 0;
        int count = 0;
        foreach (var row in table.Pack.Video)
        {
            if (row.TimeSeconds + Epsilon < range.Start || row.TimeSeconds >= range.End - Epsilon) continue;
            sum += selector(row);
            count++;
        }
        return count == 0 ? null : sum / count;
    }
}
