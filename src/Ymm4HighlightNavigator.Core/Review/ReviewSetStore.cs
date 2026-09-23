using System.Collections.Immutable;

namespace Ymm4HighlightNavigator.Core;

public sealed record ReviewSettingsSnapshot(int Schema, long Revision, ImmutableArray<ReviewSet> Sets,
    ImmutableArray<ReviewFilterPresentation> Presentations);

/// <summary>Separate settings authority; reuses the existing verified JSON publication primitive.</summary>
public sealed class ReviewSetStore(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    private string DataPath => Path.Combine(Root, "review-settings.json");

    public ReviewSettingsSnapshot Read()
    {
        if (!Directory.Exists(Root)) return new(1, 0, [], []);
        LearningInputs.RejectLinks(Root);
        if (!File.Exists(DataPath)) return new(1, 0, [], []);
        LearningInputs.RejectLinks(DataPath);
        var value = LearningJson.Read<ReviewSettingsSnapshot>(DataPath); Validate(value); return value;
    }

    private static bool SamePath(ImmutableArray<string> left, ImmutableArray<string> right)
    {
        var a = left.IsDefault ? ImmutableArray<string>.Empty : left;
        var b = right.IsDefault ? ImmutableArray<string>.Empty : right;
        return a.Length == b.Length && a.Zip(b).All(x => string.Equals(x.First, x.Second, StringComparison.OrdinalIgnoreCase));
    }

    private static void Validate(ReviewSettingsSnapshot value)
    {
        if (value.Schema != 1 || value.Revision < 0 || value.Sets.IsDefault || value.Sets.Length > 500
            || value.Presentations.IsDefault || value.Presentations.Length > 2000)
            throw new InvalidDataException("確認設定の保存形式が対応していません。");
        foreach (var set in value.Sets)
        {
            if (set is null || set.IsBuiltIn || set.Configuration is null) throw new InvalidDataException("見たいものの保存内容が不正です。");
            var normalized = set.Normalize();
            if (normalized.Name != set.Name || !normalized.Configuration.Filters.SequenceEqual(set.Configuration.Filters)
                || !normalized.ClassificationPath.SequenceEqual(set.ClassificationPath))
                throw new InvalidDataException("見たいものの保存内容が正規化されていません。");
        }
        if (value.Sets.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != value.Sets.Length)
            throw new InvalidDataException("見たいものの識別子が重複しています。");
        for (int i = 0; i < value.Sets.Length; i++)
        for (int j = i + 1; j < value.Sets.Length; j++)
            if (SamePath(value.Sets[i].ClassificationPath, value.Sets[j].ClassificationPath)
                && string.Equals(value.Sets[i].Name, value.Sets[j].Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("同じ分類に同じ名前の見たいものが重複しています。");
        foreach (var item in value.Presentations)
            if (item is null || item != item.Normalize()) throw new InvalidDataException("フィルター表示情報が不正です。");
        if (value.Presentations.Select(p => p.FilterId).Distinct(StringComparer.Ordinal).Count() != value.Presentations.Length)
            throw new InvalidDataException("フィルター表示情報が重複しています。");
    }

    private ReviewSettingsSnapshot Update(long expectedRevision, Func<ReviewSettingsSnapshot, ReviewSettingsSnapshot> change, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Root); LearningInputs.RejectLinks(Root);
        string lockPath = Path.Combine(Root, ".writer.lock");
        if (File.Exists(lockPath)) LearningInputs.RejectLinks(lockPath);
        using var writer = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = Read();
        if (current.Revision != expectedRevision)
            throw new InvalidOperationException("確認設定が別の操作で更新されました。再読み込みしてから保存してください。現在の設定は保持しています。");
        var next = change(current) with { Revision = checked(current.Revision + 1) };
        Validate(next); LearningJson.Write(DataPath, next, Validate, token); return next;
    }

    public ReviewSettingsSnapshot SaveNew(string name, ReviewConfiguration configuration, long expectedRevision, CancellationToken token = default)
        => SaveNew(name, [], configuration, expectedRevision, token);

    public ReviewSettingsSnapshot SaveNew(string name, ImmutableArray<string> classificationPath, ReviewConfiguration configuration,
        long expectedRevision, CancellationToken token = default)
    {
        var set = new ReviewSet("user." + Guid.NewGuid().ToString("N"), name, configuration)
        { ClassificationPath = classificationPath };
        return Save(set.Normalize(), expectedRevision, create: true, token);
    }

    public ReviewSettingsSnapshot SaveExisting(ReviewSet set, long expectedRevision, CancellationToken token = default)
        => Save(set.Normalize(), expectedRevision, create: false, token);

    private ReviewSettingsSnapshot Save(ReviewSet set, long revision, bool create, CancellationToken token)
    {
        if (set.IsBuiltIn) throw new InvalidOperationException("標準の見たいものは上書きできません。別名で保存してください。");
        return Update(revision, current =>
        {
            var old = current.Sets.FirstOrDefault(s => s.Id == set.Id);
            if (create != (old == null)) throw new InvalidOperationException("保存する見たいものが見つからないか、既に存在します。");
            if (current.Sets.Any(s => s.Id != set.Id && SamePath(s.ClassificationPath, set.ClassificationPath)
                && string.Equals(s.Name, set.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("同じ分類に同じ名前の見たいものがあります。別の名前にするか、対象を選んで上書きしてください。");
            return current with { Sets = old == null ? current.Sets.Add(set) : current.Sets.SetItem(current.Sets.IndexOf(old), set) };
        }, token);
    }

    public ReviewSettingsSnapshot SetPresentation(ReviewFilterPresentation presentation, long expectedRevision, CancellationToken token = default)
    {
        var value = presentation.Normalize();
        return Update(expectedRevision, current =>
        {
            var old = current.Presentations.FirstOrDefault(p => p.FilterId == value.FilterId);
            return current with { Presentations = old == null ? current.Presentations.Add(value)
                : current.Presentations.SetItem(current.Presentations.IndexOf(old), value) };
        }, token);
    }
}
