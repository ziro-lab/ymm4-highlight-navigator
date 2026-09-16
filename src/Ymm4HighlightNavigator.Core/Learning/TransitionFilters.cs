using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public sealed record TransitionPattern(string Id, TransitionSignature Representative, float Radius, float MinimumStrength,
    ImmutableArray<string> SupportSampleIds);
public sealed record TransitionFilter(int Schema, string Algorithm, LearningLabel Label, int Revision, int ParentRevision,
    ImmutableArray<TransitionPattern> Patterns, double PreRollSeconds, double PostRollSeconds)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Id => "learned." + Label.Key;
    public void Validate()
    {
        if (Schema != 1 || Algorithm != TransitionIndex.Algorithm || Label is null || Label != Label.Normalize()
            || Revision < 0 || ParentRevision < 0 || ParentRevision >= Revision && Revision > 0 || Patterns.IsDefaultOrEmpty || Patterns.Length > 12)
            throw new InvalidDataException("フィルターの形式が対応していません。");
        if (!double.IsFinite(PreRollSeconds) || !double.IsFinite(PostRollSeconds) || PreRollSeconds < 0 || PostRollSeconds <= 0 || PreRollSeconds > 30 || PostRollSeconds > 30)
            throw new InvalidDataException("フィルターの前後時間が不正です。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pattern in Patterns)
        {
            if (pattern is null || !LearningJson.IsHash(pattern.Id) || !ids.Add(pattern.Id) || pattern.Representative is null
                || !float.IsFinite(pattern.Radius) || pattern.Radius <= 0 || pattern.Radius > .5f || !float.IsFinite(pattern.MinimumStrength)
                || pattern.MinimumStrength < .02f || pattern.MinimumStrength > 1 || pattern.SupportSampleIds.IsDefaultOrEmpty
                || pattern.SupportSampleIds.Any(id => !LearningJson.IsHash(id)) || pattern.SupportSampleIds.Distinct().Count() != pattern.SupportSampleIds.Length)
                throw new InvalidDataException("フィルターのパターンが壊れています。");
            pattern.Representative.Validate();
        }
    }
}
public sealed record TransitionMatch(double CenterSeconds, float Distance, ImmutableArray<string> PatternIds);
public enum CoverageState { CoveredCandidate, HardPositive, NoTransition }
public sealed record SampleCoverage(string SampleId, string DisplayName, CoverageState State, ImmutableArray<double> Centers);
public sealed record CoverageReport(ImmutableArray<SampleCoverage> Samples)
{
    public int Covered => Samples.Count(x => x.State == CoverageState.CoveredCandidate);
    public int Hard => Samples.Count(x => x.State == CoverageState.HardPositive);
    public int NoTransition => Samples.Count(x => x.State == CoverageState.NoTransition);
}
public sealed record LearningExample(LearningSample Sample, TransitionIndex Index);
public sealed record LearningSet(long CorpusRevision, LearningLabel Label, ImmutableArray<LearningExample> Examples);
public sealed record FilterDraft(long CorpusRevision, int BaseRevision, TransitionFilter Filter, CoverageReport Coverage);

public static class TransitionMatcher
{
    // Audio is retained as optional evidence, not required by visual-transition-v1. No missing audio is imputed as silence.
    public static float Distance(TransitionSignature left, TransitionSignature right)
    {
        var a = left.Visual; var b = right.Visual;
        float activity = Math.Max(Math.Abs(a[0] - b[0]), Math.Max(Math.Abs(a[1] - b[1]), Math.Abs(a[2] - b[2])));
        float state = 0;
        for (int i = 3; i <= 8; i++) state = Math.Max(state, Math.Min(1, Math.Abs(a[i] - b[i])));
        float grid = 0, shape = 0;
        for (int i = 10; i < 26; i++) grid += Math.Min(1, Math.Abs(a[i] - b[i])) / 16;
        for (int i = 26; i < 32; i++) shape += Math.Abs(a[i] - b[i]) / 6;
        return Math.Clamp(.35f * activity + .35f * state + .15f * grid + .15f * shape, 0, 1);
    }
    private static bool Matches(TransitionCandidate point, TransitionPattern pattern, double sensitivity)
        => point.Strength >= pattern.MinimumStrength / sensitivity && Distance(point.Signature, pattern.Representative) <= pattern.Radius * sensitivity;
    public static ImmutableArray<TransitionMatch> Match(TransitionIndex index, TransitionFilter filter, double sensitivity = 1, CancellationToken token = default)
    {
        filter.Validate();
        if (!double.IsFinite(sensitivity) || sensitivity < .25 || sensitivity > 2) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        var result = ImmutableArray.CreateBuilder<TransitionMatch>();
        foreach (var candidate in index.Candidates)
        {
            token.ThrowIfCancellationRequested();
            var matches = filter.Patterns.Where(p => Matches(candidate, p, sensitivity)).ToArray();
            if (matches.Length != 0) result.Add(new(candidate.CenterSeconds, matches.Min(p => Distance(candidate.Signature, p.Representative)), matches.Select(p => p.Id).ToImmutableArray()));
        }
        return result.ToImmutable();
    }
    public static ProfileEvaluation Evaluate(TransitionIndex index, TransitionFilter filter, double sensitivity = 1, CancellationToken token = default)
    {
        var hits = Match(index, filter, sensitivity, token).Select(m => new TimeRange(Math.Max(index.Header.StartSeconds, m.CenterSeconds - filter.PreRollSeconds), Math.Min(index.Header.EndSeconds, m.CenterSeconds + filter.PostRollSeconds)));
        return new(filter.Id, true, null, TimeRange.Union(hits).Select(r => new ProfileHit(filter.Id, r)).ToImmutableArray());
    }
    public static CoverageReport Replay(LearningSet set, TransitionFilter filter, CancellationToken token = default)
    {
        // Fixed reference sensitivity: dragging the runtime slider must never silently reclassify training value.
        return new(set.Examples.Select(e =>
        {
            token.ThrowIfCancellationRequested();
            var matches = Match(e.Index, filter, 1, token);
            var state = matches.IsEmpty ? e.Index.Candidates.IsEmpty ? CoverageState.NoTransition : CoverageState.HardPositive : CoverageState.CoveredCandidate;
            return new SampleCoverage(e.Sample.Id, e.Sample.OriginalNames[0], state, matches.Select(m => m.CenterSeconds).ToImmutableArray());
        }).ToImmutableArray());
    }
}

