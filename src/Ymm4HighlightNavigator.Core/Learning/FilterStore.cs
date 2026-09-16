using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

/// <summary>Minimal explicit draft/apply/revision/rollback; no automatic negative learning in this slice.</summary>
public sealed class FilterStore(CorpusStore corpus)
{
    private sealed record Head(string Key, int Revision);
    private sealed record HeadIndex(int Schema, ImmutableArray<Head> Heads);
    private string HeadPath => Path.Combine(corpus.Root, "filters.json");
    private HeadIndex ReadHeads()
    {
        if (!File.Exists(HeadPath)) return new(1, []);
        LearningInputs.RejectLinks(HeadPath);
        var value = LearningJson.Read<HeadIndex>(HeadPath);
        ValidateHeads(value); return value;
    }
    private static void ValidateHeads(HeadIndex value)
    {
        if (value.Schema != 1 || value.Heads.IsDefault || value.Heads.Length > 1000 || value.Heads.Any(h => h is null || !LearningJson.IsHash(h.Key) || h.Revision <= 0)
            || value.Heads.Select(h => h.Key).Distinct().Count() != value.Heads.Length) throw new InvalidDataException("フィルター一覧が壊れています。");
    }
    private string RevisionPath(string key, int revision)
    {
        if (!LearningJson.IsHash(key) || revision <= 0) throw new InvalidDataException("フィルター版の指定が不正です。");
        return Path.Combine(corpus.Root, "filters", key, revision.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ".json");
    }
    private TransitionFilter Load(Head head)
    {
        string path = RevisionPath(head.Key, head.Revision);
        LearningInputs.RejectLinks(path);
        var filter = LearningJson.Read<TransitionFilter>(path); filter.Validate();
        if (filter.Label.Key != head.Key || filter.Revision != head.Revision) throw new InvalidDataException("フィルターの識別情報が一致しません。");
        return filter;
    }
    public ImmutableArray<TransitionFilter> ReadAll() => ReadHeads().Heads.Select(Load).ToImmutableArray();
    public TransitionFilter? Read(LearningLabel label)
    {
        string key = label.Key;
        var head = ReadHeads().Heads.FirstOrDefault(h => h.Key == key);
        return head == null ? null : Load(head);
    }
    public TransitionFilter Apply(FilterDraft draft, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); draft.Filter.Validate();
        using var writer = corpus.AcquireWriter();
        var heads = ReadHeads(); string key = draft.Filter.Label.Key;
        var oldHead = heads.Heads.FirstOrDefault(h => h.Key == key);
        if ((oldHead?.Revision ?? 0) != draft.BaseRevision) throw new InvalidOperationException("フィルターが別の操作で更新されました。候補を作り直してください。");
        if (corpus.Read().Revision != draft.CorpusRevision) throw new InvalidOperationException("教材が変更されました。候補を作り直してください。");
        // Recompute instead of trusting serialized preview counts. Reference sensitivity is always one.
        var examples = FilterAuthor.Load(corpus, draft.Filter.Label, token);
        var after = TransitionMatcher.Replay(examples, draft.Filter, token);
        if (oldHead != null)
        {
            var before = TransitionMatcher.Replay(examples, Load(oldHead), token);
            var nowCovered = after.Samples.Where(s => s.State == CoverageState.CoveredCandidate).Select(s => s.SampleId).ToHashSet(StringComparer.Ordinal);
            if (before.Samples.Any(s => s.State == CoverageState.CoveredCandidate && !nowCovered.Contains(s.SampleId)))
                throw new InvalidOperationException("以前拾えていた教材を落とす変更のため、保存しませんでした。");
        }
        if (after.Covered == 0) throw new InvalidOperationException("拾える教材がないため保存しませんでした。");
        int revision = checked((oldHead?.Revision ?? 0) + 1);
        // An orphan revision from a cancelled publication never overwrites an existing immutable file.
        while (File.Exists(RevisionPath(key, revision))) revision = checked(revision + 1);
        var committed = draft.Filter with { Revision = revision, ParentRevision = oldHead?.Revision ?? 0 };
        LearningJson.Write(RevisionPath(key, revision), committed, f => f.Validate(), token, overwrite: false);
        token.ThrowIfCancellationRequested();
        var head = new Head(key, revision);
        var entries = oldHead == null ? heads.Heads.Add(head) : heads.Heads.SetItem(heads.Heads.IndexOf(oldHead), head);
        LearningJson.Write(HeadPath, new HeadIndex(1, entries), ValidateHeads, token);
        return committed;
    }
    public TransitionFilter Rollback(LearningLabel label, int expectedRevision, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var writer = corpus.AcquireWriter();
        var heads = ReadHeads(); var head = heads.Heads.SingleOrDefault(h => h.Key == label.Key) ?? throw new InvalidOperationException("フィルターがありません。");
        if (head.Revision != expectedRevision) throw new InvalidOperationException("フィルターの版が変更されました。");
        var current = Load(head);
        if (current.ParentRevision <= 0) throw new InvalidOperationException("戻せる前の版がありません。");
        var previousHead = head with { Revision = current.ParentRevision }; var previous = Load(previousHead);
        LearningJson.Write(HeadPath, new HeadIndex(1, heads.Heads.SetItem(heads.Heads.IndexOf(head), previousHead)), ValidateHeads, token);
        return previous;
    }
}
