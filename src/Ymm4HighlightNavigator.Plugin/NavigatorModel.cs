using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Ymm4HighlightNavigator.Core;
using YukkuriMovieMaker.Plugin;

namespace Ymm4HighlightNavigator.Plugin;

public sealed class NavigatorPlugin : IToolPlugin
{
    public string Name => "YMM4見どころナビ";
    public Type ViewModelType => typeof(NavigatorModel);
    public Type ViewType => typeof(NavigatorView);
    public bool AllowMultipleInstances => false;
}

public class NotifyModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class ProfileChoice(SceneProfile profile) : NotifyModel
{
    // For a learned filter Profile carries only identity/display metadata. Query dispatch MUST use Learned.
    public SceneProfile Profile { get; } = profile;
    public TransitionFilter? Learned { get; }
    public ProfileChoice(TransitionFilter learned) : this(new SceneProfile(learned.Id,
        learned.Label.Group + " / " + learned.Label.Name + (learned.Revision == 0 ? "（試用）" : ""), []))
        => Learned = learned;
    private bool enabled = true;
    private string count = "";
    public string Name => Profile.Name;
    public bool Enabled { get => enabled; set { if (enabled == value) return; enabled = value; Changed(); } }
    public string Count { get => count; internal set { count = value; Changed(); } }
}

public sealed class Candidate(ReviewSourceSession session, ReviewEpisode episode, ImmutableArray<string> profiles, string filename) : NotifyModel
{
    public Guid TargetId { get; } = session.ReviewTargetId;
    public string SourceKey { get; } = session.SourceKey;
    public TimeRange Source { get; } = episode.Range;
    public ImmutableArray<ProfileHit> Hits { get; } = episode.Hits;
    public double AnchorSourceTime { get; } = episode.AnchorSourceTime;
    public StableReviewOrder StableOrderKey { get; } = new(session.CaptureOrder, episode.AnchorSourceTime);
    public ReviewCandidateIdentity Identity { get; } = ReviewCandidateIdentity.Create(session.ReviewTargetId, session.SourceKey, episode.AnchorSourceTime);
    public int? Frame { get; private set; }
    public int Fps { get; private set; } = session.Capture.Fps;
    public RebindStatus Availability { get; private set; } = RebindStatus.Unavailable;
    public string? UnavailableReason { get; private set; }
    public bool Available => Frame.HasValue && Availability == RebindStatus.Available;
    public ImmutableArray<string> Profiles { get; } = profiles;
    private bool visited;
    public bool Visited { get => visited; internal set { if (visited == value) return; visited = value; Changed(); Changed(nameof(Display)); } }
    internal void SetProjection(CandidateProjection projection)
    {
        Frame = projection.Frame; Fps = projection.Fps; Availability = projection.Status; UnavailableReason = projection.Reason;
        Changed(nameof(Frame)); Changed(nameof(Availability)); Changed(nameof(Available)); Changed(nameof(UnavailableReason)); Changed(nameof(Display));
    }
    public string Display
    {
        get
        {
            string position = Frame is int frame ? TimeSpan.FromSeconds(frame / (double)Fps).ToString(@"hh\:mm\:ss\.fff") : "移動不可";
            return $"{(Visited ? "✓ " : "")}{position}  {string.Join(" / ", Profiles)}  {filename}";
        }
    }
}

public sealed class RelayCommand(Action action, Func<bool> allowed) : ICommand
{
    public bool CanExecute(object? parameter) => allowed();
    public void Execute(object? parameter) { if (CanExecute(parameter)) action(); }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}