public static class FilterAuthor
{
    public static LearningSet Load(CorpusStore store, LearningLabel label, CancellationToken token = default)
    {
        label = label.Normalize(); var snapshot = store.Read();
        var examples = ImmutableArray.CreateBuilder<LearningExample>();
        foreach (var sample in snapshot.Samples.Where(s => s.Labels.Contains(label)).OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            examples.Add(new(sample, TransitionIndex.Build(store.LoadPack(sample), token)));
        }
        return new(snapshot.Revision, label, examples.ToImmutable());
    }
    public static FilterDraft Create(LearningSet set, TransitionFilter? previous = null, CancellationToken token = default)
    {
        if (set.Examples.IsDefaultOrEmpty) throw new InvalidOperationException("このフィルターの教材がありません。");
        if (previous != null && previous.Label != set.Label) throw new ArgumentException("別の分類のフィルターは更新できません。");
        previous?.Validate();
        var patterns = previous?.Patterns.ToList() ?? [];
        TransitionFilter Current() => new(1, TransitionIndex.Algorithm, set.Label, 0, 0, patterns.ToImmutableArray(), 2, 1);
        // Existing patterns remain intact. Search concentrates on uncovered samples instead of averaging every entry together.
        while (patterns.Count < 12)
        {
            token.ThrowIfCancellationRequested();
            var remaining = set.Examples.Where(e => !e.Index.Candidates.IsEmpty && (patterns.Count == 0 || TransitionMatcher.Match(e.Index, Current(), token: token).IsEmpty)).ToArray();
            if (remaining.Length == 0) break;
            // Budget applies only to proposed learning prototypes, not the runtime candidate scan. Uncovered samples remain visible.
            var proposals = remaining.SelectMany(e => e.Index.Candidates.OrderByDescending(c => c.FullContext).ThenByDescending(c => c.Strength).ThenBy(c => c.CenterSeconds).Take(4).Select(c => (Sample: e.Sample.Id, Candidate: c))).Take(512).ToArray();
            TransitionPattern? best = null; int support = -1; float strength = -1;
            foreach (var proposal in proposals)
            {
                token.ThrowIfCancellationRequested();
                string id = LearningJson.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(proposal.Candidate.Signature.Visual));
                if (patterns.Any(p => p.Id == id)) continue;
                var trial = new TransitionPattern(id, proposal.Candidate.Signature, .18f, Math.Max(.02f, proposal.Candidate.Strength * .35f), [proposal.Sample]);
                var filter = new TransitionFilter(1, TransitionIndex.Algorithm, set.Label, 0, 0, [trial], 2, 1);
                var ids = remaining.Where(e => !TransitionMatcher.Match(e.Index, filter, token: token).IsEmpty).Select(e => e.Sample.Id).Distinct().Order(StringComparer.Ordinal).ToImmutableArray();
                if (ids.Length > support || ids.Length == support && proposal.Candidate.Strength > strength)
                { best = trial with { SupportSampleIds = ids }; support = ids.Length; strength = proposal.Candidate.Strength; }
            }
            if (best == null || support <= 0) break;
            patterns.Add(best);
        }
        if (patterns.Count == 0) throw new InvalidOperationException("共通特徴の候補を作れませんでした。切り替わり前後が入った教材を追加してください。");
        var candidate = Current();
        return new(set.CorpusRevision, previous?.Revision ?? 0, candidate, TransitionMatcher.Replay(set, candidate, token));
    }
}
