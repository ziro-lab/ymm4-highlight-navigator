using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ymm4HighlightNavigator.Core;

public sealed record ReviewFilterState(string FilterId, bool Enabled);

/// <summary>Runtime choices only. Missing IDs are retained, never silently removed.</summary>
public sealed record ReviewConfiguration(ImmutableArray<ReviewFilterState> Filters, double Sensitivity)
{
    public ReviewConfiguration Normalize()
    {
        if (Filters.IsDefault || Filters.Length > 2000 || !double.IsFinite(Sensitivity) || Sensitivity is < .25 or > 2)
            throw new InvalidDataException("確認設定の形式または検出感度が不正です。");
        foreach (var item in Filters)
            if (item is null || !ReviewNames.ValidId(item.FilterId)) throw new InvalidDataException("フィルターの参照が不正です。");
        if (Filters.Select(f => f.FilterId).Distinct(StringComparer.Ordinal).Count() != Filters.Length)
            throw new InvalidDataException("確認設定に同じフィルターが重複しています。");
        return this with { Filters = Filters.OrderBy(f => f.FilterId, StringComparer.Ordinal).ToImmutableArray() };
    }
    public bool EquivalentTo(ReviewConfiguration other)
    {
        var a = Normalize(); var b = other.Normalize();
        return a.Sensitivity == b.Sensitivity && a.Filters.SequenceEqual(b.Filters);
    }
    public bool IsEnabled(string id) => Filters.FirstOrDefault(f => f.FilterId == id)?.Enabled ?? false;
    public ImmutableArray<string> MissingEnabledIds(IEnumerable<string> available)
    {
        var known = available.ToHashSet(StringComparer.Ordinal);
        return Filters.Where(f => f.Enabled && !known.Contains(f.FilterId)).Select(f => f.FilterId).ToImmutableArray();
    }
    public ReviewConfiguration WithFilter(string id, bool enabled)
    {
        var next = Filters.Where(f => f.FilterId != id).Append(new ReviewFilterState(id, enabled)).ToImmutableArray();
        return new ReviewConfiguration(next, Sensitivity).Normalize();
    }
}

public sealed record ReviewSet(string Id, string Name, ReviewConfiguration Configuration)
{
    public ImmutableArray<string> ClassificationPath { get; init; } = [];
    [JsonIgnore] public bool IsBuiltIn => Id.StartsWith("builtin.", StringComparison.Ordinal);
    [JsonIgnore] public string DisplayPath => ClassificationPath.IsDefaultOrEmpty
        ? Name : string.Join(" > ", ClassificationPath.Append(Name));

    public ReviewSet Normalize()
    {
        if (!ReviewNames.ValidId(Id) || !(Id.StartsWith("user.", StringComparison.Ordinal) || IsBuiltIn))
            throw new InvalidDataException("見たいものの識別情報が不正です。");
        return this with
        {
            Name = ReviewNames.Clean(Name),
            ClassificationPath = ReviewNames.CleanPath(ClassificationPath),
            Configuration = Configuration.Normalize()
        };
    }
}

/// <summary>Presentation aliases do not rename Corpus labels or change learned filter identity.</summary>
public sealed record ReviewFilterPresentation(string FilterId, string Group, string Name)
{
    public ReviewFilterPresentation Normalize()
    {
        if (!ReviewNames.ValidId(FilterId)) throw new InvalidDataException("フィルターの識別情報が不正です。");
        return this with { Group = ReviewNames.Clean(Group), Name = ReviewNames.Clean(Name) };
    }
}

public static class ReviewBuiltIns
{
    public static ReviewSet Basic { get; } = new("builtin.basic", "基本",
        new ReviewConfiguration([
            new(GenericFilterCatalog.LargeSceneChange.Id, true),
            new(GenericFilterCatalog.DarkFade.Id, true),
            new(GenericFilterCatalog.QuietToActivity.Id, true)
        ], 1).Normalize())
    { ClassificationPath = ["汎用"] };
}

/// <summary>Saved settings and working choices are independent; applying a set has one explicit restore.</summary>
public sealed class ReviewWorkspace
{
    private (ReviewSet? Set, ReviewConfiguration Configuration)? previous;
    public ReviewSet? AppliedSet { get; private set; }
    public ReviewConfiguration Current { get; private set; }
    public bool CanRestore => previous.HasValue;
    public bool IsModified => AppliedSet == null || !Current.EquivalentTo(AppliedSet.Configuration);
    public ReviewWorkspace(ReviewSet initial)
    {
        AppliedSet = initial.Normalize(); Current = AppliedSet.Configuration;
    }
    public void Change(ReviewConfiguration configuration) => Current = configuration.Normalize();
    public void Apply(ReviewSet set)
    {
        var valid = set.Normalize();
        previous = (AppliedSet, Current); AppliedSet = valid; Current = valid.Configuration;
    }
    public bool Restore()
    {
        if (previous is not { } saved) return false;
        AppliedSet = saved.Set; Current = saved.Configuration; previous = null; return true;
    }
    public bool AcceptSaved(ReviewSet saved, ReviewConfiguration captured)
    {
        var valid = saved.Normalize();
        // A late save completion must not replace more recent working choices.
        if (!Current.EquivalentTo(captured) || !valid.Configuration.EquivalentTo(captured)) return false;
        AppliedSet = valid; return true;
    }
    public bool RefreshAppliedMetadata(ReviewSet saved)
    {
        var valid = saved.Normalize();
        if (AppliedSet?.Id != valid.Id) return false;
        if (!AppliedSet.Configuration.EquivalentTo(valid.Configuration))
            throw new InvalidOperationException("保存済みの検出内容が変わっているため、表示情報だけを更新できません。");
        AppliedSet = valid;
        return true;
    }
    public bool ForgetSaved(string id)
    {
        bool changed = false;
        if (AppliedSet?.Id == id) { AppliedSet = null; changed = true; }
        if (previous?.Set?.Id == id) previous = null;
        return changed;
    }
}

internal static class ReviewNames
{
    internal static bool ValidId(string? id) => id is { Length: > 0 and <= 160 }
        && id.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');

    internal static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100 || text.Any(char.IsControl))
            throw new ArgumentException("名前と分類は100文字以内で入力してください。");
        return text.Trim().Normalize(System.Text.NormalizationForm.FormC);
    }

    internal static ImmutableArray<string> CleanPath(ImmutableArray<string> path)
    {
        if (path.IsDefaultOrEmpty) return [];
        if (path.Length > 8) throw new ArgumentException("分類階層は8段以内にしてください。");
        return path.Select(Clean).ToImmutableArray();
    }
}