public sealed partial class NavigatorModel : NotifyModel, ITimelineToolViewModel, IToolViewModel, IDisposable
{
    private sealed record Analyzed(string SourceKey, FeatureTable Table, TransitionIndex Transitions);
    private sealed record QueryResult(ReviewSourceSession Session, ReviewEpisode Episode);
    private sealed class ForwardProgress(Action<AnalysisProgress> report) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => report(value);
    }
    private readonly TargetAdapter adapter = new();
    private readonly List<Analyzed> analyzed = [];
    private readonly HashSet<ReviewCandidateIdentity> visited = [];
    private int backendCallCount;
    private CancellationTokenSource? analysisCancel, queryCancel;
    private int generation, queryGeneration;
    private bool disposed, busy, querying, hasQueryResult;
    private double sensitivity = 1, progress;
    private string status = "YMM4で動画を選び、対象に追加してください。";
    private Candidate? selected;
    private int hitTotal;
    public string Title => "YMM4見どころナビ";
    public bool CanSuspend => false;
    public ObservableCollection<ProfileChoice> Profiles { get; } = [];
    public ObservableCollection<Candidate> Candidates { get; } = [];
    public ImmutableArray<TargetSnapshot> Targets => adapter.Snapshots;
    public int DecodedRangeCount => analyzed.Count;
    public int AnalysisBackendCallCount => Volatile.Read(ref backendCallCount);
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanConfigure)); Commands(); } }
    public bool IsQuerying { get => querying; private set { querying = value; Changed(); Changed(nameof(CandidateSummary)); Commands(); } }
    public bool CanConfigure => !IsBusy;
    public double Progress { get => progress; private set { progress = Math.Clamp(value, 0, 1); Changed(); } }
    public string Status { get => status; private set { status = value; Changed(); } }
    public string TargetSummary => $"対象 {Targets.Length}個";
    public string CandidateSummary => IsQuerying ? "候補を更新中…" : hasQueryResult ? $"候補 {Candidates.Count}件 / ヒット計 {hitTotal}" : "候補は未計算です";
    public double Sensitivity { get => sensitivity; set { if (!double.IsFinite(value) || value < .25 || value > 2 || value == sensitivity) return; sensitivity = value; Changed(); _ = RequeryAsync(); } }
    public Candidate? Selected { get => selected; set { selected = value; Changed(); Commands(); } }
    public ICommand CaptureCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand JumpCommand { get; }

    public NavigatorModel()
    {
        Profiles.Add(new(new SceneProfile("seed.visual", "映像の急変", [new(FeatureAxis.Delta, .8f, .08f)])));
        Profiles.Add(new(new SceneProfile("seed.audio", "音の強い場面", [new(FeatureAxis.AudioPeak, .8f, .05f)])));
        Profiles.Add(new(new SceneProfile("seed.brightness", "明るい場面", [new(FeatureAxis.Luma, .9f, .8f)])));
        foreach (var profile in Profiles) profile.PropertyChanged += ProfileChanged;
        CaptureCommand = new RelayCommand(() => Safe(CaptureSelection), () => !disposed && !IsBusy && adapter.HasTimeline);
        AnalyzeCommand = new RelayCommand(() => _ = AnalyzeFromUiAsync(), () => !disposed && !IsBusy && Targets.Length > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy || IsQuerying);
        PreviousCommand = new RelayCommand(() => Safe(() => Move(-1)), () => !disposed && !IsBusy && !IsQuerying && Candidates.Count > 0);
        NextCommand = new RelayCommand(() => Safe(() => Move(1)), () => !disposed && !IsBusy && !IsQuerying && Candidates.Count > 0);
        JumpCommand = new RelayCommand(() => Safe(JumpSelected), () => !disposed && !IsBusy && !IsQuerying && Selected != null);
    }
    private void ProfileChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ProfileChoice.Enabled)) _ = RequeryAsync(); }
    private static void Commands() => CommandManager.InvalidateRequerySuggested();
    private void Safe(Action action) { try { action(); } catch (Exception ex) { Status = ex.Message; } }

    public void SetTimelineToolInfo(TimelineToolInfo info)
    {
        if (disposed) return;
        if (adapter.Attach(info.Timeline))
        {
            generation++; Cancel(); analyzed.Clear(); visited.Clear(); Candidates.Clear(); Selected = null; hitTotal = 0; hasQueryResult = false;
            Status = "タイムラインが変わりました。動画を対象に追加してください。";
            Changed(nameof(TargetSummary)); Changed(nameof(CandidateSummary)); Commands();
        }
    }
    public void CaptureSelection()
    {
        if (disposed || IsBusy) throw new InvalidOperationException("解析を停止してから対象を変更してください。");
        adapter.CaptureSelection(); generation++; queryCancel?.Cancel(); analyzed.Clear(); visited.Clear(); Candidates.Clear(); Selected = null; hitTotal = 0; hasQueryResult = false;
        foreach (var p in Profiles) p.Count = "";
        Status = "対象を固定しました。選択を変えても対象は変わりません。";
        Changed(nameof(TargetSummary)); Changed(nameof(CandidateSummary)); Commands();
    }
    private async Task AnalyzeFromUiAsync()
    {
        try { await AnalyzeAsync(Ymm4FfmpegLocator.CreateBackend()); }
        catch (Exception ex) { Status = "解析を開始できませんでした。" + ex.Message; }
    }
    public async Task AnalyzeAsync(FfmpegBackend backend)
    {
        if (disposed || IsBusy) throw new InvalidOperationException("解析は既に実行中です。");
        adapter.ValidateCurrent(); var targets = Targets;
        if (targets.IsEmpty) throw new InvalidOperationException("対象動画を追加してください。");
        int stamp = generation;
        queryCancel?.Cancel();
        analysisCancel = new(); var token = analysisCancel.Token;
        analyzed.Clear(); Candidates.Clear(); Selected = null; hitTotal = 0; hasQueryResult = false; Changed(nameof(CandidateSummary));
        foreach (var p in Profiles) p.Count = "未計算";
        IsBusy = true; Progress = 0; Status = "録画の特徴を解析しています。";
        try
        {
            var work = targets.GroupBy(t => t.SourceKey, StringComparer.OrdinalIgnoreCase)
                .SelectMany(g => TimeRange.Union(g.Select(t => t.SourceRange)).Select(r => (Source: g.Key, Range: r))).ToArray();
            IProgress<AnalysisProgress> report = new Progress<AnalysisProgress>(p => { if (!disposed && stamp == generation && IsBusy) { Progress = p.Fraction; Status = p.Stage; } });
            var completed = await Task.Run(async () =>
            {
                var rows = new List<Analyzed>();
                for (int index = 0; index < work.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    var item = work[index]; int currentIndex = index;
                    var rangeProgress = new ForwardProgress(p => report.Report(p with { Fraction = (currentIndex + p.Fraction) / work.Length }));
                    Interlocked.Increment(ref backendCallCount);
                    var pack = await backend.ExtractAsync(item.Source, item.Range.Start, item.Range.End, progress: rangeProgress, token: token).ConfigureAwait(false);
                    rows.Add(new(item.Source, new FeatureTable(pack), TransitionIndex.Build(pack, token)));
                }
                return rows;
            }, token);
            token.ThrowIfCancellationRequested();
            if (disposed || stamp != generation) return;
            adapter.ValidateCurrent();
            analyzed.Clear(); analyzed.AddRange(completed); Progress = 1; Status = "解析が完了しました。";
        }
        catch (OperationCanceledException) { if (!disposed && stamp == generation) Status = "解析を中止しました。未完了データは反映していません。"; }
        catch (Exception ex) { if (!disposed && stamp == generation) Status = "解析できませんでした。" + ex.Message; throw; }
        finally { IsBusy = false; analysisCancel?.Dispose(); analysisCancel = null; }
        if (!disposed && stamp == generation) await RequeryAsync();
    }

    public async Task RequeryAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || IsBusy || analyzed.Count == 0) return;
        queryCancel?.Cancel(); queryCancel?.Dispose(); queryCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); var token = queryCancel.Token;
        int stamp = generation, q = ++queryGeneration;
        var sources = analyzed.ToArray(); var sessions = adapter.Sessions;
        var profiles = Profiles.Select(p => (p.Profile, p.Learned, p.Enabled)).ToArray(); double sens = Sensitivity;
        IsQuerying = true;
        try
        {
            var result = await Task.Run(() =>
            {
                var output = new List<QueryResult>(); var counts = profiles.ToDictionary(p => p.Profile.Id, _ => 0);
                var unavailable = profiles.ToDictionary(p => p.Profile.Id, _ => 0);
                foreach (var source in sources)
                {
                    token.ThrowIfCancellationRequested();
                    var evaluations = profiles.Select(p => (Choice: p, Evaluation: p.Learned is null
                        ? ProfileEvaluator.Evaluate(source.Table, p.Profile, sens)
                        : TransitionMatcher.Evaluate(source.Transitions, p.Learned, sens, token))).ToArray();
                    foreach (var e in evaluations) if (!e.Evaluation.Compatible) unavailable[e.Choice.Profile.Id]++;
                    foreach (var session in sessions.Where(s => ReviewSourceIdentity.Same(s.SourceKey, source.SourceKey)))
                    {
                        var target = session.Capture;
                        var included = new List<ProfileHit>();
                        foreach (var e in evaluations)
                        foreach (var hit in e.Evaluation.Hits)
                        {
                            var clipped = hit.Clip(target.SourceRange);
                            if (clipped is null) continue;
                            counts[e.Choice.Profile.Id]++;
                            if (e.Choice.Enabled) included.Add(clipped);
                        }
                        output.AddRange(EpisodeUnion.Build(included).Episodes.Select(e => new QueryResult(session, e)));
                    }
                }
                return (output, counts, unavailable);
            }, token);
            token.ThrowIfCancellationRequested();
            if (disposed || stamp != generation || q != queryGeneration) return;
            adapter.RefreshCurrent();
            var next = new List<Candidate>();
            foreach (var row in result.output)
            {
                var names = row.Episode.ProfileIds.Select(id => profiles.Single(p => p.Profile.Id == id).Profile.Name).ToImmutableArray();
                var candidate = new Candidate(row.Session, row.Episode, names, Path.GetFileName(row.Session.SourceKey));
                candidate.SetProjection(adapter.ProjectCurrent(candidate.TargetId, candidate.AnchorSourceTime));
                candidate.Visited = visited.Contains(candidate.Identity);
                next.Add(candidate);
            }
            var old = Selected;
            Candidates.Clear(); foreach (var item in next.OrderBy(c => c.StableOrderKey)) Candidates.Add(item);
            Selected = old == null ? null : Candidates.FirstOrDefault(c => c.Identity == old.Identity);
            hitTotal = profiles.Where(p => p.Enabled).Sum(p => result.counts[p.Profile.Id]); hasQueryResult = true;
            foreach (var p in Profiles) p.Count = $"{result.counts[p.Profile.Id]}件" + (result.unavailable[p.Profile.Id] > 0 ? "（一部素材は非対応）" : "");
            Changed(nameof(CandidateSummary));
            Status = Candidates.Count == 0 ? "候補がありません。フィルターをONにするか、感度を広げてください。" : Candidates.All(c => !c.Available) ? AllUnavailableStatus() : "前・次で候補へ移動できます。";
        }
        catch (OperationCanceledException)
        {
            if (!disposed && stamp == generation && q == queryGeneration)
            {
                Candidates.Clear(); Selected = null; hitTotal = 0; hasQueryResult = false;
                foreach (var p in Profiles) p.Count = "未計算";
                Changed(nameof(CandidateSummary)); Status = "候補の更新を中止しました。フィルターか感度を変更すると再計算できます。";
            }
        }
        catch (Exception ex) { if (!disposed && stamp == generation && q == queryGeneration) { Candidates.Clear(); Selected = null; hitTotal = 0; hasQueryResult = false; Changed(nameof(CandidateSummary)); Status = ex.Message; } }
        finally { if (q == queryGeneration) IsQuerying = false; }
    }

    public void RefreshProjections()
    {
        adapter.RefreshCurrent();
        foreach (var candidate in Candidates) candidate.SetProjection(adapter.ProjectCurrent(candidate.TargetId, candidate.AnchorSourceTime));
        Changed(nameof(CandidateSummary));
    }
    private string AllUnavailableStatus() => "現在移動できる候補がありません。" + Candidates.FirstOrDefault()?.UnavailableReason;
    public void JumpSelected()
    {
        var candidate = Selected ?? throw new InvalidOperationException("候補を選んでください。");
        RefreshProjections();
        if (!candidate.Available) throw new InvalidOperationException(candidate.UnavailableReason);
        // Never jump to Candidate.Frame: it is display-only and may have been projected before an edit.
        candidate.SetProjection(adapter.JumpAnchor(candidate.TargetId, candidate.AnchorSourceTime));
        visited.Add(candidate.Identity); candidate.Visited = true;
        Status = "候補へ移動しました。";
    }
    public void Move(int direction)
    {
        if (Candidates.Count == 0 || direction == 0) return;
        RefreshProjections();
        int current = Selected == null ? (direction > 0 ? -1 : Candidates.Count) : Candidates.IndexOf(Selected);
        int? next = ReviewNavigation.Next(Candidates.Count, current, direction, i => Candidates[i].Available);
        if (next is null)
        {
            Status = Candidates.All(c => !c.Available) ? AllUnavailableStatus() : direction > 0 ? "最後の移動可能な候補です。" : "最初の移動可能な候補です。";
            return;
        }
        Selected = Candidates[next.Value]; JumpSelected();
    }
    public void Cancel() { analysisCancel?.Cancel(); queryCancel?.Cancel(); }
    public ToolState SaveState() => new() { Title = Title };
    public void LoadState(ToolState stateData) { }
    public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested { add { } remove { } }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; Cancel(); CloseLearningSurface();
        foreach (var p in Profiles) p.PropertyChanged -= ProfileChanged;
        analyzed.Clear(); visited.Clear(); Candidates.Clear(); adapter.Attach(null);
    }
}
