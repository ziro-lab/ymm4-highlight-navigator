using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record ViewIntentChoice(ReviewSet Set, string Summary)
{
    public string DisplayPath => Set.DisplayPath;
    public bool IsBuiltIn => Set.IsBuiltIn;
}

public sealed partial class NavigatorModel
{
    private readonly ReviewWorkspace review = new(ReviewBuiltIns.Basic);
    private ReviewSettingsSnapshot settings = new(1, 0, [], []);
    private ReviewSet? selectedReviewSet = ReviewBuiltIns.Basic;
    private ViewIntentChoice? selectedViewIntent;
    private ListCollectionView? filterLibrary;
    private Window? filterWindow;
    private bool syncingReview, settingsBusy, settingsReady;
    private CancellationTokenSource? settingsCancel;
    private string setName = "マイ見たいもの", filterSearch = "", reviewStatus = "標準の「見たいもの」は直接上書きしません。必要なら別名で保存できます。";
    private ProfileChoice? managedFilter;
    private string displayGroup = "", displayName = "";

    // ReviewSets/SelectedReviewSet remain the internal compatibility surface. Main UI uses ViewIntents.
    public ObservableCollection<ReviewSet> ReviewSets { get; } = [];
    public ObservableCollection<ViewIntentChoice> ViewIntents { get; } = [];
    public ObservableCollection<ProfileChoice> ActiveFilters { get; } = [];
    public ReviewConfiguration CurrentReviewConfiguration => review.Current;

    public bool IsReviewSettingsBusy
    {
        get => settingsBusy;
        private set { settingsBusy = value; Changed(); Changed(nameof(CanConfigure)); NotifyReview(); }
    }

    public ReviewSet? SelectedReviewSet
    {
        get => selectedReviewSet;
        set
        {
            if (syncingReview || value == null || value.Id == selectedReviewSet?.Id) return;
            Safe(() => ApplyReviewSet(value));
        }
    }

    public ViewIntentChoice? SelectedViewIntent
    {
        get => selectedViewIntent;
        set
        {
            if (syncingReview || value == null || value.Set.Id == selectedReviewSet?.Id) return;
            Safe(() => ApplyReviewSet(value.Set));
        }
    }

    public bool HasViewIntentChanges => review.IsModified;
    public string ReviewStateText => (review.AppliedSet?.DisplayPath ?? "現在の見たいもの") + (review.IsModified ? "（変更あり・未保存）" : "");
    public string ViewIntentSummary => review.AppliedSet is { } set ? Summarize(set) : "見たいものを選んでください。";
    public string FilterQuickSummary => $"フィルター {Profiles.Count(p => p.Enabled)}/{Profiles.Count}";
    public bool SettingsNeedReload => !settingsReady && !IsReviewSettingsBusy;

    public bool HasMissingFilters => review.Current.Filters.Any(f => !Profiles.Any(p => p.Profile.Id == f.FilterId));
    public string MissingFilterSummary
    {
        get
        {
            int missing = review.Current.Filters.Count(f => !Profiles.Any(p => p.Profile.Id == f.FilterId));
            int enabled = review.Current.MissingEnabledIds(Profiles.Select(p => p.Profile.Id)).Length;
            return missing == 0 ? "" : $"参照先がないフィルター {missing}件（有効 {enabled}件）。参照は保持しています。";
        }
    }

    public string ReviewStatus { get => reviewStatus; private set { reviewStatus = value; Changed(); } }
    public string SetName { get => setName; set { setName = value; Changed(); NotifyReview(); } }
    public bool HasTrialFilters => Profiles.Any(p => p.Learned is { Revision: 0 });

    public string SettingsSaveHint => !settingsReady ? "保存状態を読み込めていません。再読み込みしてください。"
        : HasTrialFilters ? "試用中のフィルターがあります。フィルターを保存するか、試用を終了してから「見たいもの」を保存してください。"
        : review.AppliedSet?.IsBuiltIn == true ? "標準の「見たいもの」は上書きできません。別名で保存できます。" : "保存済みの「見たいもの」と現在の変更は別です。";

