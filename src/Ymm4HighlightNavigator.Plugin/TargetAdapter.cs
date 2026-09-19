using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using Ymm4HighlightNavigator.Core;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record CandidateProjection(int? Frame, int Fps, RebindStatus Status, string? Reason)
{
    public bool Available => Frame.HasValue && Status == RebindStatus.Available;
}

/// <summary>All host-private access lives here. No live YMM4 object leaves the UI thread.</summary>
public sealed class TargetAdapter
{
    private sealed record Entry(ReviewSourceSession Session, OccurrenceLineage Lineage);
    private sealed class ReferenceToken { public Guid Id { get; } = Guid.NewGuid(); }
    private readonly List<Entry> entries = [];
    private ConditionalWeakTable<VideoItem, ReferenceToken> references = new();
    private Dictionary<Guid, VideoItem> currentItems = [];
    private Timeline? timeline;
    public ImmutableArray<TargetSnapshot> Snapshots => entries.Select(e => e.Session.Capture).ToImmutableArray();
    public ImmutableArray<ReviewSourceSession> Sessions => entries.Select(e => e.Session).ToImmutableArray();
    public bool HasTimeline => timeline != null;

    // Check capabilities on use, not version numbers. Weak tokens preserve historical identity without retaining items.
    public bool Attach(Timeline? next)
    {
        if (ReferenceEquals(timeline, next)) return false;
        timeline = next; entries.Clear(); currentItems.Clear(); references = new(); return true;
    }
    private static void Ui() => Application.Current.Dispatcher.VerifyAccess();
    private Guid Reference(VideoItem item) => references.GetValue(item, static _ => new ReferenceToken()).Id;

