using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed class FilterManagementRow(ProfileChoice choice) : NotifyModel
{
    public ProfileChoice Choice { get; } = choice;
    private ImmutableArray<string> usageDestinations = [];
    private bool workingReferenced;

    public string FilterId => Choice.Profile.Id;
    public string Group => Choice.Group;
    public string Name => Choice.ShortName;
    public string Kind => Choice.Generic is not null ? "汎用フィルター"
        : Choice.Learned is null ? "標準フィルター"
        : Choice.Learned.Revision == 0 ? "試用中"
        : "自作フィルター";
    public string Count => Choice.Count;
    public int SavedUsageCount => usageDestinations.Length;
    public bool WorkingReferenced => workingReferenced;
    public bool IsUnused => SavedUsageCount == 0 && !WorkingReferenced;
    public bool CanDelete => Choice.Learned is { Revision: > 0 } && IsUnused;
    public string UsageSummary => IsUnused ? "未使用"
        : SavedUsageCount > 0 ? $"使用先 {SavedUsageCount}件" + (WorkingReferenced ? " / 現在も使用中" : "")
        : "現在の作業状態で使用中";
    public string UsageDestinationsText => SavedUsageCount == 0
        ? WorkingReferenced ? "現在の作業状態で使用しています。" : "保存済みの「見たいもの」では使われていません。"
        : "使用先: " + string.Join(" / ", usageDestinations) + (WorkingReferenced ? " / 現在の作業状態" : "");
    public string DeleteHint => Choice.Learned is null ? "標準フィルターは削除できません。"
        : Choice.Learned.Revision == 0 ? "試用中フィルターは「試用を終了」から外してください。"
        : !IsUnused ? "使用中のフィルターは削除できません。先に「見たいもの」や現在の作業状態から外してください。"
        : "削除しても教材と過去の版は残ります。";

    internal void ApplyUsage(IEnumerable<string> destinations, bool working)
    {
        usageDestinations = destinations.Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
        workingReferenced = working;
        Changed(nameof(SavedUsageCount)); Changed(nameof(WorkingReferenced)); Changed(nameof(IsUnused));
        Changed(nameof(CanDelete)); Changed(nameof(UsageSummary)); Changed(nameof(UsageDestinationsText)); Changed(nameof(DeleteHint));
    }
}

public sealed partial class NavigatorModel
{
    private Window? viewIntentWindow;
    private ViewIntentChoice? managedViewIntent;
    private string managedViewIntentName = "", managedViewIntentClassification = "";
    private FilterManagementRow? managedFilterRow;
    private string filterUsageMode = "すべて";

    public ObservableCollection<FilterManagementRow> FilterManagementRows { get; } = [];
    public ObservableCollection<FilterManagementRow> VisibleFilterManagementRows { get; } = [];
    public ObservableCollection<string> FilterUsageModes { get; } = ["すべて", "使用中", "未使用"];
    public ObservableCollection<string> ViewIntentClassificationSuggestions { get; } = [];

    public ViewIntentChoice? ManagedViewIntent
    {
        get => managedViewIntent;
        set
        {
            if (ReferenceEquals(managedViewIntent, value)) return;
            managedViewIntent = value;
            Changed();
            if (value != null)
            {
                ManagedViewIntentName = value.Set.Name;
                ManagedViewIntentClassification = FormatClassification(value.Set.ClassificationPath);
            }
            else
            {
                ManagedViewIntentName = "";
                ManagedViewIntentClassification = "";
            }
            Changed(nameof(ManagedViewIntentCanEdit));
            Changed(nameof(ManagedViewIntentDescription));
            Commands();
        }
    }

    public bool ManagedViewIntentCanEdit => ManagedViewIntent is { IsBuiltIn: false };
    public string ManagedViewIntentDescription => ManagedViewIntent is null ? "整理する「見たいもの」を選んでください。"
        : ManagedViewIntent.IsBuiltIn ? "標準の「見たいもの」です。複製はできますが、直接変更・削除はできません。"
        : ManagedViewIntent.Summary;

    public string ManagedViewIntentName
    {
        get => managedViewIntentName;
        set { managedViewIntentName = value; Changed(); Commands(); }
    }

    public string ManagedViewIntentClassification
    {
        get => managedViewIntentClassification;
        set { managedViewIntentClassification = value; Changed(); Commands(); }
    }

    public FilterManagementRow? ManagedFilterRow
    {
        get => managedFilterRow;
        set
        {
            managedFilterRow = value;
            Changed();
            ManagedFilter = value?.Choice;
            Changed(nameof(ManagedFilterCanDelete));
            Commands();
        }
    }

    public bool ManagedFilterCanDelete => ManagedFilterRow?.CanDelete == true;

    public string FilterUsageMode
    {
        get => filterUsageMode;
        set
        {
            if (filterUsageMode == value) return;
            filterUsageMode = value;
            Changed();
            RefreshVisibleFilterManagementRows();
        }
    }

