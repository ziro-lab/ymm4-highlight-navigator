using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ymm4HighlightNavigator.Core;
using Ymm4HighlightNavigator.Plugin;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace Ymm4HighlightNavigator.Plugin.Tests;

// This separate fixture assembly is never shipped with the product.
public sealed class ProofEntry : ILocalizePlugin
{
    public string Name => "Navigator native product proof";
    public void SetCulture(CultureInfo cultureInfo) => Proof.Schedule();
}
internal static class Proof
{
    private static bool scheduled;
    private static string output = "";
    private static readonly List<object> assertions = [];
    private static readonly HashSet<string> menuTrace = [];
    internal static void Schedule()
    {
        string? directory = Environment.GetEnvironmentVariable("NAV_NATIVE_OUTPUT");
        if (scheduled || string.IsNullOrWhiteSpace(directory)) return;
        scheduled = true; output = Path.GetFullPath(directory); Directory.CreateDirectory(output);
        Application.Current.Dispatcher.BeginInvoke(new Action(Start));
    }
    private static void Start()
    {
        int ticks = 0; bool created = false, opened = false;
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += async (_, _) =>
        {
            try
            {
                ticks++;
                var main = Application.Current.Windows.Cast<Window>().Select(w => w.DataContext).FirstOrDefault(v => v?.GetType().FullName == "YukkuriMovieMaker.ViewModels.MainViewModel");
                var active = main?.GetType().GetProperty("ActiveTimelineViewModel")?.GetValue(main);
                if (main != null && active == null && !created) { created = true; main.GetType().GetMethod("CreateProject", Type.EmptyTypes)?.Invoke(main, null); }
                if (main != null && active != null && !opened) opened = OpenTool(main);
                var view = Application.Current.Windows.Cast<Window>().SelectMany(Descendants).OfType<NavigatorView>().FirstOrDefault();
                if (ticks is 1 or 5 or 20 or 60 or 120 or 180) TraceStartup(ticks, opened, main, active);
                if (view?.DataContext is NavigatorModel model && active != null)
                {
                    var timeline = active.GetType().GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(active) as Timeline;
                    if (timeline != null)
                    {
                        timer.Stop(); await RunAsync(view, model, timeline); Write(true, null); return;
                    }
                }
                if (ticks >= 200) throw new TimeoutException("Real product Tool was not discovered.");
            }
            catch (Exception ex) { timer.Stop(); Write(false, ex.ToString()); }
        };
        timer.Start();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var pending = new Queue<DependencyObject>();
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(root);
        while (pending.TryDequeue(out var node))
        {
            if (!seen.Add(node)) continue;
            if (seen.Count > 10000) throw new InvalidOperationException("Host tree exceeds diagnostic budget.");
            yield return node;
            if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) pending.Enqueue(VisualTreeHelper.GetChild(node, i));
            foreach (var child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject d) pending.Enqueue(d);
        }
    }
    private static void TraceStartup(int ticks, bool opened, object? main, object? active)
    {
        var windows = Application.Current.Windows.Cast<Window>().ToArray();
        var lines = new List<string> { $"tick={ticks} opened={opened} main={main?.GetType().FullName} active={active?.GetType().FullName}" };
        foreach (var window in windows)
        {
            lines.Add($"WINDOW {window.GetType().FullName} title={window.Title} visible={window.IsVisible} dc={window.DataContext?.GetType().FullName}");
            foreach (var element in Descendants(window).OfType<FrameworkElement>().Where(x => x.GetType().FullName?.Contains("Navigator", StringComparison.Ordinal) == true || x.DataContext?.GetType().FullName?.Contains("Navigator", StringComparison.Ordinal) == true).Take(30))
                lines.Add($"VIEW {element.GetType().FullName} dc={element.DataContext?.GetType().FullName} typedView={element is NavigatorView} typedModel={element.DataContext is NavigatorModel} visible={element.IsVisible} loaded={element.IsLoaded} size={element.ActualWidth}x{element.ActualHeight}");
        }
        if (active != null)
            lines.Add("ACTIVE_FIELDS " + string.Join(",", active.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => f.FieldType == typeof(Timeline)).Select(f => f.Name + ":" + f.FieldType.FullName)));
        lines.Add("ASSEMBLIES " + string.Join(" | ", AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.Contains("Navigator", StringComparison.Ordinal) == true).Select(a => a.FullName + ":" + a.Location)));
        File.AppendAllLines(Path.Combine(output, "startup.txt"), lines);
    }
    private static bool OpenTool(object main)
    {
        static object? Read(object item, string name) => item.GetType().GetProperty(name)?.GetValue(item);
        static string Text(object? value) => value is string text ? text : value == null ? "" : (Read(value, "Value")?.ToString() ?? value.ToString() ?? "");
        bool Visit(object item, int depth)
        {
            if (depth > 6) return false;
            var type = item.GetType();
            string label = string.Join(" | ", new[] { "Header", "Title", "Name" }.Select(n => Text(Read(item, n))));
            bool ownTool = label.Contains("YMM4見どころナビ", StringComparison.Ordinal);
            foreach (string member in new[] { "Plugin", "Tool", "ViewModel", "ToolViewModel", "Model" })
            {
                var value = Read(item, member);
                ownTool |= value is NavigatorModel || value is NavigatorPlugin;
            }
            if (menuTrace.Add(type.FullName + ":" + label))
                File.AppendAllText(Path.Combine(output, "startup.txt"), "MENU " + type.FullName + ":" + label + "; members=" + string.Join(",", type.GetProperties().Select(p => p.Name + ":" + p.PropertyType.Name)) + "\n");
            if (ownTool)
            {
                foreach (string name in new[] { "ShowCommand", "OpenCommand", "ActivateCommand", "ShowToolCommand", "ShowWindowCommand", "ToggleVisibleCommand", "ToggleVisibilityCommand", "Command" })
                {
                    if (Read(item, name) is not ICommand command) continue;
                    var parameter = Read(item, "CommandParameter");
                    if (!command.CanExecute(parameter)) continue;
                    command.Execute(parameter);
                    File.AppendAllText(Path.Combine(output, "startup.txt"), "OPENED " + name + "\n");
                    return true;
                }
                // Host-owned registered Tool only; never construct a replacement product model to pass a test.
                var visible = type.GetProperty("IsVisible");
                if (visible?.PropertyType == typeof(bool) && visible.SetMethod?.IsPublic == true)
                {
                    visible.SetValue(item, true);
                    File.AppendAllText(Path.Combine(output, "startup.txt"), "OPENED public IsVisible\n");
                    return true;
                }
            }
            foreach (var property in new[] { "Items", "Children", "MenuItems" })
                if (Read(item, property) is IEnumerable children)
                    foreach (var child in children) if (child != null && Visit(child, depth + 1)) return true;
            return false;
        }
        if (Read(main, "ToolMenuItems") is not IEnumerable roots) return false;
        foreach (var root in roots) if (root != null && Visit(root, 0)) return true;
        return false;
    }
    private static async Task RunAsync(NavigatorView view, NavigatorModel model, Timeline timeline)
    {
        Check("real_product_tool", model.CaptureCommand.CanExecute(null));
        string media = Environment.GetEnvironmentVariable("NAV_NATIVE_MEDIA") ?? throw new InvalidOperationException("No fixture.");
        string tools = Path.Combine(Path.GetDirectoryName(typeof(NavigatorPlugin).Assembly.Location)!, "tools");
        string ffmpeg = Path.Combine(tools, "ffmpeg.exe"), ffprobe = Path.Combine(tools, "ffprobe.exe");
        string beforeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(media)));
        int fps = timeline.VideoInfo.FPS;
        var a = new VideoItem { FilePath = media, Frame = 137, Length = 2 * fps, Layer = 1, ContentOffset = TimeSpan.FromSeconds(1), Remark = "NAV_PROOF_A" };
        var b = new VideoItem { FilePath = media, Frame = 733, Length = 4 * fps, Layer = 2, ContentOffset = TimeSpan.FromSeconds(2), Remark = "NAV_PROOF_B" };
        a.PlaybackRate2.SetFirstValue(200); b.PlaybackRate2.SetFirstValue(50);
        a.PlaybackRate2.SetAnimationParameters(a.Length, fps); b.PlaybackRate2.SetAnimationParameters(b.Length, fps);
        if (!timeline.TryAddItems([a], a.Frame, a.Layer) || !timeline.TryAddItems([b], b.Frame, b.Layer)) throw new InvalidOperationException("Fixture insertion failed.");
        string Signature() => JsonSerializer.Serialize(new[] { a, b }.Select(x => new { x.FilePath, x.Frame, x.Length, x.Layer, offset = x.ContentOffset.Ticks, x.Remark }));
        string beforeState = Signature();
        timeline.SelectedItems = ImmutableList.Create<IItem>(a, b);
        model.CaptureSelection();
        Check("capture_two_occurrences", model.Targets.Length == 2 && model.Targets.Select(t => t.Id).Distinct().Count() == 2);
        Check("same_source_different_rates", model.Targets.Select(t => t.SourceKey).Distinct().Count() == 1 && model.Targets.Any(t => t.RatePercent == 200) && model.Targets.Any(t => t.RatePercent == 50));
        var adapter = new TargetAdapter(); adapter.Attach(timeline); adapter.CaptureSelection();
        var first = adapter.Snapshots.Single(t => t.StartFrame == a.Frame);
        int? projected = adapter.Project(first.Id, new(1.5173, 1.7));
        Check("native_map_fractional_projection", projected == 153);
        Check("exclusive_end_no_jump", adapter.Project(first.Id, new(5, 5.1)) == null);
        var ids = model.Targets.Select(t => t.Id).ToArray();
        timeline.SelectedItems = ImmutableList<IItem>.Empty;
        Check("selection_independent_targets", model.Targets.Select(t => t.Id).SequenceEqual(ids));
        bool rejected = false;
        try { model.CaptureSelection(); } catch (InvalidOperationException) { rejected = true; }
        Check("empty_capture_atomic", rejected && model.Targets.Select(t => t.Id).SequenceEqual(ids));
        int heartbeats = 0;
        var heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        heartbeat.Tick += (_, _) => heartbeats++;
        heartbeat.Start();
        try { await model.AnalyzeAsync(new(ffmpeg, ffprobe)); }
        finally { heartbeat.Stop(); }
        Check("background_ui_responsive", heartbeats > 0);
        Check("overlap_decoded_once", model.DecodedRangeCount == 1);
        Check("source_to_occurrences", model.Candidates.Select(c => c.TargetId).Distinct().Count() == 2);
        Check("chronological_queue", model.Candidates.Select(c => c.Frame).SequenceEqual(model.Candidates.Select(c => c.Frame).Order()));
        model.Selected = null; model.Move(1);
        Check("next_jump_exact", model.Selected != null && timeline.CurrentFrame == model.Selected.Frame);
        var remembered = model.Selected!;
        File.Move(ffmpeg, ffmpeg + ".disabled");
        try
        {
            foreach (var p in model.Profiles) p.Enabled = false;
            await model.RequeryAsync();
            Check("all_profiles_off", model.Candidates.Count == 0 && model.CandidateSummary.Contains("ヒット計 0", StringComparison.Ordinal));
            model.Profiles[0].Enabled = true; await model.RequeryAsync();
            Check("toggle_without_decoder", model.Candidates.Count > 0 && model.DecodedRangeCount == 1);
            model.Sensitivity = 1.5; await model.RequeryAsync();
            Check("sensitivity_without_decoder", model.Candidates.Count > 0);
            model.Sensitivity = 1; foreach (var p in model.Profiles) p.Enabled = true; await model.RequeryAsync();
        }
        finally { File.Move(ffmpeg + ".disabled", ffmpeg); }
        Check("visited_survives_requery", model.Candidates.Any(c => c.TargetId == remembered.TargetId && c.Source == remembered.Source && c.Visited));
        Check("item_state_unchanged", beforeState == Signature());
        Check("source_bytes_unchanged", beforeHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(media))));
        model.Selected = model.Candidates.First(c => c.TargetId == remembered.TargetId);
        int beforeFrame = timeline.CurrentFrame;
        a.Frame++;
        rejected = false;
        try { model.JumpSelected(); } catch (InvalidOperationException) { rejected = true; }
        finally { a.Frame--; }
        Check("stale_target_rejected_atomically", rejected && timeline.CurrentFrame == beforeFrame);
        adapter.Attach(null);
        Check("timeline_detach_invalidates", adapter.Snapshots.IsEmpty);
        var narrow = new NavigatorView { DataContext = model, Width = 360, Height = 480 };
        narrow.Measure(new Size(360, 480)); narrow.Arrange(new Rect(0, 0, 360, 480)); narrow.UpdateLayout();
        bool Inside(string name)
        {
            if (narrow.FindName(name) is not FrameworkElement control || control.ActualWidth <= 0 || control.ActualHeight <= 0) return false;
            Point p = control.TranslatePoint(new Point(), narrow);
            return p.X >= 0 && p.Y >= 0 && p.X + control.ActualWidth <= narrow.ActualWidth + .5 && p.Y + control.ActualHeight <= narrow.ActualHeight + .5;
        }
        Check("narrow_primary_controls_contained", new[] { "CaptureButton", "AnalyzeButton", "PreviousButton", "NextButton", "CandidateList" }.All(Inside));
        var bitmap = new RenderTargetBitmap(360, 480, 96, 96, PixelFormats.Pbgra32); bitmap.Render(narrow);
        using (var image = File.Create(Path.Combine(output, "navigator-360x480.png"))) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); png.Save(image); }
        File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new { fps, heartbeats, decodedRanges = model.DecodedRangeCount, candidates = model.Candidates.Count, counts = model.CandidateSummary, hostAssembly = typeof(VideoItem).Assembly.FullName, productHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NavigatorPlugin).Assembly.Location))) }, new JsonSerializerOptions { WriteIndented = true }));
        model.Dispose(); Check("dispose_clears_targets", model.Targets.IsEmpty && model.Candidates.Count == 0);
    }
    private static void Check(string id, bool passed)
    {
        assertions.Add(new { id, passed });
        File.AppendAllText(Path.Combine(output, "assertions.txt"), $"ASSERT {(passed ? "PASS" : "FAIL")} {id}\n");
        if (!passed) throw new InvalidOperationException("Assertion failed: " + id);
    }
    private static void Write(bool passed, string? error)
    {
        if (!passed && File.Exists(Path.Combine(output, "startup.txt"))) error += "\nSTARTUP\n" + File.ReadAllText(Path.Combine(output, "startup.txt"));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { schema = "navigator.native.v1", passed, checkout = Environment.GetEnvironmentVariable("GITHUB_SHA"), assertions, error }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