    private bool CanSaveSettings => !disposed && CanConfigure && settingsReady && !HasTrialFilters
        && !string.IsNullOrWhiteSpace(SetName) && SetName.Length <= 100 && !SetName.Any(char.IsControl);

    public ICommand SaveNewReviewSetCommand => new RelayCommand(() => _ = SaveReviewSetAsync(false), () => CanSaveSettings);
    public ICommand OverwriteReviewSetCommand => new RelayCommand(() => _ = SaveReviewSetAsync(true),
        () => CanSaveSettings && review.AppliedSet is { IsBuiltIn: false } current && settings.Sets.Any(s => s.Id == current.Id));
    public ICommand RestoreReviewSettingsCommand => new RelayCommand(() => Safe(RestoreReviewSettings), () => !disposed && CanConfigure && review.CanRestore);
    public ICommand ReloadReviewSettingsCommand => new RelayCommand(() => _ = ReloadReviewSettingsAsync(), () => !disposed && CanConfigure);
    public ICommand EndTrialCommand => new RelayCommand(() => _ = ReloadSavedFiltersAsync(discardTrials: true), () => !disposed && CanConfigure && HasTrialFilters);
    public ICommand OpenFilterManagerCommand => new RelayCommand(() => Safe(OpenFilterManager), () => !disposed && CanConfigure);
    public ICommand SaveFilterPresentationCommand => new RelayCommand(() => _ = SaveFilterPresentationAsync(),
        () => !disposed && CanConfigure && settingsReady && ManagedFilter != null && !string.IsNullOrWhiteSpace(DisplayGroup) && !string.IsNullOrWhiteSpace(DisplayName));