    public ICommand OpenViewIntentManagerCommand => new RelayCommand(() => Safe(OpenViewIntentManager), () => !disposed && CanConfigure);
    public ICommand DuplicateManagedViewIntentCommand => new RelayCommand(() => _ = DuplicateManagedViewIntentAsync(),
        () => !disposed && CanConfigure && settingsReady && ManagedViewIntent != null);
    public ICommand SaveManagedViewIntentMetadataCommand => new RelayCommand(() => _ = SaveManagedViewIntentMetadataAsync(),
        () => !disposed && CanConfigure && settingsReady && ManagedViewIntentCanEdit
            && !string.IsNullOrWhiteSpace(ManagedViewIntentName) && ManagedViewIntentName.Length <= 100);
    public ICommand DeleteManagedViewIntentCommand => new RelayCommand(ConfirmDeleteManagedViewIntent,
        () => !disposed && CanConfigure && settingsReady && ManagedViewIntentCanEdit);
    public ICommand DeleteManagedFilterCommand => new RelayCommand(ConfirmDeleteManagedFilter,
        () => !disposed && CanConfigure && ManagedFilterCanDelete);

    private static string FormatClassification(ImmutableArray<string> path)
        => path.IsDefaultOrEmpty ? "" : string.Join(" > ", path);

