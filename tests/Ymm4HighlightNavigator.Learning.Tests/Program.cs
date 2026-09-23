using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ymm4HighlightNavigator.Core;

return await LearningSpecs.Run(args);

internal static class LearningSpecs
{
    private static readonly List<object> Results = [];
    private static int failed, checks;
    private static string root = "", work = "";
    private static LearningLabel Battle => new("X4", "戦闘開始");
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or InvalidOperationException or OperationCanceledException or JsonException or NotSupportedException) { rejected = true; }
        Check(rejected, "Expected rejection.");
    }
    private static FeaturePack Make(string name, double at = 4, double duration = 10, byte from = 0, byte to = 255, bool audio = false, int fps = 2, double start = 0)
    {
        var extractor = new VisualExtractor(); var rows = ImmutableArray.CreateBuilder<VideoFeature>();
        for (int i = 0; i < (int)(duration * fps); i++)
        {
            byte level = i / (double)fps >= at ? to : from;
            rows.Add(extractor.Extract(Enumerable.Repeat(level, 64 * 36 * 3).ToArray(), start + i / (double)fps));
        }
        var sounds = audio ? Enumerable.Range(1, (int)(duration * 20)).Select(i => new AudioFeature(start + i / 20d, i / 20d >= at ? .5f : 0, i / 20d >= at ? .7f : 0, 800)).ToImmutableArray() : ImmutableArray<AudioFeature>.Empty;
        var pack = new FeaturePack(new(1, FeatureFormat.Extractor, fps, start, start + duration, audio, Hash(name), "synthetic-test"), rows.ToImmutable(), sounds);
        pack.Validate(); return pack;
    }
    private static CorpusStore Store() => new(Path.Combine(work, "corpus"));
    private static LearningSet Seed(CorpusStore store)
    {
        store.RegisterPack(Make("a"), Battle, "a.mkv");
        store.RegisterPack(Make("b", 7, 15, to: 230), Battle, "b.mkv");
        return FilterAuthor.Load(store, Battle);
    }
    private static async Task Test(string id, Func<Task> test)
    {
        work = Path.Combine(root, id); Directory.CreateDirectory(work); int before = checks;
        try { await test(); Results.Add(new { id, passed = true, assertions = checks - before }); Console.WriteLine("PASS " + id); }
        catch (Exception e) { failed++; Results.Add(new { id, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + id + ": " + e); }
    }
    private static Task Pure(string id, Action test) => Test(id, () => { test(); return Task.CompletedTask; });
    public static async Task<int> Run(string[] args)
    {
        string? Arg(string flag) { int index = Array.IndexOf(args, flag); return index < 0 ? null : args[index + 1]; }
        root = Path.GetFullPath(Arg("--out") ?? "out/learning-tests");
        if (Directory.Exists(root)) throw new IOException("Use a fresh evidence directory.");
        Directory.CreateDirectory(root);
        await Pure("label-validation", () =>
        {
            Check(new LearningLabel(" X4 ", "戦闘開始 ").Normalize() == Battle, "Normalize labels");
            Check(new LearningLabel("X4", "ガ").Key == new LearningLabel("X4", "カ\u3099").Key, "Unicode identity");
            Reject(() => new LearningLabel("", "a").Normalize()); Reject(() => new LearningLabel("X4", new string('x', 101)).Normalize());
        });
        await Pure("folder-preview-isolated", () =>
        {
            Directory.CreateDirectory(Path.Combine(work, "sub")); File.WriteAllText(Path.Combine(work, "a.MKV"), "test");
            File.WriteAllText(Path.Combine(work, "b.txt"), "test"); File.WriteAllText(Path.Combine(work, "sub/c.mp4"), "test");
            var files = LearningInputs.Folder(work, Battle); Check(files.Length == 1 && files[0].Label == Battle, "Only selected folder videos");
        });
        await Pure("commit-before-registration", () =>
        {
            var store = Store(); var result = store.RegisterPack(Make("a"), Battle, "a.mkv");
            var sample = store.Read().Samples.Single(); Check(result.Disposition == ImportDisposition.Added, "Added");
            Check(store.LoadPack(sample).Header.SourceHash == sample.SourceHash, "Committed Pack is readable");
        });
        await Pure("dedupe-does-not-double-weight", () =>
        {
            var store = Store(); var pack = Make("a"); store.RegisterPack(pack, Battle, "a.mkv"); long revision = store.Read().Revision;
            var again = store.RegisterPack(pack, Battle, "copy.mkv");
            Check(again.Disposition == ImportDisposition.AlreadyPresent && store.Read().Samples.Length == 1 && store.Read().Revision == revision, "Idempotent same label");
            Check(Directory.GetFiles(Path.Combine(store.Root, "packs")).Length == 1, "Single Pack");
        });
        await Pure("multiple-positive-memberships", () =>
        {
            var store = Store(); var pack = Make("a"); store.RegisterPack(pack, Battle, "a.mkv");
            var station = new LearningLabel("X4", "ステーション移行");
            var result = store.RegisterPack(pack, station, "copy.mkv");
            Check(result.Disposition == ImportDisposition.MembershipAdded, "Membership added");
            Check(store.Read().Samples.Single().Labels.Length == 2 && FilterAuthor.Load(store, station).Examples.Length == 1, "One sample can be two positives");
        });
        await Pure("other-folder-is-not-negative", () =>
        {
            var store = Store(); Seed(store); store.RegisterPack(Make("station", from: 255, to: 0), new("X4", "ステーション"), "station.mkv");
            Check(FilterAuthor.Load(store, Battle).Examples.Length == 2, "Unrelated memberships excluded, never negative");
        });
        await Pure("cancel-before-publication", () =>
        {
            var store = Store(); Reject(() => store.RegisterPack(Make("a"), Battle, "a.mkv", new(true)));
            Check(!Directory.Exists(store.Root), "No cancelled registration or Pack");
        });
        await Pure("invalid-pack-preserves-catalog", () =>
        {
            var store = Store(); var good = Make("a"); store.RegisterPack(good, Battle, "a.mkv");
            var before = File.ReadAllBytes(Path.Combine(store.Root, "corpus.json"));
            Reject(() => store.RegisterPack(good with { Header = good.Header with { SchemaVersion = 99 } }, Battle, "bad.mkv"));
            Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(store.Root, "corpus.json"))), "Failed import changed no catalog");
        });
        await Pure("pack-corruption-is-not-empty-corpus", () =>
        {
            var store = Store(); Seed(store); var sample = store.Read().Samples[0];
            File.WriteAllText(Path.Combine(store.Root, "packs", sample.Id + ".navfp"), "corrupt");
            Reject(() => FilterAuthor.Load(store, Battle)); Check(store.Read().Samples.Length == 2, "Corruption not rewritten as no samples");
        });
        await Pure("catalog-corruption-is-not-new-corpus", () =>
        {
            var store = Store(); Seed(store); var path = Path.Combine(store.Root, "corpus.json"); File.WriteAllText(path, "{");
            Reject(() => store.Read()); Reject(() => store.RegisterPack(Make("c"), Battle, "c.mkv")); Check(File.ReadAllText(path) == "{", "Corrupt catalog preserved");
        });
        await Pure("writer-contention-safe", () =>
        {
            var store = Store(); Seed(store); long revision = store.Read().Revision;
            using (var locked = new FileStream(Path.Combine(store.Root, ".writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Reject(() => store.RegisterPack(Make("c"), Battle, "c.mkv"));
            Check(store.Read().Revision == revision, "Concurrent writer rejected without overwriting");
        });
        await Pure("raw-free-transition-replay", () =>
        {
            var store = Store(); var original = Make("a"); store.RegisterPack(original, Battle, "deleted-video.mkv");
            var fresh = new CorpusStore(store.Root); var restored = fresh.LoadPack(fresh.Read().Samples.Single());
            var a = TransitionIndex.Build(original); var b = TransitionIndex.Build(restored);
            Check(a.Candidates.Select(x => x.CenterSeconds).SequenceEqual(b.Candidates.Select(x => x.CenterSeconds)), "Raw-free centers");
            Check(a.Candidates.Zip(b.Candidates).All(p => p.First.Signature.Visual.SequenceEqual(p.Second.Signature.Visual)), "Raw-free primitive pattern");
        });
        await Pure("step-transition-near-expected-time", () =>
        {
            var index = TransitionIndex.Build(Make("a")); var strongest = index.Candidates.MaxBy(c => c.Strength)!;
            Check(Math.Abs(strongest.CenterSeconds - 4) <= .5, "Real transition not clip middle");
            Check(strongest.Before.End == strongest.Transition.Start && strongest.Transition.End == strongest.After.Start && strongest.FullContext, "Before/transition/after partition");
        });
        await Pure("constant-clip-no-invented-transition", () =>
        {
            Check(TransitionIndex.Build(Make("flat", from: 128, to: 128)).Candidates.IsEmpty, "No meaningful change");
        });
        await Pure("multiple-transitions-preserved", () =>
        {
            var p = Make("multi"); var ex = new VisualExtractor();
            var rows = Enumerable.Range(0, 24).Select(i => ex.Extract(Enumerable.Repeat((byte)(i >= 6 && i < 16 ? 255 : 0), 6912).ToArray(), i / 2d)).ToImmutableArray();
            p = p with { Header = p.Header with { EndSeconds = 12 }, Video = rows };
            var index = TransitionIndex.Build(p);
            Check(index.Candidates.Any(c => c.CenterSeconds == 3) && index.Candidates.Any(c => c.CenterSeconds == 8), "Both transitions, not one clip center");
        });
        await Pure("missing-audio-remains-missing", () =>
        {
            var absent = TransitionIndex.Build(Make("no-audio")).Candidates; var audio = TransitionIndex.Build(Make("audio", audio: true)).Candidates;
            Check(absent.All(c => c.Signature.PeakAudio == null) && audio.Any(c => c.Signature.PeakAudio > .5), "No missing-to-zero conversion");
            Check(absent.Select(x => x.CenterSeconds).SequenceEqual(audio.Select(x => x.CenterSeconds)), "Audio not required for visual transitions");
        });
        await Pure("source-clock-nonzero-offset", () =>
        {
            var index = TransitionIndex.Build(Make("offset", at: 3, start: 20));
            Check(index.Candidates.MaxBy(c => c.Strength)!.CenterSeconds == 23, "Source time, not normalized clip-relative time");
        });
        await Pure("local-pattern-independent-of-long-margins", () =>
        {
            var a = TransitionIndex.Build(Make("short", 4, 10)).Candidates.Single(c => c.CenterSeconds == 4);
            var b = TransitionIndex.Build(Make("long", 22, 40)).Candidates.Single(c => c.CenterSeconds == 22);
            Check(TransitionMatcher.Distance(a.Signature, b.Signature) < 1e-6, "No whole-video percentile leakage into learned pattern");
        });
        await Pure("gap-reset-not-a-transition", () =>
        {
            Check(TransitionIndex.Build(Make("a", from: 0, to: 0)).Candidates.IsEmpty && TransitionIndex.Build(Make("b", from: 255, to: 255, start: 30)).Candidates.IsEmpty, "Disjoint ranges do not fabricate a delta");
        });
        await Pure("common-pattern-from-two-clips", () =>
        {
            var store = Store(); var set = Seed(store); var draft = FilterAuthor.Create(set);
            Check(draft.Coverage.Covered == 2 && draft.Filter.Patterns.Length == 1, "One shared transition fits two clips");
            Check(draft.Filter.Patterns[0].SupportSampleIds.Length == 2, "Support is clips, not count of peaks");
        });
        await Pure("multiple-pattern-or-without-average-collapse", () =>
        {
            var store = Store(); store.RegisterPack(Make("up"), Battle, "up.mkv"); store.RegisterPack(Make("down", from: 255, to: 0), Battle, "down.mkv");
            var draft = FilterAuthor.Create(FilterAuthor.Load(store, Battle));
            Check(draft.Filter.Patterns.Length >= 2 && draft.Coverage.Covered == 2, "Different entry directions kept separately");
        });
        await Pure("covered-hard-and-no-transition-distinguished", () =>
        {
            var store = Store(); var initial = FilterAuthor.Create(Seed(store));
            store.RegisterPack(Make("inverse", from: 255, to: 0), Battle, "inverse.mkv"); store.RegisterPack(Make("flat", from: 128, to: 128), Battle, "flat.mkv");
            var replay = TransitionMatcher.Replay(FilterAuthor.Load(store, Battle), initial.Filter);
            Check(replay.Covered == 2 && replay.Hard == 1 && replay.NoTransition == 1, "Miss vs uninformative material");
        });
        await Pure("sensitivity-matches-are-nested", () =>
        {
            var store = Store(); var filter = FilterAuthor.Create(Seed(store)).Filter;
            var index = TransitionIndex.Build(Make("trial", to: 180));
            var low = TransitionMatcher.Match(index, filter, .5).Select(x => x.CenterSeconds).ToHashSet();
            var medium = TransitionMatcher.Match(index, filter, 1).Select(x => x.CenterSeconds).ToHashSet();
            var high = TransitionMatcher.Match(index, filter, 2).Select(x => x.CenterSeconds).ToHashSet();
            Check(low.IsSubsetOf(medium) && medium.IsSubsetOf(high), "Nested raw match centers, not merged episode counts");
            Reject(() => TransitionMatcher.Match(index, filter, double.NaN));
        });
        await Pure("filter-save-and-reload", () =>
        {
            var store = Store(); var draft = FilterAuthor.Create(Seed(store)); var filters = new FilterStore(store); var saved = filters.Apply(draft);
            var loaded = new FilterStore(new(store.Root)).Read(Battle)!;
            Check(saved.Revision == 1 && loaded.Revision == 1 && loaded.Id == saved.Id, "Persistent revision");
            Check(TransitionMatcher.Replay(FilterAuthor.Load(store, Battle), loaded).Covered == 2, "Persisted filter actually matches");
        });
        await Pure("stale-corpus-draft-rejected", () =>
        {
            var store = Store(); var draft = FilterAuthor.Create(Seed(store)); store.RegisterPack(Make("extra"), Battle, "extra.mkv");
            var filters = new FilterStore(store); Reject(() => filters.Apply(draft)); Check(filters.ReadAll().IsEmpty, "No stale draft published");
        });
        await Pure("concurrent-filter-revision-rejected", () =>
        {
            var store = Store(); var draft = FilterAuthor.Create(Seed(store)); var filters = new FilterStore(store); filters.Apply(draft);
            Reject(() => filters.Apply(draft)); Check(filters.Read(Battle)!.Revision == 1, "Compare-and-set head");
        });
        await Pure("positive-regression-rejected", () =>
        {
            var store = Store(); var filters = new FilterStore(store); filters.Apply(FilterAuthor.Create(Seed(store)));
            store.RegisterPack(Make("inverse", from: 255, to: 0), Battle, "inverse.mkv");
            var set = FilterAuthor.Load(store, Battle); var inverse = set.Examples.Single(e => e.Sample.OriginalNames[0] == "inverse.mkv");
            var wrong = FilterAuthor.Create(set with { Examples = [inverse] });
            Reject(() => filters.Apply(wrong with { BaseRevision = 1 })); Check(filters.Read(Battle)!.Revision == 1, "Existing positives protected");
        });
        await Pure("explicit-rollback-keeps-history", () =>
        {
            var store = Store(); var set = Seed(store); var filters = new FilterStore(store); var one = filters.Apply(FilterAuthor.Create(set));
            store.RegisterPack(Make("inverse", from: 255, to: 0), Battle, "inverse.mkv"); var two = filters.Apply(FilterAuthor.Create(FilterAuthor.Load(store, Battle), one));
            Check(two.Revision == 2 && two.ParentRevision == 1, "New immutable revision");
            var restored = filters.Rollback(Battle, 2); Check(restored.Revision == 1, "Restore prior active revision");
            Check(Directory.GetFiles(Path.Combine(store.Root, "filters", Battle.Key), "*.json").Length == 2, "History not deleted");
        });
        await Pure("filter-remove-deactivates-but-keeps-history", () =>
        {
            var store = Store(); var filters = new FilterStore(store);
            var one = filters.Apply(FilterAuthor.Create(Seed(store)));
            var removed = filters.Remove(Battle, one.Revision);
            Check(removed.Revision == 1 && filters.Read(Battle) == null && filters.ReadAll().IsEmpty, "Active head removed");
            Check(Directory.GetFiles(Path.Combine(store.Root, "filters", Battle.Key), "*.json").Length == 1, "Immutable revision retained");
            Reject(() => filters.Remove(Battle, one.Revision));
            var next = filters.Apply(FilterAuthor.Create(FilterAuthor.Load(store, Battle)));
            Check(next.Revision == 2 && next.ParentRevision == 0 && filters.Read(Battle)!.Revision == 2, "Recreate never overwrites retained history");
        });
        await Pure("cancel-filter-publication", () =>
        {
            var store = Store(); var draft = FilterAuthor.Create(Seed(store)); var filters = new FilterStore(store);
            Reject(() => filters.Apply(draft, new(true))); Check(filters.ReadAll().IsEmpty, "No cancelled active filter");
        });
        await Pure("import-does-not-silently-train", () =>
        {
            var store = Store(); var filters = new FilterStore(store); filters.Apply(FilterAuthor.Create(Seed(store)));
            store.RegisterPack(Make("inverse", from: 255, to: 0), Battle, "inverse.mkv");
            Check(filters.Read(Battle)!.Revision == 1 && filters.Read(Battle)!.Patterns.Length == 1, "Import changes data, not active knowledge");
        });
        await Pure("author-cancellation", () =>
        {
            var store = Store(); var set = Seed(store); Reject(() => FilterAuthor.Create(set, token: new(true)));
        });
        await Pure("no-feature-filter-rejected", () =>
        {
            var store = Store(); store.RegisterPack(Make("flat", from: 0, to: 0), Battle, "flat.mkv"); Reject(() => FilterAuthor.Create(FilterAuthor.Load(store, Battle)));
        });
        string ffmpeg = Arg("--ffmpeg") ?? throw new ArgumentException("--ffmpeg is required");
        string ffprobe = Arg("--ffprobe") ?? throw new ArgumentException("--ffprobe is required");
        string fixture = Arg("--fixture") ?? throw new ArgumentException("--fixture is required");
        var backend = new FfmpegBackend(ffmpeg, ffprobe);
        await Test("real-media-batch-dedupe-raw-free", async () =>
        {
            string input = Path.Combine(work, "input"); Directory.CreateDirectory(input);
            File.Copy(fixture, Path.Combine(input, "a.mkv")); File.Copy(fixture, Path.Combine(input, "b.mkv"));
            var store = Store(); var result = await store.ImportAsync(LearningInputs.Folder(input, Battle), backend);
            Check(result.Committed == 2 && result.Failed == 0 && store.Read().Samples.Length == 1, "Real duplicate decoded/registered once");
            Directory.Delete(input, true);
            var set = FilterAuthor.Load(new(store.Root), Battle); var draft = FilterAuthor.Create(set);
            var saved = new FilterStore(store).Apply(draft); Check(TransitionMatcher.Replay(set, saved).Covered == 1, "Real raw-free learning replay");
        });
        await Test("real-media-partial-failure-preserves-input", async () =>
        {
            string valid = Path.Combine(work, "valid.mkv"), invalid = Path.Combine(work, "bad.mp4"); File.Copy(fixture, valid); File.WriteAllText(invalid, "not a video");
            var store = Store(); var result = await store.ImportAsync([new(valid, Battle), new(invalid, Battle)], backend);
            Check(result.Committed == 1 && result.Failed == 1 && File.Exists(valid) && File.ReadAllText(invalid) == "not a video", "Partial outcomes, unchanged source files");
        });
        await Test("real-batch-cancellation-not-success", async () =>
        {
            var store = Store(); var result = await store.ImportAsync([new(fixture, Battle), new(fixture, Battle)], backend, token: new(true));
            Check(result.Cancelled == 2 && result.Committed == 0 && !Directory.Exists(store.Root), "All cancelled, no false success");
        });
        File.WriteAllText(Path.Combine(root, "results.json"), JsonSerializer.Serialize(new { schema = "navigator.learning-tests.v1", checkout = Environment.GetEnvironmentVariable("GITHUB_SHA"), failures = failed, assertions = checks, cases = Results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Learning cases {Results.Count}; assertions {checks}; failures {failed}");
        return failed == 0 ? 0 : 1;
    }
}
