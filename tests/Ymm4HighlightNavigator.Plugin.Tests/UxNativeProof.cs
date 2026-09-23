using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ymm4HighlightNavigator.Core;
using Ymm4HighlightNavigator.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace Ymm4HighlightNavigator.Plugin.Tests;

internal static class UxNativeProof
{
    internal static async Task RunAsync(NavigatorModel model, Timeline timeline, string output, Action<string, bool> check)
    {
        string Signature() => JsonSerializer.Serialize(timeline.Items.OfType<VideoItem>().Select(v => new { v.FilePath, v.Frame, v.Length, v.Layer, v.ContentOffset, v.Remark }));
        string before = Signature(); int decodeCalls = model.AnalysisBackendCallCount;
        await model.ReloadReviewSettingsAsync();
        check("ux_settings_initial_load", model.ReviewSets.Count == 1 && model.ReviewSets[0].IsBuiltIn);
        check("ux_generic_basic_catalog",
            GenericFilterCatalog.Basic.All(g => model.Profiles.Any(p => p.Generic?.Id == g.Id))
            && ReviewBuiltIns.Basic.Configuration.Filters.Select(f => f.FilterId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(GenericFilterCatalog.Basic.Select(g => g.Id)));
        check("ux_legacy_seed_compatibility_off_default",
            model.Profiles.Where(p => p.Profile.Id.StartsWith("seed.", StringComparison.Ordinal)).Count() == 3
            && model.Profiles.Where(p => p.Profile.Id.StartsWith("seed.", StringComparison.Ordinal)).All(p => !p.Enabled && p.Group == "旧互換"));
        check("ux_view_intent_initial_path_and_summary", model.ViewIntents.Count == 1
            && model.SelectedViewIntent?.DisplayPath == "汎用 > 基本"
            && model.ViewIntentSummary.Contains("大きな場面切替", StringComparison.Ordinal)
            && model.ViewIntentSummary.Contains("3フィルター", StringComparison.Ordinal));
        check("ux_builtin_overwrite_disabled", !model.OverwriteReviewSetCommand.CanExecute(null));
        model.ApplyReviewSet(ReviewBuiltIns.Basic); await model.RequeryAsync();
        var sceneGeneric = model.Profiles.Single(p => p.Profile.Id == GenericFilterCatalog.LargeSceneChange.Id);
        var darkGeneric = model.Profiles.Single(p => p.Profile.Id == GenericFilterCatalog.DarkFade.Id);
        var activityGeneric = model.Profiles.Single(p => p.Profile.Id == GenericFilterCatalog.QuietToActivity.Id);
        check("ux_generic_real_fixture_behavior",
            sceneGeneric.Count != "0件" && darkGeneric.Count != "0件" && activityGeneric.Count == "0件"
            && model.Candidates.Any(c => c.Profiles.Any(n => n.Contains("大きな場面切替", StringComparison.Ordinal))));
        darkGeneric.Enabled = false;
        model.Sensitivity = 1.25; await model.RequeryAsync();
        var working = model.CurrentReviewConfiguration;
        check("ux_saved_working_separated", model.ReviewStateText.Contains("変更あり", StringComparison.Ordinal) && ReviewBuiltIns.Basic.Configuration.Sensitivity == 1 && ReviewBuiltIns.Basic.Configuration.IsEnabled(GenericFilterCatalog.DarkFade.Id));
        check("ux_view_intent_modified_state", model.HasViewIntentChanges && model.FilterQuickSummary.StartsWith("フィルター 2/", StringComparison.Ordinal));
        model.SetName = "検証セットA"; await model.SaveReviewSetAsync(false);
        var one = model.ReviewSets.Single(s => s.Name == "検証セットA");
        check("ux_set_saved_and_selected", one.Configuration.EquivalentTo(working) && model.SelectedReviewSet?.Id == one.Id && !model.ReviewStateText.Contains("変更あり", StringComparison.Ordinal));
        check("ux_view_intent_save_inherits_classification", one.ClassificationPath.SequenceEqual(new[] { "汎用" })
            && model.SelectedViewIntent?.DisplayPath == "汎用 > 検証セットA");
        model.SetName = "検証セットB"; await model.SaveReviewSetAsync(false);
        var two = model.ReviewSets.Single(s => s.Name == "検証セットB");
        check("ux_set_duplicate_independent", one.Id != two.Id && one.Configuration.EquivalentTo(two.Configuration));
        model.Sensitivity = 1.5; await model.RequeryAsync();
        var modified = model.CurrentReviewConfiguration;
        model.ApplyReviewSet(ReviewBuiltIns.Basic); await model.RequeryAsync();
        model.RestoreReviewSettings(); await model.RequeryAsync();
        check("ux_set_switch_restore", model.CurrentReviewConfiguration.EquivalentTo(modified) && model.SelectedReviewSet?.Id == two.Id && !model.RestoreReviewSettingsCommand.CanExecute(null));
        model.SetName = "検証セットB"; await model.SaveReviewSetAsync(true);
        check("ux_explicit_overwrite", NavigatorModel.UserReviewSettings().Read().Sets.Single(s => s.Id == two.Id).Configuration.EquivalentTo(modified));
        model.Sensitivity = 1.75; await model.RequeryAsync();
        var unchangedByLoad = model.CurrentReviewConfiguration;
        await model.ReloadReviewSettingsAsync();
        check("ux_reload_preserves_working", model.CurrentReviewConfiguration.EquivalentTo(unchangedByLoad));
        model.SetName = "保存再試行";
        var store = NavigatorModel.UserReviewSettings(); long revision = store.Read().Revision;
        using (var hold = new FileStream(Path.Combine(store.Root, ".writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await model.SaveReviewSetAsync(false);
            check("ux_save_failure_preserves_working", store.Read().Revision == revision && model.CurrentReviewConfiguration.EquivalentTo(unchangedByLoad) && model.SetName == "保存再試行" && model.ReviewStatus.Contains("保存できません", StringComparison.Ordinal));
        }
        await model.SaveReviewSetAsync(false);
        check("ux_save_retry_succeeds", store.Read().Revision == revision + 1 && model.ReviewSets.Any(s => s.Name == "保存再試行"));

        var managementWorking = model.CurrentReviewConfiguration;
        model.ManagedViewIntent = model.ViewIntents.Single(v => v.Set.Id == ReviewBuiltIns.Basic.Id);
        model.OpenViewIntentManagerCommand.Execute(null);
        var intentManager = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "見たいものを整理");
        check("ux_view_intent_manager_layout", intentManager.Content is ViewIntentManagerView
            && Capture(new ViewIntentManagerView { DataContext = model }, output, "view-intent-manager", 420, 560,
                ["ViewIntentManagementList", "DuplicateViewIntentButton", "ManagedViewIntentNameBox", "ManagedViewIntentClassificationBox", "SaveViewIntentMetadataButton"]));
        intentManager.Close();
        await model.DuplicateManagedViewIntentAsync();
        var copiedIntent = model.ManagedViewIntent?.Set ?? throw new InvalidOperationException("Duplicated view intent was not selected.");
        check("ux_view_intent_duplicate_preserves_working", !copiedIntent.IsBuiltIn
            && copiedIntent.ClassificationPath.SequenceEqual(new[] { "汎用" })
            && copiedIntent.Configuration.EquivalentTo(ReviewBuiltIns.Basic.Configuration)
            && model.CurrentReviewConfiguration.EquivalentTo(managementWorking));
        string copiedId = copiedIntent.Id;
        model.ManagedViewIntentName = "ゲーム基本";
        model.ManagedViewIntentClassification = "動画 > ゲーム";
        await model.SaveManagedViewIntentMetadataAsync();
        var renamedIntent = model.ReviewSets.Single(s => s.Id == copiedId);
        check("ux_view_intent_rename_move_preserves_config", renamedIntent.DisplayPath == "動画 > ゲーム > ゲーム基本"
            && renamedIntent.Configuration.EquivalentTo(copiedIntent.Configuration)
            && model.CurrentReviewConfiguration.EquivalentTo(managementWorking));
        await model.DeleteManagedViewIntentAsync();
        check("ux_view_intent_delete_preserves_working", !model.ReviewSets.Any(s => s.Id == copiedId)
            && model.CurrentReviewConfiguration.EquivalentTo(managementWorking));

        var unresolved = new ReviewSet("user.missingfixture", "参照切れ検証", new ReviewConfiguration([new("missing.native", true)], 1));
        model.ApplyReviewSet(unresolved); await model.RequeryAsync();
        check("ux_missing_reference_visible_retained", model.HasMissingFilters && model.CurrentReviewConfiguration.IsEnabled("missing.native") && model.MissingFilterSummary.Contains("有効 1件", StringComparison.Ordinal) && model.Candidates.Count == 0);
        model.RestoreReviewSettings(); await model.RequeryAsync();
        var chosen = model.Profiles.Single(p => p.Profile.Id == GenericFilterCatalog.LargeSceneChange.Id);
        model.ManagedFilter = chosen; model.DisplayGroup = "整理用"; model.DisplayName = "表示名だけ変更";
        var configurationBeforeAlias = model.CurrentReviewConfiguration;
        await model.SaveFilterPresentationAsync();
        check("ux_alias_preserves_detector_and_sets", chosen.Profile.Id == GenericFilterCatalog.LargeSceneChange.Id && chosen.Generic?.Id == GenericFilterCatalog.LargeSceneChange.Id
            && chosen.Group == "整理用" && chosen.ShortName == "表示名だけ変更" && chosen.Profile.Name == "大きな場面切替"
            && model.CurrentReviewConfiguration.EquivalentTo(configurationBeforeAlias));
        model.FilterSearch = "表示名だけ";
        check("ux_filter_search_uses_display_metadata", model.FilterLibrary.Cast<object>().OfType<ProfileChoice>().Count() == 1);
        model.FilterSearch = "";
        model.ManagedFilterRow = model.FilterManagementRows.Single(r => r.FilterId == chosen.Profile.Id);
        check("ux_filter_usage_metadata", model.ManagedFilterRow.SavedUsageCount > 0
            && !model.ManagedFilterRow.IsUnused && !model.ManagedFilterRow.CanDelete
            && model.ManagedFilterRow.UsageDestinationsText.Contains("基本", StringComparison.Ordinal));
        check("ux_active_filters_track_toggle", model.ActiveFilters.Count == model.Profiles.Count(p => p.Enabled));
        chosen.RemoveCommand.Execute(null); await model.RequeryAsync();
        check("ux_chip_off_is_not_delete", !chosen.Enabled && model.Profiles.Contains(chosen) && !model.ActiveFilters.Contains(chosen));
        chosen.Enabled = true; await model.RequeryAsync();
        model.OpenFilterManagerCommand.Execute(null);
        var manager = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "フィルターを整理");
        check("ux_manager_surface_and_narrow_layout", manager.Content is FilterManagerView && Capture(new FilterManagerView { DataContext = model }, output, "filter-manager", 380, 540,
            ["FilterSearchBox", "FilterUsageModeSelector", "FilterLibraryList", "FilterUsageDestinations", "DeleteManagedFilterButton", "ManagerDataStoragePath", "ManagerOpenDataFolderButton"]));
        manager.Close();
        model.OpenLearningCommand.Execute(null);
        var authoring = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "動画からフィルターを作る");
        var retained = (LearningModel)((LearningView)authoring.Content).DataContext;
        await retained.RefreshAvailabilityAsync(); var retainedDraft = retained.Draft;
        authoring.Close(); model.OpenLearningCommand.Execute(null);
        authoring = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "動画からフィルターを作る");
        var reopened = (LearningModel)((LearningView)authoring.Content).DataContext;
        await reopened.RefreshAvailabilityAsync();
        check("ux_authoring_close_reopen_keeps_draft", retainedDraft != null && ReferenceEquals(retained, reopened) && ReferenceEquals(retainedDraft, reopened.Draft));
        reopened.Preview(); await model.RequeryAsync(); await model.ReloadSavedFiltersAsync();
        check("ux_reload_does_not_silently_end_trial", model.HasTrialFilters && !model.SaveNewReviewSetCommand.CanExecute(null));
        await model.ReloadSavedFiltersAsync(discardTrials: true);
        check("ux_explicit_trial_end_restores_saved", !model.HasTrialFilters && model.Profiles.Any(p => p.Learned is { Revision: 1 }));
        authoring.Close();
        await DraftFailures(output, check);
        model.ApplyReviewSet(ReviewBuiltIns.Basic); await model.RequeryAsync();
        var view = new NavigatorView { DataContext = model };
        check("ux_target_set_review_narrow_layout", Capture(view, output, "ux-review", 360, 480,
            ["CaptureButton", "ViewIntentSelector", "ViewIntentSummaryText", "QuickFilterExpander", "CandidateList", "SensitivitySlider"]));
        var quickView = new NavigatorView { DataContext = model };
        ((Expander)quickView.FindName("QuickFilterExpander")).IsExpanded = true;
        check("ux_quick_filter_disclosure_layout", Capture(quickView, output, "ux-review-filters", 360, 600,
            ["ViewIntentSelector", "QuickFilterExpander", "QuickFilterList", "FilterOrganizerButton", "LearningButton", "SensitivitySlider"]));
        using (var emptyModel = new NavigatorModel())
        {
            var emptyView = new NavigatorView { DataContext = emptyModel };
            check("ux_empty_state_next_action", Capture(emptyView, output, "ux-empty", 360, 360, ["NoTargetHint", "EmptyCaptureButton"]));
        }
        var list = (ListBox)view.FindName("CandidateList");
        var keys = list.InputBindings.OfType<KeyBinding>().ToArray();
        check("ux_navigation_keyboard_bindings_local", keys.Length == 3 && keys.Any(k => k.Key == Key.Up && k.Modifiers == ModifierKeys.Alt && k.Command == model.PreviousCommand)
            && keys.Any(k => k.Key == Key.Down && k.Modifiers == ModifierKeys.Alt && k.Command == model.NextCommand)
            && keys.Any(k => k.Key == Key.Enter && k.Command == model.JumpCommand) && view.InputBindings.Count == 0);
        model.Selected = null;
        keys.Single(k => k.Key == Key.Down).Command.Execute(null);
        check("ux_bound_navigation_uses_existing_jump", model.Selected != null && timeline.CurrentFrame == model.Selected.Frame);
        check("ux_settings_never_redecode_or_edit_items", model.AnalysisBackendCallCount == decodeCalls && Signature() == before);

