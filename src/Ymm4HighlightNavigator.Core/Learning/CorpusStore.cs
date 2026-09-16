using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ymm4HighlightNavigator.Core;

public sealed record LearningLabel(string Group, string Name)
{
    public LearningLabel Normalize() => new(Clean(Group), Clean(Name));
    private static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100 || text.Any(char.IsControl))
            throw new ArgumentException("グループとフィルター名を100文字以内で入力してください。");
        return text.Trim().Normalize(NormalizationForm.FormC);
    }
    [JsonIgnore] public string Key => LearningJson.Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Normalize())));
}

public sealed record LearningInput(string Path, LearningLabel Label);
public sealed record LearningSample(string Id, string SourceHash, string PackHash, string Extractor,
    int VideoFps, double StartSeconds, double EndSeconds, ImmutableArray<LearningLabel> Labels,
    ImmutableArray<string> OriginalNames, DateTimeOffset ImportedAt);
public sealed record CorpusSnapshot(int Schema, long Revision, ImmutableArray<LearningSample> Samples);
public enum ImportDisposition { Added, MembershipAdded, AlreadyPresent, Failed, Cancelled }
public sealed record ImportItemResult(string DisplayName, string? SampleId, ImportDisposition Disposition, string? Error);
public sealed record ImportProgress(int Completed, int Total, string DisplayName, string Stage);
public sealed record ImportBatchResult(ImmutableArray<ImportItemResult> Items)
{
    public int Committed => Items.Count(x => x.Disposition is ImportDisposition.Added or ImportDisposition.MembershipAdded or ImportDisposition.AlreadyPresent);
    public int Failed => Items.Count(x => x.Disposition == ImportDisposition.Failed);
    public int Cancelled => Items.Count(x => x.Disposition == ImportDisposition.Cancelled);
}

public static class LearningInputs
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts" };
    public static bool IsVideo(string path) => Extensions.Contains(System.IO.Path.GetExtension(path));
    // Immediate children only: picking X4/Battle never silently labels all sibling folders Battle.
    public static ImmutableArray<LearningInput> Folder(string folder, LearningLabel label)
    {
        label = label.Normalize(); folder = System.IO.Path.GetFullPath(folder);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("教材フォルダーが見つかりません。");
        RejectLinks(folder);
        var files = Directory.EnumerateFiles(folder).Where(IsVideo).Order(StringComparer.Ordinal).Take(10001).ToArray();
        if (files.Length > 10000) throw new InvalidDataException("一度の取込は10,000本までです。フォルダーを分けてください。");
        return files.Select(f => new LearningInput(f, label)).ToImmutableArray();
    }
    internal static void RejectLinks(string path)
    {
        string? current = System.IO.Path.GetFullPath(path);
        while (current != null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("リンク経由の教材・保存先はこの版では対応していません。");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }
}

