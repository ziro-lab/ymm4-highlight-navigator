using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Ymm4HighlightNavigator.Core;

string output = args.Length == 2 && args[0] == "--out" ? Path.GetFullPath(args[1]) : throw new ArgumentException("Use --out <fresh-directory>");
if (Directory.Exists(output)) throw new IOException("Use a fresh evidence directory.");
Directory.CreateDirectory(output);
var results = new List<(string Id, bool Passed, string? Error)>();
ReviewConfiguration A() => new([new("seed.visual", true), new("learned.fixture", false)], 1);
ReviewSet Saved(string id = "user.a", string name = "いつもの確認") => new(id, name, A().Normalize()) { ClassificationPath = ["ゲーム"] };
ReviewSetStore Store() => new(Path.Combine(output, "fixtures", Guid.NewGuid().ToString("N")));
void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed"); }
void Throws(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OperationCanceledException or JsonException) { return; } throw new Exception("Expected rejection"); }
void Test(string id, Action action)
{
    try { action(); results.Add((id, true, null)); Console.WriteLine("PASS " + id); }
    catch (Exception e) { results.Add((id, false, e.ToString())); Console.WriteLine("FAIL " + id + " " + e.Message); }
}
void WriteEnvelope(ReviewSetStore store, object value)
{
    Directory.CreateDirectory(store.Root);
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var envelope = new { schema = 1, sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), payload };
    File.WriteAllText(Path.Combine(store.Root, "review-settings.json"), JsonSerializer.Serialize(envelope));
}
Test("configuration-order-is-not-meaning", () => Assert(A().EquivalentTo(new(A().Filters.Reverse().ToImmutableArray(), 1))));
Test("configuration-duplicate-id-rejected", () => Throws(() => new ReviewConfiguration([new("same", true), new("same", false)], 1).Normalize()));
Test("configuration-invalid-sensitivity-rejected", () => { foreach (double value in new[] { double.NaN, double.PositiveInfinity, .24, 2.01 }) Throws(() => (A() with { Sensitivity = value }).Normalize()); });
Test("configuration-default-and-invalid-id-rejected", () => { Throws(() => new ReviewConfiguration(default, 1).Normalize()); Throws(() => A().WithFilter("../bad", true)); });
Test("configuration-all-off-is-valid", () => Assert(A().WithFilter("seed.visual", false).Filters.All(f => !f.Enabled)));
Test("missing-reference-retained", () => { var a = A().WithFilter("absent", true); Assert(a.MissingEnabledIds(["seed.visual"]).SequenceEqual(new[] { "absent" }) && a.Filters.Length == 3); });
Test("missing-reference-can-resolve-later", () => { var a = A().WithFilter("absent", true); Assert(a.MissingEnabledIds(["seed.visual", "absent"]).IsEmpty && a.IsEnabled("absent")); });
Test("working-edit-does-not-mutate-saved", () => { var saved = Saved(); var w = new ReviewWorkspace(saved); w.Change(A() with { Sensitivity = 2 }); Assert(saved.Configuration.Sensitivity == 1 && w.IsModified); });
Test("set-apply-and-one-step-restore", () => { var w = new ReviewWorkspace(Saved()); var edited = A().WithFilter("seed.visual", false); w.Change(edited); w.Apply(ReviewBuiltIns.Basic); Assert(!w.IsModified && w.CanRestore); Assert(w.Restore() && w.Current.EquivalentTo(edited) && w.AppliedSet!.Id == "user.a" && w.IsModified); Assert(!w.Restore()); });
Test("invalid-apply-does-not-destroy-working-state", () => { var w = new ReviewWorkspace(Saved()); Throws(() => w.Apply(Saved() with { Name = " " })); Assert(w.Current.EquivalentTo(A()) && !w.CanRestore); });
Test("late-save-does-not-overwrite-new-work", () => { var w = new ReviewWorkspace(Saved()); var captured = w.Current; w.Change(A() with { Sensitivity = 1.5 }); Assert(!w.AcceptSaved(Saved("user.b"), captured) && w.Current.Sensitivity == 1.5 && w.AppliedSet!.Id == "user.a"); });
Test("successful-save-acknowledges-without-reapply", () => { var w = new ReviewWorkspace(Saved()); var c = A() with { Sensitivity = 1.5 }; w.Change(c); Assert(w.AcceptSaved(Saved("user.b") with { Configuration = c }, c) && !w.IsModified); });
Test("settings-roundtrip-separate-from-learning", () => { var s = Store(); var result = s.SaveNew("テスト", A(), 0); var loaded = new ReviewSetStore(s.Root).Read(); Assert(loaded.Revision == 1 && loaded.Sets[0].Configuration.EquivalentTo(A()) && !File.Exists(Path.Combine(s.Root, "corpus.json"))); Assert(result.Sets[0].Id == loaded.Sets[0].Id); });
Test("view-intent-classification-roundtrip-variable-depth", () => { var s = Store(); var saved = s.SaveNew("X4", ["動画", "ゲーム"], A(), 0); var loaded = new ReviewSetStore(s.Root).Read().Sets.Single(); Assert(loaded.ClassificationPath.SequenceEqual(new[] { "動画", "ゲーム" }) && loaded.DisplayPath == "動画 > ゲーム > X4" && saved.Sets.Single().DisplayPath == loaded.DisplayPath); });
Test("view-intent-same-name-different-classification", () => { var s = Store(); var a = s.SaveNew("基本", ["ゲーム"], A(), 0); var b = s.SaveNew("基本", ["配信"], A(), a.Revision); Assert(b.Sets.Length == 2 && b.Sets.Select(x => x.DisplayPath).Order().SequenceEqual(new[] { "ゲーム > 基本", "配信 > 基本" }.Order())); Throws(() => s.SaveNew("基本", ["ゲーム"], A(), b.Revision)); });
Test("view-intent-classification-validation", () => { var s = Store(); Throws(() => s.SaveNew("X4", [""], A(), 0)); Throws(() => s.SaveNew("X4", Enumerable.Range(0, 9).Select(i => "L" + i).ToImmutableArray(), A(), 0)); });
Test("duplicate-set-uses-new-id", () => { var s = Store(); var a = s.SaveNew("A", ["ゲーム"], A(), 0); var b = s.SaveNew("B", a.Sets[0].ClassificationPath, a.Sets[0].Configuration, a.Revision); Assert(b.Sets.Length == 2 && b.Sets[0].Id != b.Sets[1].Id && b.Sets[0].Configuration.EquivalentTo(b.Sets[1].Configuration) && b.Sets.All(x => x.ClassificationPath.SequenceEqual(new[] { "ゲーム" }))); });
Test("explicit-overwrite-keeps-id", () => { var s = Store(); var a = s.SaveNew("A", A(), 0); var b = s.SaveExisting(a.Sets[0] with { Name = "B", Configuration = A() with { Sensitivity = 2 } }, 1); Assert(b.Sets.Length == 1 && b.Sets[0].Id == a.Sets[0].Id && b.Sets[0].Name == "B" && b.Sets[0].Configuration.Sensitivity == 2); });
Test("builtin-cannot-be-overwritten", () => { var s = Store(); Throws(() => s.SaveExisting(ReviewBuiltIns.Basic, 0)); Assert(s.Read().Revision == 0); });
Test("duplicate-name-rejected-with-old-data-intact", () => { var s = Store(); s.SaveNew("Work", A(), 0); Throws(() => s.SaveNew("work", A(), 1)); Assert(s.Read().Revision == 1 && s.Read().Sets.Length == 1); });
Test("optimistic-conflict-preserves-data", () => { var s = Store(); s.SaveNew("A", A(), 0); Throws(() => s.SaveNew("B", A(), 0)); Assert(s.Read().Revision == 1 && s.Read().Sets.Length == 1); });
Test("cancel-before-save-preserves-data", () => { var s = Store(); s.SaveNew("A", A(), 0); Throws(() => s.SaveNew("B", A(), 1, new CancellationToken(true))); Assert(s.Read().Revision == 1 && !Directory.EnumerateFiles(s.Root, "*.tmp-*").Any()); });
Test("writer-conflict-and-retry", () => { var s = Store(); s.SaveNew("A", A(), 0); using (var hold = new FileStream(Path.Combine(s.Root, ".writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { Throws(() => s.SaveNew("B", A(), 1)); Assert(s.Read().Revision == 1); } Assert(s.SaveNew("B", A(), 1).Revision == 2); });
Test("rename-and-group-move-preserve-references", () => { var s = Store(); var a = s.SaveNew("A", A(), 0); var b = s.SetPresentation(new("seed.visual", "新グループ", "新しい表示名"), a.Revision); Assert(b.Sets[0].Configuration.EquivalentTo(a.Sets[0].Configuration)); var c = s.SetPresentation(new("seed.visual", "別グループ", "別名"), b.Revision); Assert(c.Presentations.Length == 1 && c.Presentations[0].FilterId == "seed.visual" && c.Sets[0].Configuration.IsEnabled("seed.visual")); });
Test("corrupt-store-never-becomes-empty-success", () => { var s = Store(); s.SaveNew("A", A(), 0); string p = Path.Combine(s.Root, "review-settings.json"); File.WriteAllText(p, "corrupt"); Throws(() => s.Read()); Throws(() => s.SaveNew("B", A(), 1)); Assert(File.ReadAllText(p) == "corrupt"); });
Test("unsupported-schema-rejected", () => { var s = Store(); WriteEnvelope(s, new ReviewSettingsSnapshot(99, 0, [], [])); Throws(() => s.Read()); });
Test("missing-constructor-data-rejected", () => { var s = Store(); WriteEnvelope(s, new { schema = 1, revision = 0, sets = Array.Empty<ReviewSet>() }); Throws(() => s.Read()); });
Test("stored-builtin-rejected", () => { var s = Store(); WriteEnvelope(s, new ReviewSettingsSnapshot(1, 1, [ReviewBuiltIns.Basic], [])); Throws(() => s.Read()); });
Test("stored-duplicate-presentation-rejected", () => { var s = Store(); var p = new ReviewFilterPresentation("seed.visual", "G", "N"); WriteEnvelope(s, new ReviewSettingsSnapshot(1, 1, [], [p, p])); Throws(() => s.Read()); });
Test("stored-unknown-ids-roundtrip", () => { var s = Store(); var c = A().WithFilter("future.detector", true); s.SaveNew("Future", c, 0); Assert(s.Read().Sets[0].Configuration.EquivalentTo(c)); });
var required = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "required-cases.json")))!;
if (results.Count != required.Length || !results.Select(r => r.Id).Order().SequenceEqual(required.Order()) || required.Distinct().Count() != required.Length)
    throw new InvalidOperationException("Required case identities do not match.");
File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
{
    schema = "navigator.review-tests.v1", source = Environment.GetEnvironmentVariable("GITHUB_SHA"),
    failures = results.Count(r => !r.Passed), cases = results.Select(r => new { id = r.Id, passed = r.Passed, error = r.Error })
}, new JsonSerializerOptions { WriteIndented = true }));
return results.Any(r => !r.Passed) ? 1 : 0;