        var learnedRow = model.FilterManagementRows.Single(r => r.Choice.Learned is { Revision: > 0 });
        model.FilterUsageMode = "未使用";
        var unusedVisible = model.VisibleFilterManagementRows.ToArray();
        model.FilterUsageMode = "使用中";
        var usedVisible = model.VisibleFilterManagementRows.ToArray();
        model.FilterUsageMode = "すべて";
        check("ux_filter_usage_filtering", learnedRow.IsUnused && learnedRow.CanDelete
            && unusedVisible.Any(r => r.FilterId == learnedRow.FilterId)
            && !usedVisible.Any(r => r.FilterId == learnedRow.FilterId)
            && usedVisible.Any(r => r.FilterId == GenericFilterCatalog.LargeSceneChange.Id));

        var learnedFilter = learnedRow.Choice.Learned!;
        var corpus = NavigatorModel.UserCorpus();
        string historyDir = Path.Combine(corpus.Root, "filters", learnedFilter.Label.Key);
        int historyBefore = Directory.GetFiles(historyDir, "*.json").Length;
        model.ManagedFilterRow = learnedRow;
        await model.DeleteManagedFilterAsync();
        int historyAfter = Directory.GetFiles(historyDir, "*.json").Length;
        check("ux_unused_learned_filter_safe_delete", !model.Profiles.Any(p => p.Profile.Id == learnedRow.FilterId)
            && new FilterStore(corpus).Read(learnedFilter.Label) == null
            && historyBefore > 0 && historyAfter == historyBefore
            && model.AnalysisBackendCallCount == decodeCalls && Signature() == before);