/// <summary>Non-destructive intake. Immutable Packs are published before a checksummed catalog commit.</summary>
public sealed class CorpusStore
{
    public string Root { get; }
    private string CatalogPath => System.IO.Path.Combine(Root, "corpus.json");
    public CorpusStore(string root) => Root = System.IO.Path.GetFullPath(root);
    internal FileStream AcquireWriter()
    {
        Directory.CreateDirectory(Root); LearningInputs.RejectLinks(Root);
        return new FileStream(System.IO.Path.Combine(Root, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public CorpusSnapshot Read()
    {
        if (!Directory.Exists(Root)) return new(1, 0, []);
        LearningInputs.RejectLinks(Root);
        if (!File.Exists(CatalogPath)) return new(1, 0, []);
        var snapshot = LearningJson.Read<CorpusSnapshot>(CatalogPath); Validate(snapshot); return snapshot;
    }
    private static void Validate(CorpusSnapshot value)
    {
        if (value.Schema != 1 || value.Revision < 0 || value.Samples.IsDefault || value.Samples.Length > 10000)
            throw new InvalidDataException("教材一覧の形式が対応していません。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in value.Samples)
        {
            if (sample is null || !LearningJson.IsHash(sample.Id) || !ids.Add(sample.Id) || !LearningJson.IsHash(sample.SourceHash) || !LearningJson.IsHash(sample.PackHash)
                || sample.Extractor != FeatureFormat.Extractor || sample.VideoFps is not (1 or 2 or 4)
                || sample.Labels.IsDefaultOrEmpty || sample.Labels.Length > 100 || sample.OriginalNames.IsDefaultOrEmpty || sample.OriginalNames.Length > 32)
                throw new InvalidDataException("教材一覧の項目が壊れています。");
            Guard.Range(sample.StartSeconds, sample.EndSeconds);
            if (sample.Labels.Any(l => l is null || l != l.Normalize()) || sample.Labels.Distinct().Count() != sample.Labels.Length)
                throw new InvalidDataException("教材の分類情報が壊れています。");
            if (sample.OriginalNames.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 260 || n.Contains('/') || n.Contains('\\')))
                throw new InvalidDataException("教材表示名が壊れています。");
            if (sample.Id != Key(sample.SourceHash, sample.Extractor, sample.VideoFps, sample.StartSeconds, sample.EndSeconds))
                throw new InvalidDataException("教材と特徴形式の識別情報が一致しません。");
        }
    }
    private string PackPath(string id)
    {
        if (!LearningJson.IsHash(id)) throw new InvalidDataException("不正な教材識別子です。");
        return System.IO.Path.Combine(Root, "packs", id + ".navfp");
    }
    public FeaturePack LoadPack(LearningSample sample)
    {
        string path = PackPath(sample.Id);
        if (!File.Exists(path)) throw new FileNotFoundException("登録済み教材の特徴ファイルがありません。", path);
        LearningInputs.RejectLinks(path);
        if (LearningJson.FileHash(path) != sample.PackHash) throw new InvalidDataException("教材の特徴ファイルが変更・破損しています。");
        var pack = PackStore.Load(path);
        if (sample.SourceHash != pack.Header.SourceHash || sample.Id != Key(pack.Header))
            throw new InvalidDataException("教材一覧と特徴ファイルが一致しません。");
        return pack;
    }
    public static string Key(FeatureHeader h) => Key(h.SourceHash, h.Extractor, h.VideoFps, h.StartSeconds, h.EndSeconds);
    private static string Key(string hash, string extractor, int fps, double start, double end)
        => LearningJson.Hash(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{hash.ToLowerInvariant()}|{extractor}|{fps}|{start:R}|{end:R}")));

    public ImportItemResult RegisterPack(FeaturePack pack, LearningLabel label, string originalName, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); pack.Validate(); label = label.Normalize();
        originalName = System.IO.Path.GetFileName(originalName);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 260) throw new ArgumentException("教材の表示名が必要です。");
        using var writer = AcquireWriter();
        var snapshot = Read(); string id = Key(pack.Header), path = PackPath(id);
        var existing = snapshot.Samples.FirstOrDefault(s => s.Id == id);
        if (existing != null)
        {
            _ = LoadPack(existing);
            if (existing.Labels.Contains(label)) return new(originalName, id, ImportDisposition.AlreadyPresent, null);
        }
        if (File.Exists(path))
        {
            LearningInputs.RejectLinks(path);
            if (Key(PackStore.Load(path).Header) != id) throw new InvalidDataException("未登録の特徴ファイルが教材と一致しません。");
        }
        else PackStore.Save(path, pack, token);
        token.ThrowIfCancellationRequested();
        var entry = existing == null
            ? new LearningSample(id, pack.Header.SourceHash, LearningJson.FileHash(path), pack.Header.Extractor, pack.Header.VideoFps,
                pack.Header.StartSeconds, pack.Header.EndSeconds, [label], [originalName], DateTimeOffset.UtcNow)
            : existing with { Labels = existing.Labels.Add(label), OriginalNames = existing.OriginalNames.Contains(originalName) || existing.OriginalNames.Length >= 32 ? existing.OriginalNames : existing.OriginalNames.Add(originalName) };
        var samples = existing == null ? snapshot.Samples.Add(entry) : snapshot.Samples.SetItem(snapshot.Samples.IndexOf(existing), entry);
        var next = new CorpusSnapshot(1, checked(snapshot.Revision + 1), samples); Validate(next);
        LearningJson.Write(CatalogPath, next, Validate, token);
        return new(originalName, id, existing == null ? ImportDisposition.Added : ImportDisposition.MembershipAdded, null);
    }

    public async Task<ImportBatchResult> ImportAsync(IEnumerable<LearningInput> requested, FfmpegBackend backend,
        IProgress<ImportProgress>? progress = null, CancellationToken token = default)
    {
        var input = requested.Take(10001).ToArray();
        if (input.Length > 10000) throw new ArgumentException("一度の取込件数が多すぎます。");
        var results = ImmutableArray.CreateBuilder<ImportItemResult>();
        for (int i = 0; i < input.Length; i++)
        {
            var item = input[i]; string name = System.IO.Path.GetFileName(item.Path);
            if (token.IsCancellationRequested) { results.Add(new(name, null, ImportDisposition.Cancelled, null)); continue; }
            try
            {
                string path = System.IO.Path.GetFullPath(item.Path);
                if (!LearningInputs.IsVideo(path)) throw new NotSupportedException("対応するローカル動画を指定してください。");
                LearningInputs.RejectLinks(path); item.Label.Normalize();
                var before = new FileInfo(path); long bytes = before.Length, write = before.LastWriteTimeUtc.Ticks;
                progress?.Report(new(i, input.Length, name, "教材を確認"));
                string hash;
                await using (var stream = File.OpenRead(path)) hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).ToLowerInvariant();
                var info = await backend.ProbeAsync(path, token).ConfigureAwait(false);
                string id = Key(hash, FeatureFormat.Extractor, 2, 0, info.DurationSeconds);
                var existing = Read().Samples.FirstOrDefault(s => s.Id == id);
                FeaturePack pack;
                if (existing != null) pack = LoadPack(existing);
                else
                {
                    var report = progress == null ? null : new InlineProgress<AnalysisProgress>(p => progress.Report(new(i, input.Length, name, p.Stage)));
                    pack = await backend.ExtractAsync(path, progress: report, token: token).ConfigureAwait(false);
                    if (pack.Header.SourceHash != hash) throw new IOException("確認中に教材動画が変更されました。");
                }
                before.Refresh();
                if (!before.Exists || before.Length != bytes || before.LastWriteTimeUtc.Ticks != write) throw new IOException("取込中に教材動画が変更されました。");
                results.Add(RegisterPack(pack, item.Label, name, token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { results.Add(new(name, null, ImportDisposition.Cancelled, null)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException or JsonException or TimeoutException or UnauthorizedAccessException)
            { results.Add(new(name, null, ImportDisposition.Failed, ex.Message)); }
            progress?.Report(new(i + 1, input.Length, name, "取込結果を確認"));
        }
        return new(results.ToImmutable());
    }
}

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

internal static class LearningJson
{
    private sealed record Envelope(int Schema, string Sha256, byte[] Payload);
    private const int MaxBytes = 24 * 1024 * 1024;
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true
    };
    internal static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    internal static T Read<T>(string path)
    {
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > MaxBytes) throw new InvalidDataException("保存データのサイズが対応範囲外です。");
        var envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllBytes(path), Options) ?? throw new InvalidDataException("保存データが空です。");
        if (envelope.Schema != 1 || envelope.Payload == null || !IsHash(envelope.Sha256) || Hash(envelope.Payload) != envelope.Sha256)
            throw new InvalidDataException("保存データの整合性を確認できません。");
        return JsonSerializer.Deserialize<T>(envelope.Payload, Options) ?? throw new InvalidDataException("保存内容が空です。");
    }
    internal static void Write<T>(string path, T value, Action<T> validate, CancellationToken token, bool overwrite = true)
    {
        token.ThrowIfCancellationRequested(); validate(value);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, Hash(payload), payload), Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("保存データが大きすぎます。");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            validate(Read<T>(temporary)); token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
