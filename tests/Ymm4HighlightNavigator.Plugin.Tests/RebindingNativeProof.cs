using System.Collections;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Ymm4HighlightNavigator.Core;
using Ymm4HighlightNavigator.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.UndoRedo;

namespace Ymm4HighlightNavigator.Plugin.Tests;

// Product integration only. Edit calls and emitted UndoRedo commands reuse the adopted Lab contract.
internal static class RebindingNativeProof
{
    public static async Task RunAsync(NavigatorModel model, Timeline timeline, string output, Action<string, bool> check)
    {
        string media = Environment.GetEnvironmentVariable("NAV_REBINDING_MEDIA") ?? throw new InvalidOperationException("W1-R fixture missing.");
        int fps = timeline.VideoInfo.FPS;
        string sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(media)));
        var root = new VideoItem { FilePath = media, Frame = 2500, Length = 24 * fps, Layer = 12, ContentOffset = TimeSpan.Zero, Remark = "NAV_W1R" };
        root.PlaybackRate2.SetFirstValue(100); root.PlaybackRate2.SetAnimationParameters(root.Length, fps);
        check("w1r_fixture_insert", timeline.TryAddItems([root], root.Frame, root.Layer));
        timeline.SelectedItems = ImmutableList.Create<IItem>(root); model.CaptureSelection();
        foreach (var profile in model.Profiles) profile.Enabled = false;
        model.Profiles.Add(new(new SceneProfile("w1r.native", "W1-R fixture", [new(FeatureAxis.Luma, 0, .8f)],
            MergeGapSeconds: 0, PreRollSeconds: .5, PostRollSeconds: .25)));
        model.Sensitivity = 1;
        int callsBefore = model.AnalysisBackendCallCount;
        await model.AnalyzeAsync(Ymm4FfmpegLocator.CreateBackend());
        check("w1r_actual_hit_anchors", model.Candidates.Select(c => c.AnchorSourceTime).SequenceEqual(new[] { 2d, 6, 10, 14, 18, 22 })
            && model.Candidates.All(c => c.Source.Start < c.AnchorSourceTime));
        check("w1r_one_analysis", model.AnalysisBackendCallCount == callsBefore + 1 && model.DecodedRangeCount == 1);
        int decodeCount = model.AnalysisBackendCallCount;
        var identities = model.Candidates.Select(c => c.Identity).ToArray();
        Candidate At(double anchor) => model.Candidates.Single(c => c.AnchorSourceTime == anchor);
        void Select(VideoItem item) => timeline.SelectedItems = ImmutableList.Create<IItem>(item);
        VideoItem[] Pieces() => timeline.Items.OfType<VideoItem>().Where(v => v.Remark == "NAV_W1R" && v.Layer == 12).OrderBy(v => v.ContentOffset).ToArray();
        int Expected(VideoItem item, double anchor) => item.Frame + (int)Math.Ceiling((anchor - item.ContentOffset.TotalSeconds) * fps - 1e-9);
        var split = typeof(Timeline).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == "SplitSelectedAndGroupedItems" && m.GetParameters().Length == 2);
        object splitNone = Enum.Parse(split.GetParameters()[1].ParameterType, "None");
        var trim = typeof(Timeline).GetMethod("ChangeSelectedAndGroupedItemsLength") ?? throw new MissingMethodException("Trim");
        object neighborNone = Enum.Parse(trim.GetParameters()[2].ParameterType, "None");
        var move = typeof(Timeline).GetMethod("MoveSelectedAndGroupedItems") ?? throw new MissingMethodException("Move");
        var copy = typeof(Timeline).GetMethod("CopySelectedAndGroupedItems") ?? throw new MissingMethodException("Copy");
        var paste = typeof(Timeline).GetMethod("PasteCopiedItemsAsync") ?? throw new MissingMethodException("Paste");
        var trace = new List<object>(); bool completed = false;
        void Record(string stage) => trace.Add(new { stage, playhead = timeline.CurrentFrame, decodeCalls = model.AnalysisBackendCallCount,
            candidates = model.Candidates.Select(c => new { c.AnchorSourceTime, c.Frame, c.Available, c.Visited, c.UnavailableReason }).ToArray() });
        string ffmpeg = Ymm4FfmpegLocator.Resolve().FfmpegPath;
        int unhandled = 0;
        DispatcherUnhandledExceptionEventHandler listener = (_, _) => unhandled++;
        Application.Current.DispatcherUnhandledException += listener;
        File.Move(ffmpeg, ffmpeg + ".w1r-disabled");
        try
        {
            model.Selected = null; model.NextCommand.Execute(null);
            check("w1r_initial_jump", model.Selected == At(2) && timeline.CurrentFrame == Expected(root, 2) && At(2).Visited);
            Record("initial");
            Select(root); split.Invoke(timeline, [root.Frame + 8 * fps, splitNone]);
            var pieces = Pieces();
            check("w1r_real_split", pieces.Length == 2 && !timeline.Items.Any(v => ReferenceEquals(v, root)));
            var left = pieces[0]; var right = pieces[1];
            model.NextCommand.Execute(null);
            check("w1r_next_after_split", model.Selected == At(6) && timeline.CurrentFrame == Expected(left, 6));
            Record("split");
            Select(right); trim.Invoke(timeline, [3 * fps, true, neighborNone]);
            model.NextCommand.Execute(null);
            check("w1r_trim_skips_only_missing_anchor", model.Selected == At(14) && !At(10).Available && At(18).Available
                && model.Candidates.Count == 6 && timeline.CurrentFrame == Expected(right, 14));
            Record("head-trim");
            Select(right); move.Invoke(timeline, [100 - right.Frame, 0, 0]);
            model.NextCommand.Execute(null);
            check("w1r_moved_piece_current_frame", right.Frame == 100 && model.Selected == At(18) && timeline.CurrentFrame == Expected(right, 18));
            check("w1r_stable_queue_and_visited", model.Candidates.Select(c => c.Identity).SequenceEqual(identities) && At(2).Visited
                && At(18).Frame < At(6).Frame);
            Record("move");
            Select(right); copy.Invoke(timeline, null); await Task.Delay(150);
            await (Task)(paste.Invoke(timeline, [10000, 13]) ?? throw new InvalidOperationException("Paste task missing."));
            await Task.Delay(200);
            var duplicate = timeline.Items.OfType<VideoItem>().Single(v => v.Remark == "NAV_W1R" && v.Layer == 13);
            check("w1r_real_copy_same_source_range", !ReferenceEquals(duplicate, right) && duplicate.FilePath == right.FilePath
                && duplicate.ContentOffset == right.ContentOffset && duplicate.Length == right.Length && duplicate.Frame != right.Frame);
            model.NextCommand.Execute(null);
            check("w1r_copy_never_chosen", model.Selected == At(22) && timeline.CurrentFrame == Expected(right, 22) && timeline.CurrentFrame != Expected(duplicate, 22));
            Record("copy");
            var events = new List<UndoRedoEventArgs>();
            EventHandler<UndoRedoEventArgs> collect = (_, e) => events.Add(e);
            Select(right); timeline.UndoRedoCommandCreated += collect;
            try { split.Invoke(timeline, [Expected(right, 20), splitNone]); }
            finally { timeline.UndoRedoCommandCreated -= collect; }
            var command = FindCommand(events) ?? throw new InvalidOperationException("Split UndoRedo command missing.");
            model.Selected = At(18); model.NextCommand.Execute(null);
            check("w1r_second_split_next", Pieces().Length == 3 && model.Selected == At(22) && timeline.CurrentFrame == Expected(Pieces()[2], 22));
            await Undo(command); await Task.Delay(120);
            model.Selected = At(18); model.NextCommand.Execute(null);
            pieces = Pieces();
            check("w1r_undo_semantic_next", pieces.Length == 2 && pieces[1].ContentOffset.TotalSeconds == 11 && pieces[1].Length == 13 * fps
                && model.Selected == At(22) && timeline.CurrentFrame == Expected(pieces[1], 22));
            Record("undo");
            await Redo(command); await Task.Delay(120);
            model.Selected = At(18); model.NextCommand.Execute(null);
            pieces = Pieces();
            check("w1r_redo_semantic_next", pieces.Length == 3 && pieces[2].ContentOffset.TotalSeconds == 20 && pieces[2].Length == 4 * fps
                && model.Selected == At(22) && timeline.CurrentFrame == Expected(pieces[2], 22));
            Record("redo");
            var middle = pieces[1]; var last = pieces[2];
            Select(last); trim.Invoke(timeline, [-3 * fps, false, neighborNone]);
            int beforeFailure = timeline.CurrentFrame; model.JumpCommand.Execute(null);
            check("w1r_unavailable_jump_guarded", !At(22).Available && timeline.CurrentFrame == beforeFailure && model.Status.Contains("トリミング", StringComparison.Ordinal));
            Select(left); trim.Invoke(timeline, [-7 * fps, false, neighborNone]);
            Select(middle); trim.Invoke(timeline, [8 * fps, true, neighborNone]);
            model.Selected = null; model.NextCommand.Execute(null);
            check("w1r_all_unavailable_not_copy", model.Candidates.Count == 6 && model.Candidates.All(c => !c.Available)
                && model.Status.Contains("現在移動できる候補がありません", StringComparison.Ordinal) && timeline.CurrentFrame == beforeFailure
                && timeline.Items.Any(v => ReferenceEquals(v, duplicate)));
            Record("all-unavailable");
            await model.RequeryAsync();
            check("w1r_requery_retains_index_order_visited", model.Candidates.Select(c => c.Identity).SequenceEqual(identities)
                && model.Candidates.All(c => !c.Available) && At(2).Visited && model.DecodedRangeCount == 1);
            check("w1r_zero_redecode_after_edits", !File.Exists(ffmpeg) && model.AnalysisBackendCallCount == decodeCount);
            check("w1r_source_bytes_unchanged", sourceHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(media))));
            check("w1r_no_unhandled_normal_failure", unhandled == 0);
            completed = true;
        }
        finally
        {
            if (File.Exists(ffmpeg + ".w1r-disabled")) File.Move(ffmpeg + ".w1r-disabled", ffmpeg);
            Application.Current.DispatcherUnhandledException -= listener;
            File.WriteAllText(Path.Combine(output, "rebinding-summary.json"), JsonSerializer.Serialize(new { schema = "navigator.rebinding-native.v1",
                checkout = Environment.GetEnvironmentVariable("GITHUB_SHA"), completed, fps, sourceHash, callsBefore, decodeCount,
                callsAfter = model.AnalysisBackendCallCount, unhandled, trace }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    // Same bounded emitted-command traversal adopted from Lab df92cf6. Not a product dependency or an input-route proof.
    private static object? FindCommand(IEnumerable<UndoRedoEventArgs> events)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        object? Walk(object? value, int depth)
        {
            if (value is null || depth > 4 || !seen.Add(value)) return null;
            if (value is IUndoRedoCommand or IUndoRedoAsyncCommand) return value;
            if (value is string || value.GetType().IsPrimitive) return null;
            if (value is IEnumerable sequence)
            { foreach (var child in sequence) { var found = Walk(child, depth + 1); if (found != null) return found; } return null; }
            foreach (var member in value.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object? child;
                try { child = member switch { PropertyInfo p when p.GetIndexParameters().Length == 0 => p.GetValue(value), FieldInfo f => f.GetValue(value), _ => null }; }
                catch { continue; }
                var found = Walk(child, depth + 1); if (found != null) return found;
            }
            return null;
        }
        foreach (var e in events) { var found = Walk(e, 0); if (found != null) return found; }
        return null;
    }
    private static async Task Undo(object command)
    {
        if (command is IUndoRedoAsyncCommand a) await a.UndoAsync();
        else if (command is IUndoRedoCommand s) s.Undo();
        else throw new NotSupportedException("Unsupported Undo command.");
    }
    private static async Task Redo(object command)
    {
        if (command is IUndoRedoAsyncCommand a) await a.RedoAsync();
        else if (command is IUndoRedoCommand s) s.Redo();
        else throw new NotSupportedException("Unsupported Redo command.");
    }
}
