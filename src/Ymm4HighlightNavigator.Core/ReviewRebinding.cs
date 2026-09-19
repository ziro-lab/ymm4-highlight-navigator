using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public static class ReviewSourceIdentity
{
    // The host adapter supplies absolute Windows paths; Core does not touch the filesystem.
    public static string Key(string source) => source.Replace('\\', '/').ToUpperInvariant();
    public static bool Same(string a, string b) => StringComparer.Ordinal.Equals(Key(a), Key(b));
    public static bool Contains(TimeRange range, double time) => double.IsFinite(time) && time >= range.Start && time < range.End;
}
public readonly record struct SourceStamp(long Bytes, long WriteUtcTicks)
{
    public bool Matches(bool exists, SourceStamp current) => exists && this == current;
}
public sealed record ReviewSourceSession(TargetSnapshot Capture, int CaptureOrder, SourceStamp Stamp)
{
    public Guid ReviewTargetId => Capture.Id;
    public string SourceKey => Capture.SourceKey;
    public TimeRange AnalyzedSourceRange => Capture.SourceRange;
}
public readonly record struct ReviewCandidateIdentity(Guid TargetId, string SourceKey, long AnchorTicks)
{
    public static ReviewCandidateIdentity Create(Guid target, string source, double anchor)
    {
        if (target == Guid.Empty || string.IsNullOrWhiteSpace(source) || !double.IsFinite(anchor) || anchor < 0)
            throw new ArgumentException("Invalid review identity.");
        return new(target, ReviewSourceIdentity.Key(source), TimeSpan.FromSeconds(anchor).Ticks);
    }
}
public readonly record struct StableReviewOrder(int TargetOrder, double AnchorSourceTime) : IComparable<StableReviewOrder>
{
    public int CompareTo(StableReviewOrder other)
    {
        int order = TargetOrder.CompareTo(other.TargetOrder);
        return order != 0 ? order : AnchorSourceTime.CompareTo(other.AnchorSourceTime);
    }
}
public enum RebindStatus { Available, Unavailable, Ambiguous, Unsupported, SourceChanged }
// A null mapping means a PRESENT host item with unsupported timing, not a deleted item.
public sealed record OccurrenceObservation(Guid ReferenceId, TargetSnapshot? Mapping, int Layer, string? Problem = null);
public sealed record OccurrenceResolution(RebindStatus Status, OccurrenceObservation? Occurrence, string? Reason)
{
    public bool Available => Status == RebindStatus.Available;
}

/// <summary>Session-only lineage over detached reference tokens. No YMM4 object or decoder is retained here.</summary>
public sealed class OccurrenceLineage
{
    private sealed class Node(OccurrenceObservation observed, Guid? parent)
    {
        public OccurrenceObservation LastGood = observed;
        public Guid? Parent = parent;
        public bool Blocked;
        public RebindStatus Failure = RebindStatus.Unavailable;
    }
    private readonly ReviewSourceSession session;
    private readonly Dictionary<Guid, Node> history = [];
    private readonly HashSet<Guid> seen = [];
    private HashSet<Guid> frontier = [];
    private Dictionary<Guid, OccurrenceObservation> current = [];
    public const string AmbiguousMessage = "この候補の移動先を一意に特定できません。";

    public OccurrenceLineage(ReviewSourceSession session, OccurrenceObservation original, IEnumerable<Guid> observedReferences)
    {
        session.Capture.Validate();
        if (original.ReferenceId == Guid.Empty || original.Mapping is null || !ReviewSourceIdentity.Same(original.Mapping.SourceKey, session.SourceKey))
            throw new ArgumentException("A supported original occurrence is required.");
        this.session = session;
        history.Add(original.ReferenceId, new(original, null));
        frontier.Add(original.ReferenceId); current.Add(original.ReferenceId, original);
        seen.UnionWith(observedReferences); seen.Add(original.ReferenceId);
    }
    public bool Knows(Guid reference) => history.ContainsKey(reference);
    public int KnownReferenceCount => history.Count;

    public void Refresh(IReadOnlyList<OccurrenceObservation> observations)
    {
        if (observations.Any(o => o.ReferenceId == Guid.Empty) || observations.Select(o => o.ReferenceId).Distinct().Count() != observations.Count)
            throw new ArgumentException("Occurrence reference tokens must be unique.");
        foreach (var observation in observations) observation.Mapping?.Validate();
        current = observations.ToDictionary(o => o.ReferenceId);
        var present = current.Keys.Where(history.ContainsKey).ToHashSet();
        foreach (var id in present)
        {
            var node = history[id]; var value = current[id];
            if (value.Mapping is not null && ReviewSourceIdentity.Same(value.Mapping.SourceKey, session.SourceKey))
            { node.LastGood = value; node.Blocked = false; }
            else { node.Blocked = true; node.Failure = value.Mapping is null ? RebindStatus.Unsupported : RebindStatus.SourceChanged; }
        }
        var next = new HashSet<Guid>(present);
        foreach (var missing in frontier.Where(id => !current.ContainsKey(id)))
        {
            // Undo/Redo may reintroduce a known ancestor or descendant. Siblings do not explain a disappearance.
            if (present.Any(id => AncestorOf(missing, id) || AncestorOf(id, missing))) continue;
            var node = history[missing];
            if (!node.Blocked)
            {
                var fresh = observations.Where(o => !history.ContainsKey(o.ReferenceId) && !seen.Contains(o.ReferenceId)).ToArray();
                var partition = UniquePartition(node.LastGood, fresh);
                if (!partition.IsEmpty)
                {
                    foreach (var piece in partition)
                    { history.Add(piece.ReferenceId, new(piece, missing)); next.Add(piece.ReferenceId); }
                    continue;
                }
                // Never reconsider an unexplained disappearance after a later paste. Known refs can still recover it.
                node.Blocked = true;
                node.Failure = fresh.Any(o => o.Mapping is not null && ReviewSourceIdentity.Same(o.Mapping.SourceKey, session.SourceKey)
                    && o.Mapping.SourceRange.Intersect(node.LastGood.Mapping!.SourceRange) is not null)
                    ? RebindStatus.Ambiguous : RebindStatus.Unavailable;
            }
            next.Add(missing);
        }
        frontier = next; seen.UnionWith(current.Keys);
    }

