using System.IO;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed partial class NavigatorModel
{
    private Window? learningWindow;
    private LearningModel? learningModel;
    private bool learningLoaded;
    private int filterLoadGeneration;
    public static CorpusStore UserCorpus()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData)) throw new IOException("ユーザー用の保存先を取得できませんでした。");
        return new(Path.Combine(appData, "Ymm4HighlightNavigator", "Learning"));
    }
    public ICommand OpenLearningCommand => new RelayCommand(() => Safe(OpenLearning), () => !disposed && !IsBusy);
    public async Task EnsureLearningLoadedAsync()
    {
        if (learningLoaded || disposed) return;
        learningLoaded = true; await ReloadSavedFiltersAsync(); await ReloadReviewSettingsAsync();
    }
    public async Task ReloadSavedFiltersAsync(CorpusStore? store = null, bool discardTrials = false)
    {
        int stamp = ++filterLoadGeneration;
        try
        {
            store ??= UserCorpus();
            var saved = await Task.Run(() => new FilterStore(store).ReadAll());
            if (disposed || stamp != filterLoadGeneration) return;
            var enabled = Profiles.ToDictionary(p => p.Profile.Id, p => p.Enabled);
            var trials = Profiles.Where(p => p.Learned is { Revision: 0 }).ToArray();
            var values = saved.Select(f => new ProfileChoice(f) { Enabled = enabled.GetValueOrDefault(f.Id, review.Current.IsEnabled(f.Id)) }).ToList();
            if (!discardTrials)
            {
                foreach (var trial in trials) { values.RemoveAll(p => p.Profile.Id == trial.Profile.Id); values.Add(trial); }
            }
            else
            {
                var removed = trials.Where(t => !saved.Any(f => f.Id == t.Profile.Id)).Select(t => t.Profile.Id).ToHashSet(StringComparer.Ordinal);
                review.Change(review.Current with { Filters = review.Current.Filters.Where(f => !removed.Contains(f.FilterId)).ToImmutableArray() });
            }
            foreach (var old in Profiles.Where(p => p.Learned != null).ToArray()) { old.PropertyChanged -= ProfileChanged; Profiles.Remove(old); }
            foreach (var choice in values) { ApplyPresentation(choice); choice.PropertyChanged += ProfileChanged; Profiles.Add(choice); }
            review.Change(CaptureReviewConfiguration()); RefreshFilterViews(); NotifyReview();
            await RequeryAsync();
        }
        catch (Exception ex) { if (!disposed) Status = "保存済みフィルターを読み込めませんでした。" + ex.Message; }
    }
    public void InstallTransitionFilter(TransitionFilter filter)
    {
        if (disposed) return;
        filter.Validate(); filterLoadGeneration++;
        var previous = Profiles.FirstOrDefault(p => p.Profile.Id == filter.Id);
        bool enabled = previous?.Enabled ?? true;
        var replacement = new ProfileChoice(filter) { Enabled = enabled };
        if (previous != null) { previous.PropertyChanged -= ProfileChanged; Profiles[Profiles.IndexOf(previous)] = replacement; }
        else Profiles.Add(replacement);
        ApplyPresentation(replacement); replacement.PropertyChanged += ProfileChanged;
        ReviewChoicesChanged();
    }
    private void OpenLearning()
    {
        if (learningWindow != null) { learningWindow.Activate(); return; }
        var model = learningModel ??= new LearningModel(UserCorpus(), InstallTransitionFilter);
        var view = new LearningView { DataContext = model };
        var window = new Window { Title = "動画からフィルターを作る", Content = view, Width = 680, Height = 680, MinWidth = 400, MinHeight = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) window.Owner = owner;
        window.Closed += (_, _) => learningWindow = null; // Closing the view does not discard its session draft or trial.
        learningWindow = window; window.Show(); _ = model.RefreshLabelsAsync();
    }
    private void CloseLearningSurface()
    {
        try { learningWindow?.Close(); } catch (Exception) { /* Closing this optional surface must not break host disposal. */ }
        learningWindow = null; learningModel?.Dispose(); learningModel = null;
    }
}
