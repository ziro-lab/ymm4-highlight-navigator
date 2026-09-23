using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Ymm4HighlightNavigator.Core;

return await Specs.Run(args);

internal static class Specs
{
    private static readonly List<object> Results = [];
    private static int assertions, failures;
    private static string root = "";
    private static FeaturePack Example(bool audio = false)
    {
        var extractor = new VisualExtractor();
        var frames = ImmutableArray.CreateBuilder<VideoFeature>();
        for (int i = 0; i < 8; i++) frames.Add(extractor.Extract(Enumerable.Repeat((byte)(i is 3 or 4 ? 255 : 0), 64 * 36 * 3).ToArray(), i / 2d));
        ImmutableArray<AudioFeature> sounds = audio ? Enumerable.Range(1, 80).Select(i => new AudioFeature(i * .05, .25f, .5f, 800)).ToImmutableArray() : [];
        return new(new(1, FeatureFormat.Extractor, 2, 0, 4, audio, new string('0', 64), "generated"), frames.ToImmutable(), sounds);
    }
    private static FeaturePack VisualLevels(params byte[] levels)
    {
        if (levels.Length < 4) throw new ArgumentException("Need enough frames for transition context.");
        var extractor = new VisualExtractor();
        var frames = ImmutableArray.CreateBuilder<VideoFeature>();
        for (int i = 0; i < levels.Length; i++)
            frames.Add(extractor.Extract(Enumerable.Repeat(levels[i], 64 * 36 * 3).ToArray(), i / 2d));
        return new(new(1, FeatureFormat.Extractor, 2, 0, levels.Length / 2d, false, new string('1', 64), "generic-test"), frames.ToImmutable(), []);
    }
    private static HashSet<double> Anchors(ProfileEvaluation evaluation)
        => evaluation.Hits.SelectMany(h => h.AnchorSourceTimes).Select(t => Math.Round(t, 6)).ToHashSet();
    private static void Check(bool value, string name)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(name);
    }
    private static void Near(double actual, double expected, double epsilon = 1e-5) => Check(Math.Abs(actual - expected) <= epsilon, $"Expected {expected:R}, actual {actual:R}");
    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or OperationCanceledException or JsonException) { rejected = true; }
        Check(rejected, "Invalid operation was accepted.");
    }
    private static async Task RejectAsync(Func<Task> action)
    {
        bool rejected = false;
        try { await action(); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or OperationCanceledException or JsonException) { rejected = true; }
        Check(rejected, "Invalid async operation was accepted.");
    }
    private static async Task Test(string id, Func<Task> test)
    {
        int before = assertions;
        try { await test(); Results.Add(new { id, passed = true, assertions = assertions - before }); Console.WriteLine("PASS " + id); }
        catch (Exception ex) { failures++; Results.Add(new { id, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + id + ": " + ex); }
    }
    private static Task Pure(string id, Action test) => Test(id, () => { test(); return Task.CompletedTask; });
    private static string FileName(string name) => Path.Combine(root, name);

    public static async Task<int> Run(string[] args)
    {
        string? Get(string name) { int i = Array.IndexOf(args, name); return i < 0 ? null : args[i + 1]; }
        root = Path.GetFullPath(Get("--out") ?? "out/core-tests");
        Directory.CreateDirectory(root);
        await Pure("visual-black-white", () =>
        {
            var x = new VisualExtractor();
            var black = x.Extract(new byte[6912], 0);
            var white = x.Extract(Enumerable.Repeat((byte)255, 6912).ToArray(), .5);
            Near(black.Luma, 0); Near(black.Contrast, 0); Near(black.Delta, 0); Near(black.Histogram[0], 1);
            Near(white.Luma, 1); Near(white.Delta, 1); Near(white.Edges, 0); Near(white.Extremes, 1);
            Check(white.Descriptor.All(v => v == 255), "White descriptor");
            Check(white.GridDelta.All(v => Math.Abs(v - 1) < 1e-5), "Grid delta");
        });
        await Pure("visual-color-and-spatial-edge", () =>
        {
            byte[] rgb = new byte[6912];
            for (int y = 0; y < 36; y++) for (int x = 32; x < 64; x++) for (int c = 0; c < 3; c++) rgb[(y * 64 + x) * 3 + c] = 255;
            var f = new VisualExtractor().Extract(rgb, 0);
            Near(f.Luma, .5); Near(f.Contrast, .5); Near(f.Edges, 36d / (64 * 35 + 36 * 63));
            rgb = new byte[6912]; for (int i = 0; i < rgb.Length; i += 3) rgb[i] = 255;
            f = new VisualExtractor().Extract(rgb, 0); Near(f.Luma, .299); Near(f.Chroma, 1);
        });
        await Pure("visual-invalid-input-and-gap-reset", () =>
        {
            Reject(() => new VisualExtractor().Extract(new byte[3], 0));
            Reject(() => new VisualExtractor().Extract(new byte[6912], double.NaN));
            Near(new VisualExtractor().Extract(Enumerable.Repeat((byte)255, 6912).ToArray(), 30).Delta, 0);
        });
        await Pure("audio-rms-peak-window", () =>
        {
            var a = new AudioExtractor(); AudioFeature? f = null;
            for (int i = 0; i < 800; i++) f = a.Push(.5f, 2) ?? f;
            Check(f != null, "Audio row missing"); Near(f!.Rms, .5); Near(f.Peak, .5); Near(f.TimeSeconds, 2.1); Check(f.WindowSamples == 800, "100ms window");
            Check(a.Finish(2) == null, "No duplicate final audio row");
            Reject(() => a.Push(float.NaN, 2));
        });
        await Pure("audio-short-tail", () =>
        {
            var a = new AudioExtractor(); for (int i = 0; i < 123; i++) a.Push(-.25f, 0);
            var f = a.Finish(0)!; Near(f.Rms, .25); Near(f.TimeSeconds, 123d / 8000); Check(f.WindowSamples == 123, "Tail window");
        });
        await Pure("salience-constant-and-outlier", () =>
        {
            Check(Salience.Rank(new float[] { .5f, .5f, .5f }).All(v => v == 0), "Constant signal is not a rare event");
            var r = Salience.Rank(new float[] { 0, 0, 0, 1 }); Near(r[3], 1); Near(r[0], 0);
            Reject(() => Salience.Rank(new float[] { float.PositiveInfinity }));
        });
        await Pure("pack-roundtrip-and-raw-free-replay", () =>
        {
            var p = Example(true); string path = FileName("roundtrip.navfp"); PackStore.Save(path, p);
            var q = PackStore.Load(path);
            Check(q.Header == p.Header, "Header roundtrip");
            Check(q.Video.Length == 8 && q.Audio.Length == 80, "Counts");
            for (int i = 0; i < 8; i++)
            {
                Near(q.Video[i].TimeSeconds, p.Video[i].TimeSeconds); Near(q.Video[i].Luma, p.Video[i].Luma);
                Check(q.Video[i].Descriptor.SequenceEqual(p.Video[i].Descriptor), "Descriptor roundtrip");
                Check(q.Video[i].GridDelta.SequenceEqual(p.Video[i].GridDelta), "Grid roundtrip");
            }
            var profile = new SceneProfile("bright", "bright", [new(FeatureAxis.Luma, 0, .9f)], MergeGapSeconds: 0, PreRollSeconds: 0, PostRollSeconds: 0);
            var result = ProfileEvaluator.Evaluate(new(q), profile); Check(result.Hits.Length == 1, "Raw-free Profile replay"); Near(result.Hits[0].Range.Start, 1.5); Near(result.Hits[0].Range.End, 2.5);
        });
        await Pure("pack-overwrite-rejected", () =>
        {
            string path = FileName("roundtrip.navfp"); string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Reject(() => PackStore.Save(path, Example()));
            Check(before == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), "Existing committed Pack unchanged");
        });
        await Pure("pack-cancel-no-publication", () =>
        {
            string path = FileName("cancel.navfp"); Reject(() => PackStore.Save(path, Example(), new CancellationToken(true)));
            Check(!File.Exists(path), "Cancelled Pack not committed"); Check(!Directory.GetFiles(root, "*.tmp-*").Any(), "No staging remnants");
        });
        await Pure("pack-corruption-rejected", () =>
        {
            byte[] bytes = File.ReadAllBytes(FileName("roundtrip.navfp"));
            foreach (int offset in new[] { 0, 16, 31 })
            {
                var bad = (byte[])bytes.Clone(); bad[offset] ^= 255; string path = FileName($"corrupt-{offset}.navfp"); File.WriteAllBytes(path, bad); Reject(() => PackStore.Load(path));
            }
            File.WriteAllBytes(FileName("truncated.navfp"), bytes[..40]); Reject(() => PackStore.Load(FileName("truncated.navfp")));
            var huge = (byte[])bytes.Clone(); System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(huge.AsSpan(8, 8), long.MaxValue);
            File.WriteAllBytes(FileName("huge.navfp"), huge); Reject(() => PackStore.Load(FileName("huge.navfp")));
        });
        await Pure("pack-schema-and-missingness", () =>
        {
            var p = Example(); Reject(() => (p with { Header = p.Header with { SchemaVersion = 99 } }).Validate());
            Reject(() => (p with { Header = p.Header with { HasAudio = true } }).Validate());
            Reject(() => (p with { Video = p.Video.SetItem(0, p.Video[0] with { Descriptor = [] }) }).Validate());
            Reject(() => (p with { Video = p.Video.RemoveAt(7) }).Validate());
        });
        await Pure("range-union-and-boundaries", () =>
        {
            var r = TimeRange.Union([new(10, 20), new(0, 5), new(4, 8), new(8, 9)]);
            Check(r.SequenceEqual(new[] { new TimeRange(0, 9), new TimeRange(10, 20) }), "Sorted half-open union");
            Check(new TimeRange(0, 1).Intersect(new(1, 2)) == null, "Touching intervals do not overlap");
            Check(TimeRange.Union(r, 1).Length == 1, "Exact merge gap");
            Reject(() => new TimeRange(double.NaN, 2)); Reject(() => new TimeRange(1, 1)); Reject(() => TimeRange.Union([default(TimeRange)]));
        });
        await Pure("target-constant-rate-offset", () =>
        {
            foreach (double rate in new[] { 50d, 100d, 200d })
            {
                var t = new TargetSnapshot(Guid.NewGuid(), "source", 137, 300, 60, 4, rate);
                Near(t.SourceRange.Start, 4); Near(t.SourceRange.End, 4 + 5 * rate / 100); Near(t.SourceTimeAt(197), 4 + rate / 100);
                Check(t.FirstFrame(new(4, 4.1)) == 137, "Offset unscaled");
                Check(t.FirstFrame(new(t.SourceRange.End, t.SourceRange.End + 1)) == null, "Exclusive item end");
                Reject(() => t.SourceTimeAt(437));
            }
        });
        await Pure("target-fractional-rounding", () =>
        {
            var t = new TargetSnapshot(Guid.NewGuid(), "source", 137, 300, 60, 4, 100);
            Check(t.FirstFrame(new(4.5173, 5)) == 169, "First representable frame");
            Check(t.FirstFrame(new(4.001, 4.002)) == null, "Subframe interval with no frame is omitted");
            Reject(() => (t with { RatePercent = 0 }).Validate()); Reject(() => (t with { RatePercent = double.NaN }).Validate());
        });
        await Pure("target-occurrence-not-source-identity", () =>
        {
            var a = new TargetSnapshot(Guid.NewGuid(), "same", 100, 60, 60, 2, 100); var b = a with { Id = Guid.NewGuid(), StartFrame = 1000 };
            Check(a.FirstFrame(new(2.5, 2.6)) == 130 && b.FirstFrame(new(2.5, 2.6)) == 1030, "Separate Timeline occurrences");
            Check(TimeRange.Union([a.SourceRange, b.SourceRange]).Length == 1, "Only one decoded source range");
        });
        await Pure("multi-profile-40-hits-36-episodes", () =>
        {
            var battle = Enumerable.Range(0, 30).Select(i => new ProfileHit("battle", new(i * 10, i * 10 + 1)));
            var station = Enumerable.Range(0, 10).Select(i => new ProfileHit("station", new(i < 4 ? i * 10 : 400 + i * 10, (i < 4 ? i * 10 : 400 + i * 10) + 1)));
            var q = EpisodeUnion.Build(battle.Concat(station)); Check(q.HitTotal == 40, "40 raw hits"); Check(q.Episodes.Length == 36, "36 unique episodes");
            Check(q.Episodes.Count(e => e.ProfileIds.Length == 2) == 4, "Four dual-attribution episodes");
            Check(q.Episodes.Sum(e => e.Hits.Length) == 40, "No attribution lost"); Check(EpisodeUnion.Build([]).Episodes.IsEmpty, "All profiles off");
        });
        await Pure("profile-any-all-nofm", () =>
        {
            var table = new FeatureTable(Example());
            var p = new SceneProfile("mix", "mix", [new(FeatureAxis.Luma, 0, .9f), new(FeatureAxis.Delta, 0, .9f)], MergeGapSeconds: 0, PreRollSeconds: 0, PostRollSeconds: 0);
            var any = ProfileEvaluator.Evaluate(table, p); var all = ProfileEvaluator.Evaluate(table, p with { Mode = MatchMode.All }); var two = ProfileEvaluator.Evaluate(table, p with { Mode = MatchMode.AtLeast, RequiredMatches = 2 });
            Check(any.Hits.Length > 0 && all.Hits.Length == 1, "Any/All matches");
            Check(all.Hits.SequenceEqual(two.Hits), "All == two of two"); Near(all.Hits[0].Range.Start, 1.5); Near(all.Hits[0].Range.End, 2);
        });
        await Pure("profile-missing-audio-not-zero", () =>
        {
            var table = new FeatureTable(Example());
            var p = new SceneProfile("audio", "audio", [new(FeatureAxis.AudioPeak, 0, 0)]);
            var result = ProfileEvaluator.Evaluate(table, p); Check(!result.Compatible && result.Hits.IsEmpty, "No silent reinterpretation");
            Reject(() => table.Raw(FeatureAxis.AudioRms));
            var disabled = ProfileEvaluator.Evaluate(table, p with { Conditions = [new(FeatureAxis.AudioPeak, 0, 0, false)] });
            Check(disabled.Hits.IsEmpty, "Empty condition set is not match-all");
        });
        await Pure("profile-sensitivity-monotonic", () =>
        {
            var table = new FeatureTable(Example()); var p = new SceneProfile("p", "p", [new(FeatureAxis.Luma, .8f, .9f)], MergeGapSeconds: 0, PreRollSeconds: 0, PostRollSeconds: 0);
            double Duration(double s) => ProfileEvaluator.Evaluate(table, p, s).Hits.Sum(x => x.Range.End - x.Range.Start);
            Check(Duration(.5) <= Duration(1) && Duration(1) <= Duration(2), "Sensitivity expands coverage");
            Reject(() => ProfileEvaluator.Evaluate(table, p, double.NaN));
            Reject(() => ProfileEvaluator.Evaluate(table, p with { Mode = MatchMode.AtLeast, RequiredMatches = 2 }));
        });
        await Pure("generic-large-scene-change", () =>
        {
            var pack = VisualLevels([.. Enumerable.Repeat((byte)0, 8), .. Enumerable.Repeat((byte)255, 8)]);
            var result = GenericFilterEvaluator.Evaluate(new(pack), TransitionIndex.Build(pack), GenericFilterCatalog.LargeSceneChange);
            Check(result.Compatible && !result.Hits.IsEmpty, "Strong visual cut should be detected");
            Check(Anchors(result).Any(t => t >= 3.5 && t <= 4.5), "Scene-cut anchor should stay near the actual cut");
        });
        await Pure("generic-dark-fade-directional", () =>
        {
            var down = VisualLevels([.. Enumerable.Repeat((byte)255, 8), .. Enumerable.Repeat((byte)0, 8)]);
            var up = VisualLevels([.. Enumerable.Repeat((byte)0, 8), .. Enumerable.Repeat((byte)255, 8)]);
            var dark = GenericFilterEvaluator.Evaluate(new(down), TransitionIndex.Build(down), GenericFilterCatalog.DarkFade);
            var bright = GenericFilterEvaluator.Evaluate(new(up), TransitionIndex.Build(up), GenericFilterCatalog.DarkFade);
            Check(!dark.Hits.IsEmpty, "Transition into darkness should be detected");
            Check(bright.Hits.IsEmpty, "Brightening must not be reinterpreted as dark/fade");
        });
        await Pure("generic-quiet-to-activity-directional", () =>
        {
            var quietThenActive = VisualLevels([
                .. Enumerable.Repeat((byte)0, 10),
                0, 255, 0, 255, 0, 255, 0, 255, 0, 255
            ]);
            var activeThenQuiet = VisualLevels([
                0, 255, 0, 255, 0, 255, 0, 255, 0, 255,
                .. Enumerable.Repeat((byte)0, 10)
            ]);
            var rising = GenericFilterEvaluator.Evaluate(new(quietThenActive), TransitionIndex.Build(quietThenActive), GenericFilterCatalog.QuietToActivity);
            var falling = GenericFilterEvaluator.Evaluate(new(activeThenQuiet), TransitionIndex.Build(activeThenQuiet), GenericFilterCatalog.QuietToActivity);
            Check(!rising.Hits.IsEmpty, "Quiet to sustained activity should be detected");
            Check(falling.Hits.IsEmpty, "Activity to quiet must not match the opposite direction");
        });
        await Pure("generic-sensitivity-monotonic-no-audio", () =>
        {
            var pack = VisualLevels([.. Enumerable.Repeat((byte)0, 8), .. Enumerable.Repeat((byte)64, 8)]);
            var table = new FeatureTable(pack); var index = TransitionIndex.Build(pack);
            var low = Anchors(GenericFilterEvaluator.Evaluate(table, index, GenericFilterCatalog.LargeSceneChange, .5));
            var medium = Anchors(GenericFilterEvaluator.Evaluate(table, index, GenericFilterCatalog.LargeSceneChange, 1));
            var high = Anchors(GenericFilterEvaluator.Evaluate(table, index, GenericFilterCatalog.LargeSceneChange, 2));
            Check(low.IsSubsetOf(medium) && medium.IsSubsetOf(high), "Higher sensitivity must not remove generic matches");
            Check(GenericFilterCatalog.Basic.All(g => GenericFilterEvaluator.Evaluate(table, index, g).Compatible), "Visual generic pack must not require audio");
            Reject(() => GenericFilterEvaluator.Evaluate(table, index, GenericFilterCatalog.LargeSceneChange, double.NaN));
        });
        await Pure("union-randomized-coverage", () =>
        {
            var random = new Random(74321);
            for (int trial = 0; trial < 100; trial++)
            {
                var intervals = Enumerable.Range(0, 20).Select(_ => { int start = random.Next(0, 100); return new TimeRange(start, start + random.Next(1, 10)); }).ToArray();
                var merged = TimeRange.Union(intervals);
                for (double t = .5; t < 110; t += 1) Check(intervals.Any(x => t >= x.Start && t < x.End) == merged.Any(x => t >= x.Start && t < x.End), "Union coverage changed");
            }
        });
        string? ffmpeg = Get("--ffmpeg"), ffprobe = Get("--ffprobe"), fixture = Get("--fixture");
        if (ffmpeg != null && ffprobe != null && fixture != null)
        {
            var backend = new FfmpegBackend(ffmpeg, ffprobe); FeaturePack? actual = null;
            await Test("ffmpeg-full-video-audio", async () =>
            {
                actual = await backend.ExtractAsync(fixture);
                Check(actual.Video.Length == 16, "8s at 2fps"); Check(actual.Audio.Length == 160, "8s audio hops");
                Check(actual.Header.HasAudio, "Audio present");
                Check(actual.Video.Take(3).All(v => v.Luma < .01), "Initial black");
                Check(actual.Video[5].Luma > .95 && actual.Video[9].Luma < .01, "White then black");
                Check(actual.Audio.Any(a => a.Rms > .3f) && actual.Audio.Any(a => a.Peak < .001f), "Loud and quiet windows");
            });
            await Test("ffmpeg-trimmed-source-clock", async () =>
            {
                var pack = await backend.ExtractAsync(fixture, 1.25, 3.25);
                Check(pack.Video.Length == 4, "Trim count"); Near(pack.Video[0].TimeSeconds, 1.25); Near(pack.Header.EndSeconds, 3.25);
                Check(pack.Video[0].Luma < .01 && pack.Video[^1].Luma > .95, "Trim corresponds to source");
            });
            await Test("ffmpeg-pack-persist-and-source-independent", async () =>
            {
                actual ??= await backend.ExtractAsync(fixture);
                string copy = FileName("temporary-teaching-source.mkv"); File.Copy(fixture, copy, overwrite: false);
                string packPath = FileName("actual.navfp"); PackStore.Save(packPath, actual); File.Delete(copy);
                var reloaded = PackStore.Load(packPath); Check(reloaded.Header.SourceHash == actual.Header.SourceHash, "Persistent identity");
                Check(reloaded.Video.Length == actual.Video.Length, "No raw video needed after reload");
                File.WriteAllText(FileName("size.json"), JsonSerializer.Serialize(new { sourceBytes = new FileInfo(fixture).Length, packBytes = new FileInfo(packPath).Length, videoRows = actual.Video.Length, audioRows = actual.Audio.Length }));
            });
            await Test("ffmpeg-no-audio-explicit", async () =>
            {
                string silent = FileName("no-audio.mkv");
                await ChildProcess.CaptureAsync(ffmpeg, ["-nostdin", "-v", "error", "-i", fixture, "-map", "0:v:0", "-c", "copy", silent], TimeSpan.FromSeconds(30), default);
                var pack = await backend.ExtractAsync(silent); Check(!pack.Header.HasAudio && pack.Audio.IsEmpty, "Absent, not silent");
            });
            await Test("ffmpeg-corrupt-source-preserved", async () =>
            {
                string corrupt = FileName("invalid.mp4"); File.WriteAllText(corrupt, "not media");
                await RejectAsync(async () => { await backend.ExtractAsync(corrupt); }); Check(File.ReadAllText(corrupt) == "not media", "Source unchanged after failure");
            });
            await Test("ffmpeg-cancel-before-start", async () =>
            {
                await RejectAsync(async () => { await backend.ExtractAsync(fixture, token: new CancellationToken(true)); });
                Check(File.Exists(fixture), "Cancellation never deletes input");
            });
            await Test("backend-active-cancellation", async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await RejectAsync(async () => { await ChildProcess.CaptureAsync(ffmpeg, ["-nostdin", "-v", "error", "-re", "-f", "lavfi", "-i", "anullsrc=r=8000", "-t", "30", "-f", "null", "-"], TimeSpan.FromSeconds(10), cts.Token); });
                Check(watch.Elapsed < TimeSpan.FromSeconds(7), "Cancellation drains and terminates only its own child");
            });
        }
        else if (args.Contains("--require-media")) { failures++; Results.Add(new { id = "media-inputs-required", passed = false }); }
        File.WriteAllText(FileName("results.json"), JsonSerializer.Serialize(new { schema = "navigator.core-tests.v1", source = Environment.GetEnvironmentVariable("GITHUB_SHA"), failures, assertions, cases = Results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Cases: {Results.Count}; assertions: {assertions}; failures: {failures}");
        return failures == 0 ? 0 : 1;
    }
}