    private static ImmutableArray<string> ParseClassification(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        string normalized = text.Replace('＞', '>');
        var parts = normalized.Split('>', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("分類は「動画 > ゲーム」のように入力してください。空の階層は使えません。");
        return parts.ToImmutableArray();
    }

    private static bool SameClassification(ImmutableArray<string> a, ImmutableArray<string> b)
    {
        if (a.IsDefaultOrEmpty) a = [];
        if (b.IsDefaultOrEmpty) b = [];
        return a.Length == b.Length && a.Zip(b).All(x => string.Equals(x.First, x.Second, StringComparison.CurrentCultureIgnoreCase));
    }

    private string UniqueCopyName(ReviewSet source)
    {
        string root = source.Name + " のコピー";
        string candidate = root;
        int suffix = 2;
        while (ReviewSets.Any(s => SameClassification(s.ClassificationPath, source.ClassificationPath)
            && string.Equals(s.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
            candidate = root + " " + suffix++;
        return candidate;
    }

    private bool FilterManagementVisible(FilterManagementRow row)
    {
        string q = FilterSearch.Trim();
        bool search = q.Length == 0
            || row.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || row.Group.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || row.UsageDestinationsText.Contains(q, StringComparison.CurrentCultureIgnoreCase);
        bool usage = FilterUsageMode switch
        {
            "使用中" => !row.IsUnused,
            "未使用" => row.IsUnused,
            _ => true
        };
        return search && usage;
    }

    private void RefreshVisibleFilterManagementRows()
    {
        var next = FilterManagementRows.Where(FilterManagementVisible)
            .OrderBy(r => r.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        VisibleFilterManagementRows.Clear();
        foreach (var row in next) VisibleFilterManagementRows.Add(row);
    }

    private void RefreshManagementSurfaces()
    {
        string? filterId = managedFilterRow?.FilterId;
        FilterManagementRows.Clear();
        foreach (var profile in Profiles)
        {
            var destinations = ReviewSets
                .Where(s => s.Configuration.Filters.Any(f => f.FilterId == profile.Profile.Id))
                .Select(s => s.DisplayPath);
            bool working = review.Current.Filters.Any(f => f.FilterId == profile.Profile.Id);
            var row = new FilterManagementRow(profile);
            row.ApplyUsage(destinations, working);
            FilterManagementRows.Add(row);
        }
        RefreshVisibleFilterManagementRows();
        managedFilterRow = filterId == null ? null : FilterManagementRows.FirstOrDefault(r => r.FilterId == filterId);
        Changed(nameof(ManagedFilterRow));
        if (managedFilterRow != null) ManagedFilter = managedFilterRow.Choice;
        Changed(nameof(ManagedFilterCanDelete));

        string? viewId = managedViewIntent?.Set.Id;
        if (viewId != null)
        {
            var replacement = ViewIntents.FirstOrDefault(v => v.Set.Id == viewId);
            if (replacement == null) ManagedViewIntent = null;
            else if (!ReferenceEquals(replacement, managedViewIntent))
            {
                managedViewIntent = replacement;
                Changed(nameof(ManagedViewIntent));
                Changed(nameof(ManagedViewIntentCanEdit));
                Changed(nameof(ManagedViewIntentDescription));
            }
        }

        var suggestions = ReviewSets.Select(s => FormatClassification(s.ClassificationPath))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (!ViewIntentClassificationSuggestions.SequenceEqual(suggestions))
        {
            ViewIntentClassificationSuggestions.Clear();
            foreach (var suggestion in suggestions) ViewIntentClassificationSuggestions.Add(suggestion);
        }
        Commands();
    }

    public async Task DuplicateManagedViewIntentAsync()
    {
        if (ManagedViewIntent is not { } selected || !settingsReady || IsReviewSettingsBusy) return;
        var source = selected.Set;
        var before = settings;
        string name = UniqueCopyName(source);
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().SaveNew(name, source.ClassificationPath,
                source.Configuration, before.Revision));
            if (disposed) return;
            var saved = next.Sets.Single(s => !before.Sets.Any(old => old.Id == s.Id));
            PublishSettings(next);
            ManagedViewIntent = ViewIntents.Single(v => v.Set.Id == saved.Id);
            ReviewStatus = $"{saved.DisplayPath} を複製しました。元の「見たいもの」は変更していません。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "複製できませんでした。保存済みの内容は変更していません。" + ex.Message;
        }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
    }

    public async Task SaveManagedViewIntentMetadataAsync()
    {
        if (ManagedViewIntent is not { IsBuiltIn: false } selected || !settingsReady || IsReviewSettingsBusy) return;
        var before = settings;
        ReviewSet updated;
        try
        {
            updated = selected.Set with
            {
                Name = ManagedViewIntentName,
                ClassificationPath = ParseClassification(ManagedViewIntentClassification)
            };
            updated = updated.Normalize();
        }
        catch (Exception ex)
        {
            ReviewStatus = "名前・分類を保存できませんでした。" + ex.Message;
            return;
        }

        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().SaveExisting(updated, before.Revision));
            if (disposed) return;
            var saved = next.Sets.Single(s => s.Id == updated.Id);
            review.RefreshAppliedMetadata(saved);
            PublishSettings(next);
            ManagedViewIntent = ViewIntents.Single(v => v.Set.Id == saved.Id);
            if (review.AppliedSet?.Id == saved.Id) SetName = saved.Name;
            ReviewStatus = $"{saved.DisplayPath} の名前・分類を保存しました。Filter構成は変更していません。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "名前・分類を保存できませんでした。入力は保持しています。" + ex.Message;
        }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
    }

    private void ConfirmDeleteManagedViewIntent()
    {
        if (ManagedViewIntent is not { IsBuiltIn: false } selected) return;
        if (MessageBox.Show($"「{selected.DisplayPath}」を削除しますか？\n現在の作業状態は削除しません。",
            "見たいものを削除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _ = DeleteManagedViewIntentAsync();
    }

    public async Task DeleteManagedViewIntentAsync()
    {
        if (ManagedViewIntent is not { IsBuiltIn: false } selected || !settingsReady || IsReviewSettingsBusy) return;
        string id = selected.Set.Id;
        bool wasApplied = review.AppliedSet?.Id == id;
        var before = settings;
        IsReviewSettingsBusy = true;
        try
        {
            var next = await Task.Run(() => UserReviewSettings().Delete(id, before.Revision));
            if (disposed) return;
            review.ForgetSaved(id);
            PublishSettings(next);
            ManagedViewIntent = null;
            if (wasApplied) SetName = selected.Set.Name + " のコピー";
            ReviewStatus = wasApplied
                ? $"{selected.DisplayPath} を削除しました。現在の作業状態は未保存のまま保持しています。"
                : $"{selected.DisplayPath} を削除しました。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "削除できませんでした。保存済みの内容は保持しています。" + ex.Message;
        }
        finally { if (!disposed) IsReviewSettingsBusy = false; }
    }

    private void ConfirmDeleteManagedFilter()
    {
        if (ManagedFilterRow is not { CanDelete: true } row) return;
        if (MessageBox.Show($"未使用の自作フィルター「{row.Name}」を削除しますか？\n教材と過去の版は残します。",
            "フィルターを削除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _ = DeleteManagedFilterAsync();
    }

    public async Task DeleteManagedFilterAsync()
    {
        if (ManagedFilterRow is not { CanDelete: true } row || row.Choice.Learned is not { Revision: > 0 } learned) return;
        try
        {
            await Task.Run(() => new FilterStore(UserCorpus()).Remove(learned.Label, learned.Revision));
            if (disposed) return;
            ManagedFilterRow = null;
            await ReloadSavedFiltersAsync();
            ReviewStatus = $"{row.Name} をフィルター一覧から削除しました。教材と過去の版は保持しています。";
        }
        catch (Exception ex)
        {
            if (!disposed) ReviewStatus = "フィルターを削除できませんでした。現在の内容は保持しています。" + ex.Message;
        }
    }

    private void OpenViewIntentManager()
    {
        if (viewIntentWindow != null) { viewIntentWindow.Activate(); return; }
        var window = new Window
        {
            Title = "見たいものを整理",
            Content = new ViewIntentManagerView { DataContext = this },
            Width = 560, Height = 620, MinWidth = 400, MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) window.Owner = owner;
        window.Closed += (_, _) => viewIntentWindow = null;
        viewIntentWindow = window;
        window.Show();
    }

    private void CloseViewIntentManagementSurface()
    {
        try { viewIntentWindow?.Close(); } catch (Exception) { /* Optional management window must not block host disposal. */ }
        viewIntentWindow = null;
    }
}
