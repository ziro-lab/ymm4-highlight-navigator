using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using Ymm4HighlightNavigator.Core;

namespace Ymm4HighlightNavigator.Plugin;

public sealed record LearningRow(string Name, string Result, string Details);
public sealed record LabelChoice(LearningLabel Label)
{
    public string Display => Label.Group + " / " + Label.Name;
}

public sealed class LearningModel : NotifyModel, IDisposable
{
    private readonly CorpusStore corpus;
    private readonly FilterStore filters;
    private readonly Action<TransitionFilter> useFilter;
    private CancellationTokenSource? cancel;
    private bool busy, disposed, checking, hasExamples, hasPrevious, newClassification = true;
    private int contextGeneration, knownFilterRevision = -1;
    private long knownCorpusRevision = -1;
    private string group = "マイフィルター", filterName = "場面切り替わり", status = "切り替わりの前後を含む動画を、まとめて追加できます。";
    private string availabilityHint = "教材を選ぶか、保存済みの分類を選んでください。", rowsHeading = "教材";
    private double progress;
    private FilterDraft? draft;
    private LabelChoice? selectedLabel;
    private ImmutableArray<string> paths = [];
    public ObservableCollection<LearningRow> Rows { get; } = [];
    public ObservableCollection<LabelChoice> KnownLabels { get; } = [];
    public LabelChoice? SelectedLabel
    {
        get => selectedLabel;
        set
        {
            if (IsBusy || disposed || selectedLabel == value) return;
            selectedLabel = value; Changed();
            if (value != null) { SetContext(value.Label); IsNewClassification = false; }
        }
    }
    public bool IsNewClassification { get => newClassification; private set { newClassification = value; Changed(); } }
    public string Group { get => group; set { if (group == value || IsBusy || disposed) return; group = value; Changed(); ContextChanged(); } }
    public string FilterName { get => filterName; set { if (filterName == value || IsBusy || disposed) return; filterName = value; Changed(); ContextChanged(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanEdit)); NotifyDraft(); } }
    public bool CanEdit => !disposed && !IsBusy;
    public bool CanUseDraft => CanEdit && !checking && DraftMatchesContext;
    public bool CanRollback => CanEdit && !checking && hasPrevious;
    private bool DraftMatchesContext => draft != null && TryLabel(out var label) && label == draft.Filter.Label
        && knownCorpusRevision == draft.CorpusRevision && knownFilterRevision == draft.BaseRevision;
    public string Status { get => status; private set { status = value; Changed(); } }
    public double Progress { get => progress; private set { progress = Math.Clamp(value, 0, 1); Changed(); } }
    public string InputSummary => $"追加する動画 {paths.Length}本（元動画は変更・削除しません）";
    public string AvailabilityHint { get => availabilityHint; private set { availabilityHint = value; Changed(); } }
    public string RowsHeading { get => rowsHeading; private set { rowsHeading = value; Changed(); } }
    public FilterDraft? Draft => draft;
    public bool HasDraft => draft != null;
    public TransitionFilter? LastSaved { get; private set; }
    public ImportBatchResult? LastImport { get; private set; }
    public string DraftSummary => draft == null ? LastSaved == null ? "作成した候補は、試してから保存できます。" : $"{LastSaved.Label.Name} を保存済み（第{LastSaved.Revision}版）" :
        $"保持中: {draft.Filter.Label.Group} / {draft.Filter.Label.Name}\n候補あり {draft.Coverage.Covered}本 / 未検出 {draft.Coverage.Hard}本 / 切替候補なし {draft.Coverage.NoTransition}本";
    public string DraftHint => draft == null ? "" : checking ? "適用先を確認中です。候補は保持しています。" : DraftMatchesContext
        ? "未保存の候補です。中止や保存失敗でも消えません。"
        : TryLabel(out var label) && label != draft.Filter.Label ? "別の分類の候補を保持しています。作成時の分類へ戻すと確認できます。"
        : "教材または保存版が変わりました。前の候補は保持していますが、試用・保存には再生成が必要です。";
    public ICommand ChooseFolderCommand { get; }
    public ICommand ChooseFilesCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand NewClassificationCommand { get; }
    public ICommand RestoreDraftContextCommand { get; }
    public ICommand DiscardDraftCommand { get; }

    public LearningModel(CorpusStore corpus, Action<TransitionFilter> useFilter)
    {
        this.corpus = corpus; filters = new(corpus); this.useFilter = useFilter;
        ChooseFolderCommand = new RelayCommand(() => Safe(ChooseFolder), () => CanEdit);
        ChooseFilesCommand = new RelayCommand(() => Safe(ChooseFiles), () => CanEdit);
        ImportCommand = new RelayCommand(() => _ = ImportFromHostAsync(), () => CanEdit && paths.Length > 0 && TryLabel(out _));
        CreateCommand = new RelayCommand(() => _ = CreateDraftAsync(), () => CanEdit && !checking && hasExamples);
        PreviewCommand = new RelayCommand(() => Safe(Preview), () => CanUseDraft);
        SaveCommand = new RelayCommand(() => _ = SaveAsync(), () => CanUseDraft);
        RollbackCommand = new RelayCommand(() => _ = RollbackAsync(), () => CanRollback);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        NewClassificationCommand = new RelayCommand(() => { selectedLabel = null; Changed(nameof(SelectedLabel)); IsNewClassification = true; }, () => CanEdit);
        RestoreDraftContextCommand = new RelayCommand(() => { if (draft != null) SetContext(draft.Filter.Label); }, () => CanEdit && draft != null);
        DiscardDraftCommand = new RelayCommand(() => { draft = null; NotifyDraft(); Status = "未保存の候補を破棄しました。教材と保存済みフィルターは残っています。"; }, () => CanEdit && draft != null);
    }
    private LearningLabel Label => new LearningLabel(Group, FilterName).Normalize();
    private bool TryLabel(out LearningLabel? label)
    {
        try { label = Label; return true; }
        catch (ArgumentException) { label = null; return false; }
    }
    private void Safe(Action operation) { try { operation(); } catch (Exception ex) { Status = ex.Message; } }
    private void NotifyDraft()
    {
        Changed(nameof(Draft)); Changed(nameof(HasDraft)); Changed(nameof(DraftSummary)); Changed(nameof(DraftHint));
        Changed(nameof(CanUseDraft)); Changed(nameof(CanRollback)); CommandManager.InvalidateRequerySuggested();
    }
    private void SetContext(LearningLabel value)
    {
        group = value.Group; filterName = value.Name; Changed(nameof(Group)); Changed(nameof(FilterName));
        selectedLabel = KnownLabels.FirstOrDefault(l => l.Label == value); Changed(nameof(SelectedLabel));
        IsNewClassification = selectedLabel == null; ContextChanged();
    }
    private void ContextChanged()
    {
        knownCorpusRevision = -1; knownFilterRevision = -1; hasExamples = false; hasPrevious = false;
        NotifyDraft(); _ = RefreshAvailabilityAsync();
    }
    public async Task RefreshAvailabilityAsync()
    {
        int generation = ++contextGeneration;
        if (disposed) return;
        checking = true; NotifyDraft();
        try
        {
            var label = Label;
            var result = await Task.Run(() =>
            {
                var snapshot = corpus.Read(); var current = filters.Read(label);
                bool previous = current is { ParentRevision: > 0 } && filters.ReadPrevious(label) != null;
                return (snapshot.Revision, Current: current?.Revision ?? 0, HasExamples: snapshot.Samples.Any(s => s.Labels.Contains(label)), Previous: previous);
            });
            if (disposed || generation != contextGeneration) return;
            knownCorpusRevision = result.Revision; knownFilterRevision = result.Current; hasExamples = result.HasExamples; hasPrevious = result.Previous;
            AvailabilityHint = hasExamples ? hasPrevious ? "保存済み教材から作成できます。前の保存版にも戻せます。" : "保存済み教材から作成できます。戻せる前の保存版はありません。" : "この分類には教材がありません。動画を選び、取り込んでください。";
        }
        catch (Exception ex)
        {
            if (!disposed && generation == contextGeneration)
            { knownCorpusRevision = -1; knownFilterRevision = -1; hasExamples = false; hasPrevious = false; AvailabilityHint = "利用状態を確認できません。" + ex.Message; }
        }
        finally { if (!disposed && generation == contextGeneration) { checking = false; NotifyDraft(); } }
    }
    public void SetFiles(IEnumerable<string> files, LearningLabel label)
    {
        if (!CanEdit) throw new InvalidOperationException("取込中は教材を変更できません。");
        var validated = label.Normalize(); var next = files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(10001).ToImmutableArray();
        if (next.Length > 10000 || next.Any(p => !LearningInputs.IsVideo(p))) throw new ArgumentException("対応する動画を10,000本以内で選んでください。");
        SetContext(validated); paths = next;
        Rows.Clear(); foreach (var path in paths) Rows.Add(new(Path.GetFileName(path), "取込前", ""));
        RowsHeading = "取り込む教材"; Changed(nameof(InputSummary)); CommandManager.InvalidateRequerySuggested();
    }
    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "同じ切り替わりの教材フォルダーを選択", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        var directory = new DirectoryInfo(dialog.FolderName);
        var label = new LearningLabel(directory.Parent?.Name ?? "マイフィルター", directory.Name).Normalize();
        SetFiles(LearningInputs.Folder(directory.FullName, label).Select(i => i.Path), label);
        Status = "直下の動画を取り込みます。推定した分類を確認してください。以前の候補は保持しています。";
    }
    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Title = "フィルターの教材動画を選択", Multiselect = true, Filter = "動画|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v;*.ts" };
        if (dialog.ShowDialog() == true) SetFiles(paths.Concat(dialog.FileNames), Label);
    }
    private async Task Operation(string message, Func<CancellationToken, Task> operation)
    {
        if (!CanEdit) return;
        IsBusy = true; Status = message; Progress = 0;
        using var source = new CancellationTokenSource(); cancel = source;
        try { await operation(source.Token); }
        catch (OperationCanceledException) { if (!disposed) Status = "中止しました。以前の候補は保持しています。取込済み教材は取込結果を確認してください。"; }
        catch (Exception ex) { if (!disposed) Status = "処理できませんでした。以前の候補は保持しています。" + ex.Message; }
        finally
        {
            cancel = null;
            if (!disposed) { await RefreshAvailabilityAsync(); IsBusy = false; }
        }
    }
    private async Task ImportFromHostAsync()
    {
        try { await ImportAsync(Ymm4FfmpegLocator.CreateBackend()); }
        catch (Exception ex) { Status = ex.Message; }
    }
    public Task ImportAsync(FfmpegBackend backend) => Operation("教材を取り込んでいます。", async token =>
    {
        var label = Label; var input = paths.Select(p => new LearningInput(p, label)).ToArray();
        var report = new Progress<ImportProgress>(p => { if (!disposed && IsBusy) { Status = $"{p.Completed}/{p.Total}  {p.DisplayName} — {p.Stage}"; Progress = p.Total == 0 ? 0 : p.Completed / (double)p.Total; } });
        var result = await Task.Run(() => corpus.ImportAsync(input, backend, report, token), token);
        if (disposed) return;
        LastImport = result; Rows.Clear(); RowsHeading = "教材の取込結果";
        foreach (var item in result.Items)
        {
            string outcome = item.Disposition switch { ImportDisposition.Added => "取込済み", ImportDisposition.MembershipAdded => "分類を追加", ImportDisposition.AlreadyPresent => "登録済み・重複なし", ImportDisposition.Cancelled => "中止・未取込", _ => "取込失敗" };
            Rows.Add(new(item.DisplayName, outcome, item.Error ?? ""));
        }
        Status = $"取込確定 {result.Committed}本 / 失敗 {result.Failed}本 / 中止 {result.Cancelled}本。元動画は変更していません。";
        Progress = 1; await RefreshLabelsAsync();
    });
    public async Task RefreshLabelsAsync()
    {
        try
        {
            var labels = await Task.Run(() => corpus.Read().Samples.SelectMany(s => s.Labels).Distinct().OrderBy(l => l.Group).ThenBy(l => l.Name).ToArray());
            if (disposed) return;
            KnownLabels.Clear(); foreach (var label in labels) KnownLabels.Add(new(label));
            selectedLabel = TryLabel(out var current) ? KnownLabels.FirstOrDefault(l => l.Label == current) : null;
            Changed(nameof(SelectedLabel));
            // A refreshed selection can equal the previous value and bypass its setter.
            // Keep disclosure consistent without changing the entered label or retained draft.
            IsNewClassification = selectedLabel == null;
        }
        catch (Exception ex) { if (!disposed) Status = ex.Message; }
        await RefreshAvailabilityAsync();
    }
    public Task CreateDraftAsync() => Operation("教材の切り替わり傾向から候補を作っています。", async token =>
    {
        var label = Label;
        var next = await Task.Run(() => FilterAuthor.Create(FilterAuthor.Load(corpus, label, token), filters.Read(label), token), token);
        token.ThrowIfCancellationRequested(); if (disposed) return;
        // Publish only after successful generation. Fail/cancel never mutates the previous draft.
        draft = next; LastSaved = null; Rows.Clear(); RowsHeading = $"候補の再判定: {label.Group} / {label.Name}";
        foreach (var item in next.Coverage.Samples)
        {
            string outcome = item.State switch { CoverageState.CoveredCandidate => "候補あり（位置未確認）", CoverageState.HardPositive => "未検出・重点教材", _ => "切替候補なし" };
            string positions = string.Join(" / ", item.Centers.Take(4).Select(s => TimeSpan.FromSeconds(s).ToString("hh\\:mm\\:ss\\.fff")));
            Rows.Add(new(item.DisplayName, outcome, positions));
        }
        Progress = 1; Status = "基準感度で教材を再判定しました。長尺動画で試してから保存できます。"; NotifyDraft();
    });
    private FilterDraft RequireDraft()
    {
        var candidate = draft ?? throw new InvalidOperationException("フィルター候補を作成してください。");
        if (!DraftMatchesContext) throw new InvalidOperationException("保持中の候補は現在の分類・教材・保存版と一致しません。作成時の分類へ戻すか、再生成してください。");
        return candidate;
    }
    public void Preview()
    {
        var candidate = RequireDraft(); useFilter(candidate.Filter);
        Status = "メイン画面に「試用」として追加しました。保存済みフィルターは変更していません。";
    }
    public Task SaveAsync() => Operation("再確認してフィルターを保存しています。", async token =>
    {
        var candidate = RequireDraft();
        var saved = await Task.Run(() => filters.Apply(candidate, token), token);
        if (disposed) return;
        draft = null; LastSaved = saved; Progress = 1; NotifyDraft();
        InstallSaved(saved, $"{saved.Label.Name} を保存しました（第{saved.Revision}版）。");
    });
    public Task RollbackAsync() => Operation("前のフィルターへ戻しています。", async token =>
    {
        var label = Label;
        var restored = await Task.Run(() => { var current = filters.Read(label) ?? throw new InvalidOperationException("保存済みフィルターがありません。"); return filters.Rollback(label, current.Revision, token); }, token);
        if (disposed) return;
        LastSaved = restored; Progress = 1; NotifyDraft();
        InstallSaved(restored, $"第{restored.Revision}版に戻しました。教材と未保存候補は保持しています。");
    });
    private void InstallSaved(TransitionFilter filter, string message)
    {
        try { useFilter(filter); Status = message + "メイン画面で使えます。"; }
        catch (Exception ex) { Status = message + "保存は完了していますが、メイン画面への反映に失敗しました。" + ex.Message; }
    }
    public void Cancel() { try { cancel?.Cancel(); } catch (ObjectDisposedException) { } }
    public void Dispose() { if (disposed) return; disposed = true; contextGeneration++; Cancel(); }
}
