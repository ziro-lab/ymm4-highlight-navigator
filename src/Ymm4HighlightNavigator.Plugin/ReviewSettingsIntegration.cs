using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed partial class NavigatorModel
{
    private readonly ReviewWorkspace review = new(ReviewBuiltIns.Basic);
    private ReviewSettingsSnapshot settings = new(1, 0, [], []);
    private ReviewSet? selectedReviewSet = ReviewBuiltIns.Basic;
    private ListCollectionView? filterLibrary;
    private Window? filterWindow;
    private bool syncingReview, settingsBusy, settingsReady;
    private CancellationTokenSource? settingsCancel;
    private string setName = "マイ確認セット", filterSearch = "", reviewStatus = "標準セットは変更しても上書きされません。";
    private ProfileChoice? managedFilter;
    private string displayGroup = "", displayName = "";
    public ObservableCollection<ReviewSet> ReviewSets { get; } = [];
    public ObservableCollection<ProfileChoice> ActiveFilters { get; } = [];
    public ReviewConfiguration CurrentReviewConfiguration => review.Current;
    public bool IsReviewSettingsBusy { get => settingsBusy; private set { settingsBusy = value; Changed(); Changed(nameof(CanConfigure)); NotifyReview(); } }
    public ReviewSet? SelectedReviewSet
    {
        get => selectedReviewSet;
        set
        {
            if (syncingReview || value == null || value.Id == selectedReviewSet?.Id) return;
            Safe(() => ApplyReviewSet(value));
        }
    }
    public string ReviewStateText => (review.AppliedSet?.Name ?? "現在の確認設定") + (review.IsModified ? "（変更あり・未保存）" : "");
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
    public string SettingsSaveHint => !settingsReady ? "保存先を読み込めていません。「再読み込み」を実行してください。"
        : HasTrialFilters ? "試用中のフィルターがあります。フィルターを保存するか、試用を終了してからセットを保存してください。"
        : review.AppliedSet?.IsBuiltIn == true ? "標準セットは上書きできません。自分用の名前で新規保存できます。" : "保存済みセットと現在の確認設定は別です。";
    private bool CanSaveSettings => !disposed && CanConfigure && settingsReady && !HasTrialFilters
        && !string.IsNullOrWhiteSpace(SetName) && SetName.Length <= 100 && !SetName.Any(char.IsControl);
    public ICommand SaveNewReviewSetCommand => new RelayCommand(() => _ = SaveReviewSetAsync(false), () => CanSaveSettings);
    public ICommand OverwriteReviewSetCommand => new RelayCommand(() => _ = SaveReviewSetAsync(true), () => CanSaveSettings && review.AppliedSet is { IsBuiltIn: false } current && settings.Sets.Any(s => s.Id == current.Id));
    public ICommand RestoreReviewSettingsCommand => new RelayCommand(() => Safe(RestoreReviewSettings), () => !disposed && CanConfigure && review.CanRestore);
    public ICommand ReloadReviewSettingsCommand => new RelayCommand(() => _ = ReloadReviewSettingsAsync(), () => !disposed && CanConfigure);
    public ICommand EndTrialCommand => new RelayCommand(() => _ = ReloadSavedFiltersAsync(discardTrials: true), () => !disposed && CanConfigure && HasTrialFilters);
    public ICommand OpenFilterManagerCommand => new RelayCommand(() => Safe(OpenFilterManager), () => !disposed && CanConfigure);
    public ICommand SaveFilterPresentationCommand => new RelayCommand(() => _ = SaveFilterPresentationAsync(), () => !disposed && CanConfigure && settingsReady && ManagedFilter != null && !string.IsNullOrWhiteSpace(DisplayGroup) && !string.IsNullOrWhiteSpace(DisplayName));
    public string FilterSearch { get => filterSearch; set { filterSearch = value; Changed(); filterLibrary?.Refresh(); } }
    public ICollectionView FilterLibrary
    {
        get
        {
            if (filterLibrary != null) return filterLibrary;
            filterLibrary = new ListCollectionView(Profiles) { Filter = o => o is ProfileChoice p && (string.IsNullOrWhiteSpace(FilterSearch) || p.Name.Contains(FilterSearch.Trim(), StringComparison.OrdinalIgnoreCase)) };
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
        ReviewSets.Add(ReviewBuiltIns.Basic); RefreshFilterViews(); NotifyReview();
    }
    private void NotifyReview()
    {
        Changed(nameof(ReviewStateText)); Changed(nameof(MissingFilterSummary)); Changed(nameof(HasMissingFilters));
        Changed(nameof(HasTrialFilters)); Changed(nameof(SettingsSaveHint)); Changed(nameof(CurrentReviewConfiguration)); Commands();
    }
    private void RefreshFilterViews()
    {
        ActiveFilters.Clear(); foreach (var p in Profiles.Where(p => p.Enabled)) ActiveFilters.Add(p);
        filterLibrary?.Refresh();
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
        review.Change(CaptureReviewConfiguration()); RefreshFilterViews(); NotifyReview(); _ = RequeryAsync();
    }
    public void ApplyReviewSet(ReviewSet set)
    {
        if (disposed || !CanConfigure) throw new InvalidOperationException("処理が終わってから確認セットを切り替えてください。");
        review.Change(CaptureReviewConfiguration()); review.Apply(set); ApplyWorkingConfiguration();
        SetName = set.IsBuiltIn ? "マイ確認セット" : set.Name;
        ReviewStatus = $"{set.Name} を適用しました。「切替前に戻す」で直前の設定を復元できます。";
    }
    public void RestoreReviewSettings()
    {
        if (disposed || !CanConfigure || !review.Restore()) return;
        ApplyWorkingConfiguration(); SetName = review.AppliedSet is { IsBuiltIn: false } set ? set.Name : "マイ確認セット";
        ReviewStatus = "切り替える前の確認設定へ戻しました。保存済みセットは変更していません。";
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
        RefreshFilterViews(); NotifyReview(); _ = RequeryAsync();
    }
    private void PublishSettings(ReviewSettingsSnapshot value)
    {
        settings = value; syncingReview = true;
        try
        {
            ReviewSets.Clear(); ReviewSets.Add(ReviewBuiltIns.Basic);
            foreach (var set in value.Sets.OrderBy(s => s.Name, StringComparer.CurrentCulture)) ReviewSets.Add(set);
            selectedReviewSet = ReviewSets.FirstOrDefault(s => s.Id == review.AppliedSet?.Id);
            Changed(nameof(SelectedReviewSet));
            foreach (var p in Profiles) ApplyPresentation(p);
        }
        finally { syncingReview = false; }
        RefreshFilterViews(); NotifyReview();
    }
    private void ApplyPresentation(ProfileChoice profile) => profile.ApplyPresentation(settings.Presentations.FirstOrDefault(p => p.FilterId == profile.Profile.Id));
    public async Task ReloadReviewSettingsAsync()
    {
        if (disposed || IsReviewSettingsBusy) return;
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().Read());
            if (disposed) return;
            settingsReady = true; PublishSettings(next); ReviewStatus = "保存済みセットを読み込みました。現在の確認設定は変更していません。";
        }
        catch (Exception ex) { if (!disposed) { settingsReady = false; ReviewStatus = "確認設定を読み込めませんでした。現在の設定と保存ファイルは保持しています。" + ex.Message; } }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
        if (!disposed) await RequeryAsync();
    }
    public async Task SaveReviewSetAsync(bool overwrite)
    {
        if (!CanSaveSettings) return;
        var captured = CaptureReviewConfiguration(); string name = SetName; var target = review.AppliedSet;
        var before = settings; IsReviewSettingsBusy = true;
        using var tokenSource = new CancellationTokenSource(); settingsCancel = tokenSource;
        try
        {
            if (overwrite && (target == null || target.IsBuiltIn)) throw new InvalidOperationException("標準セットは上書きできません。");
            var next = await Task.Run(() => overwrite
                ? UserReviewSettings().SaveExisting(new(target!.Id, name, captured), before.Revision, tokenSource.Token)
                : UserReviewSettings().SaveNew(name, captured, before.Revision, tokenSource.Token));
            if (disposed) return;
            var saved = overwrite ? next.Sets.Single(s => s.Id == target!.Id) : next.Sets.Single(s => !before.Sets.Any(old => old.Id == s.Id));
            review.AcceptSaved(saved, captured); PublishSettings(next);
            ReviewStatus = $"{saved.Name} を保存しました。";
        }
        catch (OperationCanceledException) { if (!disposed) ReviewStatus = "保存を中止しました。現在の確認設定は保持しています。"; }
        catch (Exception ex) { if (!disposed) ReviewStatus = "保存できませんでした。現在の確認設定は保持しています。" + ex.Message; }
        finally { settingsCancel = null; if (!disposed) IsReviewSettingsBusy = false; }
    }
    public async Task SaveFilterPresentationAsync()
    {
        if (disposed || !CanConfigure || !settingsReady || ManagedFilter is not { } selected) return;
        var value = new ReviewFilterPresentation(selected.Profile.Id, DisplayGroup, DisplayName); long revision = settings.Revision;
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().SetPresentation(value, revision));
            if (disposed) return;
            PublishSettings(next); ReviewStatus = "表示名と整理グループを保存しました。検出条件と教材の分類は変更していません。";
        }
        catch (Exception ex) { if (!disposed) ReviewStatus = "表示情報を保存できませんでした。入力は保持しています。" + ex.Message; }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
        if (!disposed) await RequeryAsync();
    }
    private void OpenFilterManager()
    {
        if (filterWindow != null) { filterWindow.Activate(); return; }
        var window = new Window { Title = "フィルターの選択・整理", Content = new FilterManagerView { DataContext = this }, Width = 520, Height = 560, MinWidth = 360, MinHeight = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) window.Owner = owner;
        window.Closed += (_, _) => filterWindow = null;
        filterWindow = window; window.Show();
    }
    private void CloseReviewSettingsSurface()
    {
        settingsCancel?.Cancel();
        try { filterWindow?.Close(); } catch (Exception) { /* Optional view must not prevent host disposal. */ }
        filterWindow = null;
    }
}