    private bool AncestorOf(Guid ancestor, Guid descendant)
    {
        for (Guid? at = descendant; at is not null; at = history[at.Value].Parent)
            if (at.Value == ancestor) return true;
        return false;
    }
    private static ImmutableArray<OccurrenceObservation> UniquePartition(OccurrenceObservation previous, OccurrenceObservation[] observations)
    {
        var parent = previous.Mapping!;
        int end = checked(parent.StartFrame + parent.LengthFrames);
        var pieces = observations.Where(o => o.Mapping is { } m && o.Layer == previous.Layer
            && ReviewSourceIdentity.Same(m.SourceKey, parent.SourceKey) && m.Fps == parent.Fps
            && Math.Abs(m.RatePercent - parent.RatePercent) < 1e-8
            && m.StartFrame >= parent.StartFrame && (long)m.StartFrame + m.LengthFrames <= end
            && m.LengthFrames < parent.LengthFrames
            && Math.Abs(m.OffsetSeconds - (parent.OffsetSeconds + (m.StartFrame - parent.StartFrame) / (double)parent.Fps * parent.RatePercent / 100)) < 1e-6)
            .GroupBy(o => o.Mapping!.StartFrame).ToDictionary(g => g.Key, g => g.ToArray());
        // Count complete partitions, capped at two. No exponential enumeration and no first-match tie breaking.
        var ways = new Dictionary<int, int> { [end] = 1 };
        foreach (var start in pieces.Keys.OrderDescending())
        {
            int count = 0;
            foreach (var piece in pieces[start])
                count = Math.Min(2, count + ways.GetValueOrDefault(piece.Mapping!.StartFrame + piece.Mapping.LengthFrames));
            ways[start] = count;
        }
        if (ways.GetValueOrDefault(parent.StartFrame) != 1) return [];
        var result = ImmutableArray.CreateBuilder<OccurrenceObservation>();
        for (int at = parent.StartFrame; at < end;)
        {
            var piece = pieces[at].Single(p => ways.GetValueOrDefault(p.Mapping!.StartFrame + p.Mapping.LengthFrames) > 0);
            result.Add(piece); at += piece.Mapping!.LengthFrames;
        }
        return result.Count >= 2 ? result.ToImmutable() : [];
    }

    public OccurrenceResolution Resolve(double anchor)
    {
        if (!ReviewSourceIdentity.Contains(session.AnalyzedSourceRange, anchor))
            return new(RebindStatus.Unavailable, null, "この候補は解析済みの範囲外です。");
        var matches = new List<OccurrenceObservation>();
        RebindStatus problem = RebindStatus.Unavailable;
        foreach (var id in frontier)
        {
            var node = history[id];
            if (current.TryGetValue(id, out var value))
            {
                if (value.Mapping is { } mapping && ReviewSourceIdentity.Same(mapping.SourceKey, session.SourceKey))
                { if (ReviewSourceIdentity.Contains(mapping.SourceRange, anchor)) matches.Add(value); }
                else if (ReviewSourceIdentity.Contains(node.LastGood.Mapping!.SourceRange, anchor))
                    problem = value.Mapping is null ? RebindStatus.Unsupported : RebindStatus.SourceChanged;
            }
            else if (ReviewSourceIdentity.Contains(node.LastGood.Mapping!.SourceRange, anchor) && node.Failure != RebindStatus.Unavailable)
                problem = node.Failure;
        }
        if (problem != RebindStatus.Unavailable) return new(problem, null, Reason(problem));
        return matches.Count switch
        {
            1 => new(RebindStatus.Available, matches[0], null),
            > 1 => new(RebindStatus.Ambiguous, null, AmbiguousMessage),
            _ => new(RebindStatus.Unavailable, null, "この候補はトリミング・削除により現在の対象にありません。")
        };
    }
    private static string Reason(RebindStatus status) => status switch
    {
        RebindStatus.Unsupported => "この候補の再生速度・時間変換には対応していません。",
        RebindStatus.SourceChanged => "動画の参照先が変わりました。対象を選び直して再解析してください。",
        _ => AmbiguousMessage
    };
}

public static class ReviewNavigation
{
    // Uses the immutable queue order. Availability changes do not reorder the queue or decode media.
    public static int? Next(int count, int current, int direction, Func<int, bool> available)
    {
        if (count <= 0 || direction == 0) return null;
        int step = Math.Sign(direction);
        for (int i = current + step; i >= 0 && i < count; i += step) if (available(i)) return i;
        return null;
    }
}
