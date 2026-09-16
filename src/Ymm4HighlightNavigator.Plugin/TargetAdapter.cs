using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using Ymm4HighlightNavigator.Core;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace Ymm4HighlightNavigator.Plugin;

/// <summary>All host-private access lives here. No live YMM4 object leaves the UI thread.</summary>
public sealed class TargetAdapter
{
    private sealed record Entry(VideoItem Item, TargetSnapshot Snapshot, long SourceBytes, long SourceWriteTicks);
    private readonly List<Entry> entries = [];
    private Timeline? timeline;
    public ImmutableArray<TargetSnapshot> Snapshots => entries.Select(e => e.Snapshot).ToImmutableArray();
    public bool HasTimeline => timeline != null;

    // Host lifecycle callbacks must not fail merely because the YMM4 build number changed.
    // The actual feature use below checks the surfaces it needs and fails closed if they changed.
    public bool Attach(Timeline? next)
    {
        if (ReferenceEquals(timeline, next)) return false;
        timeline = next; entries.Clear(); return true;
    }
    private static void Ui() => Application.Current.Dispatcher.VerifyAccess();

    public void CaptureSelection()
    {
        Ui();
        try
        {
            var host = timeline ?? throw new InvalidOperationException("対象のタイムラインがありません。");
            int fps = host.VideoInfo.FPS;
            var selected = host.SelectedItems.OfType<VideoItem>().Distinct(ReferenceEqualityComparer.Instance).Cast<VideoItem>().ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("YMM4で動画アイテムを選択してください。");
            var next = new List<Entry>();
            foreach (var item in selected)
            {
                if (!host.Items.Any(x => ReferenceEquals(x, item))) throw new InvalidOperationException("選択した動画がタイムラインにありません。");
                var id = entries.FirstOrDefault(x => ReferenceEquals(x.Item, item))?.Snapshot.Id ?? Guid.NewGuid();
                var snapshot = Read(item, fps, id);
                var file = new FileInfo(snapshot.SourceKey);
                if (!file.Exists) throw new FileNotFoundException("録画ファイルが見つかりません。", snapshot.SourceKey);
                next.Add(new(item, snapshot, file.Length, file.LastWriteTimeUtc.Ticks));
            }
            // A failed capture must not leave a partially replaced Target Set.
            entries.Clear(); entries.AddRange(next);
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex))
        {
            throw DependencyChanged(ex);
        }
    }

    public void ValidateCurrent()
    {
        Ui();
        try
        {
            var host = timeline ?? throw new InvalidOperationException("タイムラインが変わりました。対象を選び直してください。");
            foreach (var entry in entries)
            {
                if (!host.Items.Any(x => ReferenceEquals(x, entry.Item))) throw Stale();
                var current = Read(entry.Item, host.VideoInfo.FPS, entry.Snapshot.Id);
                if (current != entry.Snapshot) throw Stale();
                var file = new FileInfo(current.SourceKey);
                if (!file.Exists || file.Length != entry.SourceBytes || file.LastWriteTimeUtc.Ticks != entry.SourceWriteTicks) throw Stale();
            }
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex))
        {
            throw DependencyChanged(ex);
        }
    }
    private static InvalidOperationException Stale() => new("対象の動画・位置・速度が変わりました。対象を選び直して再解析してください。");

    public int? Project(Guid targetId, TimeRange sourceRange)
    {
        Ui(); ValidateCurrent();
        try
        {
            var entry = entries.SingleOrDefault(x => x.Snapshot.Id == targetId) ?? throw Stale();
            var clipped = entry.Snapshot.SourceRange.Intersect(sourceRange);
            if (clipped == null) return null;
            var map = HostMap.Read(entry.Item);
            var itemTime = map.Inverse(TimeSpan.FromSeconds(clipped.Value.Start), entry.Item.Length, entry.Snapshot.Fps, entry.Item.ContentOffset, entry.Item.ContentLength);
            if (itemTime == null) return null;
            int local = Math.Max(0, checked((int)Math.Ceiling(itemTime.Value.TotalSeconds * entry.Snapshot.Fps - 1e-9)));
            if (local >= entry.Item.Length) return null;
            var actualSource = map.Forward(TimeSpan.FromSeconds(local / (double)entry.Snapshot.Fps), entry.Item.Length, entry.Snapshot.Fps, entry.Item.ContentOffset, entry.Item.ContentLength);
            if (actualSource.TotalSeconds < clipped.Value.Start - 1e-6 || actualSource.TotalSeconds >= clipped.Value.End) return null;
            return checked(entry.Item.Frame + local);
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex))
        {
            throw DependencyChanged(ex);
        }
    }

    public int Jump(Guid targetId, TimeRange sourceRange)
    {
        int frame = Project(targetId, sourceRange) ?? throw new InvalidOperationException("この候補には移動できるフレームがありません。");
        try
        {
            timeline!.CurrentFrame = frame;
            if (timeline.CurrentFrame != frame) throw new InvalidOperationException("YMM4の再生位置を移動できませんでした。");
            return frame;
        }
        catch (Exception ex) when (IsHostDependencyFailure(ex))
        {
            throw DependencyChanged(ex);
        }
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