    public void CaptureSelection()
    {
        Ui();
        try
        {
            var host = timeline ?? throw new InvalidOperationException("対象のタイムラインがありません。");
            int fps = host.VideoInfo.FPS;
            var selected = host.SelectedItems.OfType<VideoItem>().Distinct(ReferenceEqualityComparer.Instance).Cast<VideoItem>()
                .OrderBy(v => v.Frame).ThenBy(v => v.Layer).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("YMM4で動画アイテムを選択してください。");
            var observed = host.Items.OfType<VideoItem>().Select(Reference).ToArray();
            var next = new List<Entry>();
            foreach (var item in selected)
            {
                if (!host.Items.Any(x => ReferenceEquals(x, item))) throw new InvalidOperationException("選択した動画がタイムラインにありません。");
                var snapshot = Read(item, fps, Guid.NewGuid());
                var file = new FileInfo(snapshot.SourceKey);
                if (!file.Exists) throw new FileNotFoundException("録画ファイルが見つかりません。", snapshot.SourceKey);
                var session = new ReviewSourceSession(snapshot, next.Count, new(file.Length, file.LastWriteTimeUtc.Ticks));
                next.Add(new(session, new(session, new(Reference(item), snapshot, item.Layer), observed)));
            }
            // A failed capture must not partially replace the Target Set or its analysis authority.
            entries.Clear(); entries.AddRange(next); currentItems.Clear();
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex)) { throw DependencyChanged(ex); }
    }

    // Timeline edits are NOT source mutations. Captured source coverage remains immutable for decoding/querying.
    public void ValidateCurrent()
    {
        Ui();
        if (timeline is null) throw new InvalidOperationException("タイムラインが変わりました。対象を選び直してください。");
        foreach (var entry in entries)
        {
            var file = new FileInfo(entry.Session.SourceKey);
            if (!file.Exists || !entry.Session.Stamp.Matches(true, new(file.Length, file.LastWriteTimeUtc.Ticks))) throw Stale();
        }
    }
    private static InvalidOperationException Stale() => new("録画ファイルが削除・変更されました。対象を選び直して再解析してください。");

    public void RefreshCurrent()
    {
        Ui(); ValidateCurrent();
        try
        {
            var host = timeline!; int fps = host.VideoInfo.FPS;
            var observations = new List<OccurrenceObservation>();
            var items = new Dictionary<Guid, VideoItem>();
            foreach (var item in host.Items.OfType<VideoItem>())
            {
                Guid id = Reference(item); items.Add(id, item);
                try
                {
                    bool relevant = entries.Any(e => e.Lineage.Knows(id)) || !string.IsNullOrWhiteSpace(item.FilePath)
                        && entries.Any(e => ReviewSourceIdentity.Same(e.Session.SourceKey, Path.GetFullPath(item.FilePath)));
                    observations.Add(relevant ? new(id, Read(item, fps, id), item.Layer) : new(id, null, item.Layer));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException or OverflowException || IsHostDependencyFailure(ex))
                {
                    // A present unsupported occurrence must not look deleted and accidentally rebind to its copy.
                    observations.Add(new(id, null, item.Layer, IsHostDependencyFailure(ex) ? DependencyChanged(ex).Message : ex.Message));
                }
            }
            foreach (var entry in entries) entry.Lineage.Refresh(observations);
            currentItems = items;
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex)) { throw DependencyChanged(ex); }
    }

    // Call after RefreshCurrent, within the same UI-thread operation. HostMap is always the current item's map.
    public CandidateProjection ProjectCurrent(Guid targetId, double anchor, TimeRange? requestedRange = null)
    {
        Ui();
        var entry = entries.SingleOrDefault(e => e.Session.ReviewTargetId == targetId)
            ?? throw new InvalidOperationException("対象が変わりました。動画を対象に追加してください。");
        int fps = timeline?.VideoInfo.FPS ?? entry.Session.Capture.Fps;
        var resolved = entry.Lineage.Resolve(anchor);
        if (!resolved.Available) return new(null, fps, resolved.Status, resolved.Reason);
        var occurrence = resolved.Occurrence!;
        if (!currentItems.TryGetValue(occurrence.ReferenceId, out var item)) return Missing(fps);
        try
        {
            var map = HostMap.Read(item);
            if (!map.IsConstant || !double.IsFinite(map.FirstRate) || map.FirstRate <= 0)
                return new(null, fps, RebindStatus.Unsupported, "この候補の再生速度・時間変換には対応していません。");
            var itemTime = map.Inverse(TimeSpan.FromSeconds(anchor), item.Length, fps, item.ContentOffset, item.ContentLength);
            if (itemTime is null) return Missing(fps);
            int local = Math.Max(0, checked((int)Math.Ceiling(itemTime.Value.TotalSeconds * fps - 1e-9)));
            if (local >= item.Length) return Missing(fps);
            var actualSource = map.Forward(TimeSpan.FromSeconds(local / (double)fps), item.Length, fps, item.ContentOffset, item.ContentLength);
            double end = Math.Min(entry.Session.AnalyzedSourceRange.End, occurrence.Mapping!.SourceRange.End);
            if (requestedRange is { } requested) end = Math.Min(end, requested.End);
            if (actualSource.TotalSeconds < anchor - 1e-6 || actualSource.TotalSeconds >= end) return Missing(fps);
            return new(checked(item.Frame + local), fps, RebindStatus.Available, null);
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex)) { return new(null, fps, RebindStatus.Unsupported, DependencyChanged(ex).Message); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { return new(null, fps, RebindStatus.Unsupported, ex.Message); }
    }
    private static CandidateProjection Missing(int fps) => new(null, fps, RebindStatus.Unavailable, "この候補には移動できるフレームがありません。");

    // Retain the original range projection surface for the fractional-frame and exclusive-end regressions.
    public int? Project(Guid targetId, TimeRange sourceRange)
    {
        RefreshCurrent();
        var entry = entries.SingleOrDefault(e => e.Session.ReviewTargetId == targetId) ?? throw Stale();
        var clipped = entry.Session.AnalyzedSourceRange.Intersect(sourceRange);
        return clipped is null ? null : ProjectCurrent(targetId, clipped.Value.Start, clipped.Value).Frame;
    }
    public CandidateProjection JumpAnchor(Guid targetId, double anchor)
    {
        RefreshCurrent();
        var projection = ProjectCurrent(targetId, anchor);
        if (!projection.Available) throw new InvalidOperationException(projection.Reason);
        MovePlayhead(projection.Frame!.Value); return projection;
    }
    public int Jump(Guid targetId, TimeRange sourceRange)
    {
        int frame = Project(targetId, sourceRange) ?? throw new InvalidOperationException("この候補には移動できるフレームがありません。");
        MovePlayhead(frame); return frame;
    }
    private void MovePlayhead(int frame)
    {
        try
        {
            timeline!.CurrentFrame = frame;
            if (timeline.CurrentFrame != frame) throw new InvalidOperationException("YMM4の再生位置を移動できませんでした。");
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex)) { throw DependencyChanged(ex); }
    }

    private static TargetSnapshot Read(VideoItem item, int fps, Guid id)
    {
        var map = HostMap.Read(item);
        if (!map.IsConstant || !double.IsFinite(map.FirstRate) || map.FirstRate <= 0)
            throw new NotSupportedException("この開発版は正の一定再生速度のみ対応しています。");
        if (string.IsNullOrWhiteSpace(item.FilePath)) throw new InvalidOperationException("動画の参照先がありません。");
        var snapshot = new TargetSnapshot(id, Path.GetFullPath(item.FilePath), item.Frame, item.Length, fps, item.ContentOffset.TotalSeconds, map.FirstRate);
        snapshot.Validate();
        var start = map.Forward(TimeSpan.Zero, item.Length, fps, item.ContentOffset, item.ContentLength);
        var end = map.Forward(TimeSpan.FromSeconds(item.Length / (double)fps), item.Length, fps, item.ContentOffset, item.ContentLength);
        if (Math.Abs(start.TotalSeconds - snapshot.SourceRange.Start) > 1e-6 || Math.Abs(end.TotalSeconds - snapshot.SourceRange.End) > 1e-6)
            throw new NotSupportedException("YMM4の時間変換が対応している形式と一致しないため、この動画では使用できません。");
        return snapshot;
    }

    private static bool IsHostDependencyFailure(Exception ex)
        => ex is MissingMemberException
            or TypeLoadException
            or TypeInitializationException
            or FileLoadException
            or ReflectionTypeLoadException
            or TargetInvocationException;

    private static NotSupportedException DependencyChanged(Exception ex)
        => new("YMM4の更新で依存関係が変更されたため、この機能は現在使用できません。", ex);

    private sealed class HostMap
    {
        private readonly object value;
        private readonly MethodInfo forward, inverse;
        public bool IsConstant { get; }
        public double FirstRate { get; }
        private HostMap(object value)
        {
            this.value = value;
            var type = value.GetType();
            var signature = new[] { typeof(TimeSpan), typeof(int), typeof(int), typeof(TimeSpan), typeof(TimeSpan) };
            forward = type.GetMethod("GetSourceTime", signature) ?? throw new MissingMethodException("PlaybackRateMap.GetSourceTime");
            inverse = type.GetMethod("FindFirstTimeForSourceTime", signature) ?? throw new MissingMethodException("PlaybackRateMap.FindFirstTimeForSourceTime");
            IsConstant = type.GetProperty("IsConstant")?.GetValue(value) is true;
            FirstRate = Convert.ToDouble(type.GetProperty("FirstRate")?.GetValue(value), CultureInfo.InvariantCulture);
        }
        public static HostMap Read(VideoItem item)
        {
            var property = typeof(VideoItem).GetProperty("PlaybackRateMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException("PlaybackRateMap");
            return new(property.GetValue(item) ?? throw new MissingMemberException("PlaybackRateMap value"));
        }
        public TimeSpan Forward(TimeSpan time, int length, int fps, TimeSpan offset, TimeSpan mediaDuration)
            => (TimeSpan)(forward.Invoke(value, [time, length, fps, offset, mediaDuration]) ?? throw new MissingMemberException("Source time result"));
        public TimeSpan? Inverse(TimeSpan time, int length, int fps, TimeSpan offset, TimeSpan mediaDuration)
            => inverse.Invoke(value, [time, length, fps, offset, mediaDuration]) is TimeSpan result ? result : null;
    }
}
