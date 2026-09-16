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
    private bool busy, disposed;
    private string group = "マイフィルター", filterName = "場面切り替わり", status = "切り替わりの前後を含む動画を、まとめて追加できます。";
    private double progress;
    private FilterDraft? draft;
    private LabelChoice? selectedLabel;
    private ImmutableArray<string> paths = [];
    public ObservableCollection<LearningRow> Rows { get; } = [];
    public ObservableCollection<LabelChoice> KnownLabels { get; } = [];
    public LabelChoice? SelectedLabel
    {
        get => selectedLabel;
        set { selectedLabel = value; Changed(); if (value != null && !IsBusy) { Group = value.Label.Group; FilterName = value.Label.Name; } }
    }
    public string Group { get => group; set { if (group == value || IsBusy) return; group = value; Changed(); ClearDraft(); } }
    public string FilterName { get => filterName; set { if (filterName == value || IsBusy) return; filterName = value; Changed(); ClearDraft(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanEdit)); CommandManager.InvalidateRequerySuggested(); } }
    public bool CanEdit => !IsBusy;
    public string Status { get => status; private set { status = value; Changed(); } }
    public double Progress { get => progress; private set { progress = Math.Clamp(value, 0, 1); Changed(); } }
    public string InputSummary => $"追加する動画 {paths.Length}本（元動画は変更・削除しません）";
    public FilterDraft? Draft => draft;
    public TransitionFilter? LastSaved { get; private set; }
    public ImportBatchResult? LastImport { get; private set; }
    public string DraftSummary => draft == null ? "教材を取り込んだら、フィルター候補を作成します。" :
        $"{draft.Filter.Patterns.Length}パターン / 候補あり {draft.Coverage.Covered}本 / 未検出 {draft.Coverage.Hard}本 / 切替候補なし {draft.Coverage.NoTransition}本";
    public ICommand ChooseFolderCommand { get; }
    public ICommand ChooseFilesCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand CancelCommand { get; }

    public LearningModel(CorpusStore corpus, Action<TransitionFilter> useFilter)
    {
        this.corpus = corpus; filters = new(corpus); this.useFilter = useFilter;
        ChooseFolderCommand = new RelayCommand(() => Safe(ChooseFolder), () => !disposed && !IsBusy);
        ChooseFilesCommand = new RelayCommand(() => Safe(ChooseFiles), () => !disposed && !IsBusy);
        ImportCommand = new RelayCommand(() => _ = ImportFromHostAsync(), () => !disposed && !IsBusy && paths.Length > 0);
        CreateCommand = new RelayCommand(() => _ = CreateDraftAsync(), () => !disposed && !IsBusy);
        PreviewCommand = new RelayCommand(() => Safe(Preview), () => !disposed && !IsBusy && draft != null);
        SaveCommand = new RelayCommand(() => _ = SaveAsync(), () => !disposed && !IsBusy && draft != null);
        RollbackCommand = new RelayCommand(() => _ = RollbackAsync(), () => !disposed && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }
    private LearningLabel Label => new LearningLabel(Group, FilterName).Normalize();
    private void Safe(Action operation) { try { operation(); } catch (Exception ex) { Status = ex.Message; } }
    private void ClearDraft() { draft = null; LastSaved = null; Changed(nameof(Draft)); Changed(nameof(DraftSummary)); CommandManager.InvalidateRequerySuggested(); }
    public void SetFiles(IEnumerable<string> files, LearningLabel label)
    {
        if (IsBusy || disposed) throw new InvalidOperationException("取込中は教材を変更できません。");
        var validated = label.Normalize(); var next = files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(10001).ToImmutableArray();
        if (next.Length > 10000 || next.Any(p => !LearningInputs.IsVideo(p))) throw new ArgumentException("対応する動画を10,000本以内で選んでください。");
        Group = validated.Group; FilterName = validated.Name; paths = next; ClearDraft();
        Rows.Clear(); foreach (var path in paths) Rows.Add(new(Path.GetFileName(path), "取込前", ""));
        Changed(nameof(InputSummary)); CommandManager.InvalidateRequerySuggested();
    }
    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "同じ切り替わりの教材フォルダーを選択", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        var directory = new DirectoryInfo(dialog.FolderName);
        var label = new LearningLabel(directory.Parent?.Name ?? "マイフィルター", directory.Name).Normalize();
        SetFiles(LearningInputs.Folder(directory.FullName, label).Select(i => i.Path), label);
        Status = "このフォルダー直下の動画を取り込みます。グループとフィルター名を確認してください。";
    }
    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Title = "フィルターの教材動画を選択", Multiselect = true, Filter = "動画|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v;*.ts" };
        if (dialog.ShowDialog() == true) SetFiles(paths.Concat(dialog.FileNames), Label);
    }
    private async Task Operation(string message, Func<CancellationToken, Task> operation)
    {
        if (disposed || IsBusy) return;
        IsBusy = true; Status = message; Progress = 0;
        using var source = new CancellationTokenSource(); cancel = source;
        try { await operation(source.Token); }
        catch (OperationCanceledException) { if (!disposed) { ClearDraft(); Status = "中止しました。確定していない結果は反映していません。"; } }
        catch (Exception ex) { if (!disposed) { ClearDraft(); Status = ex.Message; } }
        finally { cancel = null; if (!disposed) IsBusy = false; }
    }
    private async Task ImportFromHostAsync()
    {
        try { await ImportAsync(Ymm4FfmpegLocator.CreateBackend()); }
        catch (Exception ex) { Status = ex.Message; }
    }
    public Task ImportAsync(FfmpegBackend backend) => Operation("教材を取り込んでいます。", async token =>
    {
        var label = Label; var input = paths.Select(p => new LearningInput(p, label)).ToArray(); ClearDraft();
        var report = new Progress<ImportProgress>(p => { if (!disposed && IsBusy) { Status = $"{p.Completed}/{p.Total}  {p.DisplayName} — {p.Stage}"; Progress = p.Total == 0 ? 0 : p.Completed / (double)p.Total; } });
        var result = await Task.Run(() => corpus.ImportAsync(input, backend, report, token), token);
        if (disposed) return;
        LastImport = result; Rows.Clear();
        foreach (var item in result.Items)
        {
            string outcome = item.Disposition switch { ImportDisposition.Added => "取込済み", ImportDisposition.MembershipAdded => "分類を追加", ImportDisposition.AlreadyPresent => "登録済み・重複なし", ImportDisposition.Cancelled => "中止・未取込", _ => "取込失敗" };
            Rows.Add(new(item.DisplayName, outcome, item.Error ?? ""));
        }
        Status = $"取込確定 {result.Committed}本 / 失敗 {result.Failed}本 / 中止 {result.Cancelled}本。元動画は変更していません。";
        Progress = 1;
        await RefreshLabelsAsync();
    });
    public async Task RefreshLabelsAsync()
    {
        try
        {
            var labels = await Task.Run(() => corpus.Read().Samples.SelectMany(s => s.Labels).Distinct().OrderBy(l => l.Group).ThenBy(l => l.Name).ToArray());
            if (disposed) return;
            KnownLabels.Clear(); foreach (var label in labels) KnownLabels.Add(new(label));
        }
        catch (Exception ex) { if (!disposed) Status = ex.Message; }
    }
    public Task CreateDraftAsync() => Operation("教材の切り替わり傾向から候補を作っています。", async token =>
    {
        var label = Label; ClearDraft();
        var next = await Task.Run(() => FilterAuthor.Create(FilterAuthor.Load(corpus, label, token), filters.Read(label), token), token);
        token.ThrowIfCancellationRequested(); if (disposed) return;
        draft = next; Rows.Clear();
        foreach (var item in next.Coverage.Samples)
        {
            string outcome = item.State switch { CoverageState.CoveredCandidate => "候補あり（位置未確認）", CoverageState.HardPositive => "未検出・重点教材", _ => "切替候補なし" };
            string positions = string.Join(" / ", item.Centers.Take(4).Select(s => TimeSpan.FromSeconds(s).ToString("hh\\:mm\\:ss\\.fff")));
            Rows.Add(new(item.DisplayName, outcome, positions));
        }
        Progress = 1; Status = "基準感度で教材を再判定しました。長尺動画で試してから保存できます。";
        Changed(nameof(Draft)); Changed(nameof(DraftSummary));
    });
    public void Preview()
    {
        var candidate = draft ?? throw new InvalidOperationException("フィルター候補を作成してください。");
        useFilter(candidate.Filter);
        Status = "メイン画面に「試用」として追加しました。保存済みフィルターは変更していません。";
    }
    public Task SaveAsync() => Operation("再確認してフィルターを保存しています。", async token =>
    {
        var candidate = draft ?? throw new InvalidOperationException("フィルター候補を作成してください。");
        var saved = await Task.Run(() => filters.Apply(candidate, token), token);
        if (disposed) return;
        draft = null; LastSaved = saved; useFilter(saved); Progress = 1;
        Status = $"{saved.Label.Name} を保存しました（第{saved.Revision}版）。メイン画面で使えます。";
        Changed(nameof(Draft)); Changed(nameof(DraftSummary));
    });
    public Task RollbackAsync() => Operation("前のフィルターへ戻しています。", async token =>
    {
        var label = Label;
        var restored = await Task.Run(() => { var current = filters.Read(label) ?? throw new InvalidOperationException("保存済みフィルターがありません。"); return filters.Rollback(label, current.Revision, token); }, token);
        if (disposed) return;
        ClearDraft(); LastSaved = restored; useFilter(restored); Progress = 1; Status = $"第{restored.Revision}版に戻しました。教材は削除していません。";
    });
    public void Cancel() { try { cancel?.Cancel(); } catch (ObjectDisposedException) { } }
    public void Dispose() { if (disposed) return; disposed = true; Cancel(); }
}