    public string FilterSearch { get => filterSearch; set { filterSearch = value; Changed(); filterLibrary?.Refresh(); } }
    public ICollectionView FilterLibrary
    {
        get
        {
            if (filterLibrary != null) return filterLibrary;
            filterLibrary = new ListCollectionView(Profiles)
            {
                Filter = o => o is ProfileChoice p && (string.IsNullOrWhiteSpace(FilterSearch)
                    || p.Name.Contains(FilterSearch.Trim(), StringComparison.OrdinalIgnoreCase))
            };
            filterLibrary.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProfileChoice.Group)));
            filterLibrary.SortDescriptions.Add(new(nameof(ProfileChoice.Group), ListSortDirection.Ascending));
            filterLibrary.SortDescriptions.Add(new(nameof(ProfileChoice.Name), ListSortDirection.Ascending));
            return filterLibrary;
        }
    }

    public ProfileChoice? ManagedFilter
    {
        get => managedFilter;
        set { managedFilter = value; Changed(); if (value != null) { DisplayGroup = value.Group; DisplayName = value.ShortName; } Commands(); }
    }
    public string DisplayGroup { get => displayGroup; set { displayGroup = value; Changed(); Commands(); } }
    public string DisplayName { get => displayName; set { displayName = value; Changed(); Commands(); } }

    public static ReviewSetStore UserReviewSettings() => new(NavigatorStorage.PrepareReview().Path);

    private void InitializeReviewSettings()
    {
        ReviewSets.Add(ReviewBuiltIns.Basic);
        RefreshFilterViews();
        RebuildViewIntents();
        NotifyReview();
    }

    private string Summarize(ReviewSet set)
    {
        var names = set.Configuration.Filters
            .Select(f => Profiles.FirstOrDefault(p => p.Profile.Id == f.FilterId)?.ShortName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        int total = set.Configuration.Filters.Length;
        int missing = Math.Max(0, total - names.Length);
        string summary = total == 0 ? "フィルターなし"
            : names.Length == 0 ? $"{total}フィルター（参照先なし）"
            : string.Join("・", names.Take(3)) + (total > 3 ? $"など{total}フィルター" : $"（{total}フィルター）");
        return missing > 0 && names.Length > 0 ? summary + $" / 参照切れ{missing}" : summary;
    }

    private void RebuildViewIntents()
    {
        string? selectedId = review.AppliedSet?.Id;
        ViewIntents.Clear();
        foreach (var set in ReviewSets.OrderBy(s => s.DisplayPath, StringComparer.CurrentCultureIgnoreCase))
            ViewIntents.Add(new(set, Summarize(set)));
        selectedViewIntent = ViewIntents.FirstOrDefault(v => v.Set.Id == selectedId);
        Changed(nameof(SelectedViewIntent));
        Changed(nameof(ViewIntentSummary));
    }

    private void NotifyReview()
    {
        Changed(nameof(ReviewStateText)); Changed(nameof(ViewIntentSummary)); Changed(nameof(HasViewIntentChanges));
        Changed(nameof(FilterQuickSummary)); Changed(nameof(SettingsNeedReload));
        Changed(nameof(MissingFilterSummary)); Changed(nameof(HasMissingFilters));
        Changed(nameof(HasTrialFilters)); Changed(nameof(SettingsSaveHint)); Changed(nameof(CurrentReviewConfiguration)); Commands();
    }

    private void RefreshFilterViews()
    {
        ActiveFilters.Clear();
        foreach (var p in Profiles.Where(p => p.Enabled)) ActiveFilters.Add(p);
        filterLibrary?.Refresh();
        RebuildViewIntents();
    }

    private ReviewConfiguration CaptureReviewConfiguration()
    {
        // Include unresolved references as well as visible controls.
        var states = review.Current.Filters.ToDictionary(f => f.FilterId, StringComparer.Ordinal);
        foreach (var p in Profiles)
            if (p.Enabled || states.ContainsKey(p.Profile.Id)) states[p.Profile.Id] = new(p.Profile.Id, p.Enabled);
        return new ReviewConfiguration(states.Values.ToImmutableArray(), Sensitivity).Normalize();
    }

    private void ReviewChoicesChanged()
    {
        if (syncingReview) return;
        review.Change(CaptureReviewConfiguration());
        RefreshFilterViews();
        NotifyReview();
        _ = RequeryAsync();
    }

    public void ApplyReviewSet(ReviewSet set)
    {
        if (disposed || !CanConfigure) throw new InvalidOperationException("処理が終わってから「見たいもの」を切り替えてください。");
        review.Change(CaptureReviewConfiguration());
        review.Apply(set);
        ApplyWorkingConfiguration();
        SetName = set.IsBuiltIn ? set.Name + " のコピー" : set.Name;
        ReviewStatus = $"{set.DisplayPath} を選びました。「切替前に戻す」で直前の状態へ一度だけ戻せます。";
    }

    public void RestoreReviewSettings()
    {
        if (disposed || !CanConfigure || !review.Restore()) return;
        ApplyWorkingConfiguration();
        SetName = review.AppliedSet is { } set ? (set.IsBuiltIn ? set.Name + " のコピー" : set.Name) : "マイ見たいもの";
        ReviewStatus = "切り替える前の状態へ戻しました。保存済みの「見たいもの」は変更していません。";
    }

    private void ApplyWorkingConfiguration()
    {
        syncingReview = true;
        try
        {
            foreach (var p in Profiles) p.Enabled = review.Current.IsEnabled(p.Profile.Id);
            sensitivity = review.Current.Sensitivity; Changed(nameof(Sensitivity));
            selectedReviewSet = ReviewSets.FirstOrDefault(s => s.Id == review.AppliedSet?.Id) ?? review.AppliedSet;
            Changed(nameof(SelectedReviewSet));
        }
        finally { syncingReview = false; }
        RefreshFilterViews();
        NotifyReview();
        _ = RequeryAsync();
    }

    private void PublishSettings(ReviewSettingsSnapshot value)
    {
        settings = value;
        syncingReview = true;
        try
        {
            ReviewSets.Clear();
            ReviewSets.Add(ReviewBuiltIns.Basic);
            foreach (var set in value.Sets.OrderBy(s => s.DisplayPath, StringComparer.CurrentCultureIgnoreCase)) ReviewSets.Add(set);
            selectedReviewSet = ReviewSets.FirstOrDefault(s => s.Id == review.AppliedSet?.Id);
            Changed(nameof(SelectedReviewSet));
            foreach (var p in Profiles) ApplyPresentation(p);
        }
        finally { syncingReview = false; }
        RefreshFilterViews();
        NotifyReview();
    }

    private void ApplyPresentation(ProfileChoice profile)
        => profile.ApplyPresentation(settings.Presentations.FirstOrDefault(p => p.FilterId == profile.Profile.Id));

    public async Task ReloadReviewSettingsAsync()
    {
        if (disposed || IsReviewSettingsBusy) return;
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().Read());
            if (disposed) return;
            settingsReady = true;
            PublishSettings(next);
            ReviewStatus = "保存済みの「見たいもの」を読み込みました。現在の作業状態は変更していません。";
        }
        catch (Exception ex)
        {
            if (!disposed)
            {
                settingsReady = false;
                ReviewStatus = "保存状態を読み込めませんでした。現在の状態と保存ファイルは保持しています。" + ex.Message;
            }
        }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
        if (!disposed) await RequeryAsync();
    }

    public async Task SaveReviewSetAsync(bool overwrite)
    {
        if (!CanSaveSettings) return;
        var captured = CaptureReviewConfiguration();
        string name = SetName;
        var target = review.AppliedSet;
        var classification = target?.ClassificationPath ?? ImmutableArray<string>.Empty;
        var before = settings;
        IsReviewSettingsBusy = true;
        using var tokenSource = new CancellationTokenSource();
        settingsCancel = tokenSource;
        try
        {
            if (overwrite && (target == null || target.IsBuiltIn))
                throw new InvalidOperationException("標準の「見たいもの」は上書きできません。");
            var next = await Task.Run(() => overwrite
                ? UserReviewSettings().SaveExisting(target! with { Name = name, Configuration = captured }, before.Revision, tokenSource.Token)
                : UserReviewSettings().SaveNew(name, classification, captured, before.Revision, tokenSource.Token));
            if (disposed) return;
            var saved = overwrite ? next.Sets.Single(s => s.Id == target!.Id)
                : next.Sets.Single(s => !before.Sets.Any(old => old.Id == s.Id));
            review.AcceptSaved(saved, captured);
            PublishSettings(next);
            SetName = saved.Name;
            ReviewStatus = $"{saved.DisplayPath} を保存しました。";
        }
        catch (OperationCanceledException)
        {
            if (!disposed) ReviewStatus = "保存を中止しました。現在の変更は保持しています。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "保存できませんでした。現在の変更は保持しています。" + ex.Message;
        }
        finally { settingsCancel = null; if (!disposed) IsReviewSettingsBusy = false; }
    }

    public async Task SaveFilterPresentationAsync()
    {
        if (disposed || !CanConfigure || !settingsReady || ManagedFilter is not { } selected) return;
        var value = new ReviewFilterPresentation(selected.Profile.Id, DisplayGroup, DisplayName);
        long revision = settings.Revision;
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().SetPresentation(value, revision));
            if (disposed) return;
            PublishSettings(next);
            ReviewStatus = "フィルターの表示名と整理グループを保存しました。検出条件や「見たいもの」の分類は変更していません。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "表示情報を保存できませんでした。入力は保持しています。" + ex.Message;
        }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
        if (!disposed) await RequeryAsync();
    }

    private void OpenFilterManager()
    {
        if (filterWindow != null) { filterWindow.Activate(); return; }
        var window = new Window
        {
            Title = "フィルターを整理",
            Content = new FilterManagerView { DataContext = this },
            Width = 520, Height = 560, MinWidth = 360, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) window.Owner = owner;
        window.Closed += (_, _) => filterWindow = null;
        filterWindow = window;
        window.Show();
    }

    private void CloseReviewSettingsSurface()
    {
        settingsCancel?.Cancel();
        try { filterWindow?.Close(); } catch (Exception) { /* Optional view must not prevent host disposal. */ }
        filterWindow = null;
    }
}