        File.WriteAllText(Path.Combine(output, "ux-summary.json"), JsonSerializer.Serialize(new
        {
            schema = "navigator.ux-native.v1", checkout = Environment.GetEnvironmentVariable("GITHUB_SHA"), completed = true,
            callsBefore = decodeCalls, callsAfter = model.AnalysisBackendCallCount, physicalKeyboardTested = false,
            draftSurvivesViewClose = true, restartDraftPersistence = false, settingsRevision = store.Read().Revision
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task DraftFailures(string output, Action<string, bool> check)
    {
        var original = NavigatorModel.UserCorpus(); var isolated = new CorpusStore(Path.Combine(output, "ux-corpus"));
        var label = new LearningLabel("作業保全検証", "切替候補");
        foreach (var sample in original.Read().Samples) isolated.RegisterPack(original.LoadPack(sample), label, sample.OriginalNames[0]);
        using var learning = new LearningModel(isolated, _ => { });
        learning.SetFiles([], label); await learning.RefreshAvailabilityAsync();
        check("ux_rollback_prevented_before_save", !learning.RollbackCommand.CanExecute(null));
        await learning.CreateDraftAsync(); var draft = learning.Draft;
        if (draft == null) throw new InvalidOperationException(learning.Status);
        Task cancelled = learning.CreateDraftAsync(); learning.Cancel(); await cancelled;
        check("ux_generation_cancel_keeps_draft", ReferenceEquals(draft, learning.Draft) && learning.SaveCommand.CanExecute(null));
        learning.Group = "教材なし"; await learning.RefreshAvailabilityAsync();
        check("ux_wrong_context_keeps_but_blocks_draft", ReferenceEquals(draft, learning.Draft) && !learning.SaveCommand.CanExecute(null) && !learning.PreviewCommand.CanExecute(null) && !learning.CreateCommand.CanExecute(null));
        await learning.CreateDraftAsync();
        check("ux_generation_error_keeps_draft", ReferenceEquals(draft, learning.Draft));
        learning.RestoreDraftContextCommand.Execute(null); await learning.RefreshAvailabilityAsync();
        check("ux_restore_draft_context_reenables", learning.SaveCommand.CanExecute(null) && learning.Group == label.Group);
        using (var hold = new FileStream(Path.Combine(isolated.Root, ".writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await learning.SaveAsync();
            check("ux_filter_save_failure_keeps_draft", ReferenceEquals(draft, learning.Draft) && learning.SaveCommand.CanExecute(null) && new FilterStore(isolated).Read(label) == null);
        }
        await learning.SaveAsync();
        check("ux_filter_save_retry_succeeds", learning.Draft == null && learning.LastSaved is { Revision: 1 } && !learning.CanRollback);
        await learning.CreateDraftAsync(); var stale = learning.Draft;
        var first = original.Read().Samples[0];
        isolated.RegisterPack(original.LoadPack(first), new LearningLabel("追加分類", "別の正例"), first.OriginalNames[0]);
        await learning.RefreshAvailabilityAsync();
        check("ux_corpus_change_retains_stale_draft", stale != null && ReferenceEquals(stale, learning.Draft) && !learning.CanUseDraft);
        await learning.SaveAsync();
        check("ux_stale_save_does_not_publish", ReferenceEquals(stale, learning.Draft) && new FilterStore(isolated).Read(label)!.Revision == 1);
        await learning.CreateDraftAsync(); await learning.SaveAsync();
        check("ux_second_save_enables_actual_rollback", learning.LastSaved is { Revision: 2 } && learning.CanRollback);
        await learning.CreateDraftAsync(); var beforeRollback = learning.Draft; await learning.RollbackAsync();
        check("ux_rollback_keeps_unsaved_draft_stale", ReferenceEquals(beforeRollback, learning.Draft) && !learning.CanUseDraft && !learning.CanRollback && new FilterStore(isolated).Read(label)!.Revision == 1);
        var learningView = new LearningView { DataContext = learning };
        Capture(learningView, output, "ux-authoring", 400, 640, ["LearningResults", "SaveFilterButton", "DataStoragePath", "OpenDataFolderButton"]);
        var inputs = (FrameworkElement)learningView.FindName("ClassificationInputs");
        await learning.RefreshLabelsAsync(); learning.SelectedLabel = learning.KnownLabels.Single(l => l.Label == label); await learning.RefreshAvailabilityAsync();
        await learningView.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        learningView.UpdateLayout(); bool hidden = !learning.IsNewClassification && inputs.Visibility == Visibility.Collapsed;
        learning.NewClassificationCommand.Execute(null);
        await learningView.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        learningView.UpdateLayout();
        check("ux_classification_progressive_disclosure", hidden && learning.IsNewClassification && inputs.Visibility == Visibility.Visible);
        learning.DiscardDraftCommand.Execute(null);
        check("ux_explicit_discard_only_clears_draft", learning.Draft == null && new FilterStore(isolated).Read(label) is { Revision: 1 } && isolated.Read().Samples.Length == original.Read().Samples.Length);
    }
    private static bool Capture(UserControl view, string output, string name, int width, int height, string[] required)
    {
        view.Width = width; view.Height = height;
        var border = new Border { Background = SystemColors.WindowBrush, Child = view, Width = width, Height = height };
        border.Measure(new Size(width, height)); border.Arrange(new Rect(0, 0, width, height)); border.UpdateLayout();
        bool Inside(string id)
        {
            if (view.FindName(id) is not FrameworkElement element || element.ActualWidth <= 0 || element.ActualHeight <= 0 || element.Visibility != Visibility.Visible) return false;
            Point p = element.TranslatePoint(new Point(), view);
            return p.X >= 0 && p.Y >= 0 && p.X + element.ActualWidth <= width + .5 && p.Y + element.ActualHeight <= height + .5;
        }
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(border);
        using (var file = File.Create(Path.Combine(output, $"{name}-{width}x{height}.png"))) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(file); }
        return required.All(Inside);
    }
}
