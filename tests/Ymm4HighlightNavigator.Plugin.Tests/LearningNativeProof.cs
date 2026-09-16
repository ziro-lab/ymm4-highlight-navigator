using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ymm4HighlightNavigator.Core;
using Ymm4HighlightNavigator.Plugin;
using YukkuriMovieMaker.Project;

namespace Ymm4HighlightNavigator.Plugin.Tests;

internal static class LearningNativeProof
{
    internal static async Task RunAsync(NavigatorModel navigator, Timeline timeline, string media, string output, Action<string, bool> check)
    {
        navigator.OpenLearningCommand.Execute(null);
        var window = Application.Current.Windows.Cast<Window>().SingleOrDefault(w => w.Title == "動画からフィルターを作る");
        var view = window?.Content as LearningView;
        var learning = view?.DataContext as LearningModel;
        check("learning_surface_opened", window is { IsVisible: true } && learning != null);
        if (window == null || learning == null) throw new InvalidOperationException("The real authoring surface was not created.");
        check("learning_controls_idle", !learning.ImportCommand.CanExecute(null) && !learning.PreviewCommand.CanExecute(null) && !learning.SaveCommand.CanExecute(null));
        var store = NavigatorModel.UserCorpus();
        check("learning_test_corpus_fresh", store.Read().Samples.IsEmpty && new FilterStore(store).ReadAll().IsEmpty);
        string input = Path.Combine(output, "teaching-input"); Directory.CreateDirectory(input);
        string one = Path.Combine(input, "切り替わり1.mkv"), two = Path.Combine(input, "切り替わり2.mkv");
        File.Copy(media, one);
        var paths = Ymm4FfmpegLocator.Resolve();
        await ChildProcess.CaptureAsync(paths.FfmpegPath, ["-nostdin", "-v", "error", "-i", media, "-t", "6", "-map", "0", "-c", "copy", two], TimeSpan.FromSeconds(20), default);
        string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        string oneHash = Hash(one), twoHash = Hash(two);
        var label = new LearningLabel("検証用", "場面切り替わり");
        learning.SetFiles([one, two], label);
        Task importing = learning.ImportAsync(Ymm4FfmpegLocator.CreateBackend());
        check("learning_busy_actions_disabled", learning.IsBusy && !learning.ImportCommand.CanExecute(null) && !learning.ChooseFilesCommand.CanExecute(null) && learning.CancelCommand.CanExecute(null));
        await importing;
        // Keep the actual operation result even when the following assertion fails.
        File.WriteAllText(Path.Combine(output, "learning-summary.json"), JsonSerializer.Serialize(new { stage = "intake", learning.Status, learning.LastImport, rows = learning.Rows.ToArray(), corpusRoot = store.Root }, new JsonSerializerOptions { WriteIndented = true }));
        if (learning.LastImport is not { Committed: 2, Failed: 0, Cancelled: 0 })
            throw new InvalidOperationException("Native intake failed: " + learning.Status + "\n" + JsonSerializer.Serialize(learning.LastImport));
        check("learning_batch_committed", store.Read().Samples.Length == 2);
        check("learning_source_preserved", Hash(one) == oneHash && Hash(two) == twoHash);
        check("learning_import_does_not_train", new FilterStore(store).ReadAll().IsEmpty);
        await learning.CreateDraftAsync();
        if (learning.Draft == null) throw new InvalidOperationException("Draft creation failed: " + learning.Status);
        check("learning_draft_created", learning.Draft is { Coverage.Covered: 2 } && learning.Draft.Filter.Patterns.Length > 0 && learning.SaveCommand.CanExecute(null));
        learning.Preview(); await navigator.RequeryAsync();
        check("learning_preview_not_persistent", navigator.Profiles.Any(p => p.Learned is { Revision: 0 }) && new FilterStore(store).ReadAll().IsEmpty);
        foreach (var p in navigator.Profiles) p.Enabled = p.Learned != null;
        await navigator.RequeryAsync();
        check("learning_preview_runtime_hit", navigator.Candidates.Count > 0 && navigator.Candidates.All(c => c.Profiles.All(n => n.Contains("検証用", StringComparison.Ordinal))));
        await learning.SaveAsync();
        var saved = new FilterStore(store).Read(label);
        if (saved == null) throw new InvalidOperationException("Filter save failed: " + learning.Status);
        check("learning_save_persisted", learning.LastSaved is { Revision: 1 } && saved is { Revision: 1 } && navigator.Profiles.Any(p => p.Learned is { Revision: 1 }));
        // Delete ONLY generated test-owned copies, never the fixture referenced by live Timeline Items.
        File.Delete(one); File.Delete(two); Directory.Delete(input);
        File.Move(paths.FfmpegPath, paths.FfmpegPath + ".learning-disabled");
        try
        {
            await learning.CreateDraftAsync();
            check("learning_raw_free_reauthor", learning.Draft is { Coverage.Covered: 2 } && !File.Exists(one) && !File.Exists(two));
            await navigator.ReloadSavedFiltersAsync();
            check("learned_filter_reloads", navigator.Profiles.Count(p => p.Learned is { Revision: 1 }) == 1);
            foreach (var p in navigator.Profiles) p.Enabled = p.Learned != null;
            await navigator.RequeryAsync();
            check("learned_query_without_decoder", navigator.Candidates.Count > 0 && navigator.DecodedRangeCount == 1);
            navigator.Sensitivity = .5; await navigator.RequeryAsync(); int narrowCount = navigator.Candidates.Count;
            navigator.Sensitivity = 2; await navigator.RequeryAsync();
            check("learned_sensitivity_without_decoder", navigator.Candidates.Count > 0 && navigator.DecodedRangeCount == 1);
            navigator.Sensitivity = 1; await navigator.RequeryAsync(); navigator.Selected = null; navigator.Move(1);
            check("learned_filter_jump", navigator.Selected != null && timeline.CurrentFrame == navigator.Selected.Frame);
            File.WriteAllText(Path.Combine(output, "learning-summary.json"), JsonSerializer.Serialize(new
            {
                samples = store.Read().Samples.Length, patterns = saved.Patterns.Length,
                referenceCandidateSamples = learning.Draft!.Coverage.Covered,
                narrowReviewCount = narrowCount, baselineReviewCount = navigator.Candidates.Count,
                rawVideoPresent = false, semanticRecallMeasured = false, filterRevision = saved.Revision
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { File.Move(paths.FfmpegPath + ".learning-disabled", paths.FfmpegPath); }
        check("learning_layout_contained", Capture(learning, output, 640, 640) && Capture(learning, output, 400, 640));
        string bad = Path.Combine(output, "invalid-teaching.mp4"); File.WriteAllText(bad, "not a recording");
        learning.SetFiles([bad], label); await learning.ImportAsync(Ymm4FfmpegLocator.CreateBackend());
        check("learning_error_does_not_escape", learning.LastImport is { Failed: 1, Committed: 0 } && new FilterStore(store).Read(label)!.Revision == 1 && File.ReadAllText(bad) == "not a recording");
        File.Delete(bad); window.Close(); await navigator.ReloadSavedFiltersAsync();
        check("learning_close_preserves_review", navigator.Targets.Length == 2 && navigator.Profiles.Any(p => p.Learned is { Revision: 1 }) && !Application.Current.Dispatcher.HasShutdownStarted);
    }

    private static bool Capture(LearningModel model, string output, int width, int height)
    {
        var view = new LearningView { DataContext = model, Width = width, Height = height };
        var surface = new Border { Background = SystemColors.WindowBrush, Child = view, Width = width, Height = height };
        surface.Measure(new Size(width, height)); surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
        bool Inside(string name)
        {
            if (view.FindName(name) is not FrameworkElement element || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
            Point p = element.TranslatePoint(new Point(), view);
            return p.X >= 0 && p.Y >= 0 && p.X + element.ActualWidth <= width + .5 && p.Y + element.ActualHeight <= height + .5;
        }
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(surface);
        using (var file = File.Create(Path.Combine(output, $"learning-{width}x{height}.png")))
        { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); png.Save(file); }
        return new[] { "ChooseFolderButton", "ImportButton", "CreateFilterButton", "LearningResults", "TryFilterButton", "SaveFilterButton" }.All(Inside);
    }
}
