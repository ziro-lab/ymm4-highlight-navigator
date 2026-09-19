using System.Collections.Immutable;
using System.Text.Json;
using Ymm4HighlightNavigator.Core;

var results = new List<object>(); int assertions = 0, failures = 0;
void Check(bool value, string reason = "Assertion failed") { assertions++; if (!value) throw new InvalidOperationException(reason); }
void Test(string id, Action test)
{
    int before = assertions;
    try { test(); results.Add(new { id, passed = true, assertions = assertions - before }); Console.WriteLine("PASS " + id); }
    catch (Exception ex) { failures++; results.Add(new { id, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + id + ": " + ex); }
}
void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
const string source = "C:/recording/video.mkv";
OccurrenceObservation Item(int frame = 100, int length = 600, double offset = 1, double rate = 100, Guid? id = null, int layer = 2)
    => new(id ?? Guid.NewGuid(), new(Guid.NewGuid(), source, frame, length, 60, offset, rate), layer);
OccurrenceLineage Lineage(OccurrenceObservation root, params OccurrenceObservation[] others)
    => new(new(root.Mapping!, 0, new(100, 200)), root, others.Select(o => o.ReferenceId));
(OccurrenceObservation Left, OccurrenceObservation Right) Split(OccurrenceObservation root, int leftLength = 240)
{
    var m = root.Mapping!;
    return (Item(m.StartFrame, leftLength, m.OffsetSeconds, m.RatePercent, layer: root.Layer),
        Item(m.StartFrame + leftLength, m.LengthFrames - leftLength, m.OffsetSeconds + leftLength / 60d * m.RatePercent / 100, m.RatePercent, layer: root.Layer));
}
FeaturePack Pack(double start = 0)
{
    var extractor = new VisualExtractor(); var frames = ImmutableArray.CreateBuilder<VideoFeature>();
    for (int i = 0; i < 20; i++) frames.Add(extractor.Extract(Enumerable.Repeat((byte)(i is 8 or 9 ? 255 : 0), 6912).ToArray(), start + i / 2d));
    return new(new(1, FeatureFormat.Extractor, 2, start, start + 10, false, new string('0', 64), "rebinding-generated"), frames.ToImmutable(), []);
}
Test("transition-center-not-preroll", () =>
{
    var index = TransitionIndex.Build(Pack(96)); var point = index.Candidates.OrderByDescending(c => c.Strength).First();
    var filter = new TransitionFilter(1, TransitionIndex.Algorithm, new("X4", "anchor"), 0, 0,
        [new(new string('a', 64), point.Signature, .18f, .02f, [new string('b', 64)])], 2, 1);
    var matches = TransitionMatcher.Match(index, filter);
    var hits = TransitionMatcher.Evaluate(index, filter).Hits;
    Check(matches.Any(m => Math.Abs(m.CenterSeconds - 100) < 1e-6), "Known generated transition at 100s");
    Check(hits.SelectMany(h => h.AnchorSourceTimes).Order().SequenceEqual(matches.Select(m => m.CenterSeconds).Order()), "Every matched center retained");
    Check(hits.Any(h => h.Range.Start < h.AnchorSourceTimes.Min()), "Pre-roll is context, not anchor");
});
Test("generic-actual-hit-and-target-clip", () =>
{
    var profile = new SceneProfile("bright", "bright", [new(FeatureAxis.Luma, 0, .8f)], MergeGapSeconds: 0, PreRollSeconds: 2, PostRollSeconds: 1);
    var hit = ProfileEvaluator.Evaluate(new(Pack()), profile).Hits.Single();
    Check(hit.Range == new TimeRange(2, 6)); Check(hit.AnchorSourceTimes.SequenceEqual(new[] { 4d, 4.5 }));
    Check(hit.Clip(new(4.25, 5))!.AnchorSourceTimes.SequenceEqual(new[] { 4.5 }));
    Check(hit.Clip(new(2, 3)) is null, "Context-only coverage is not a hit");
});
Test("union-attribution-all-anchors", () =>
{
    var q = EpisodeUnion.Build([new("A", new(98, 101), [100]), new("B", new(99, 103), [100.5, 102])]);
    var e = q.Episodes.Single(); Check(q.HitTotal == 2 && e.Hits.Length == 2 && e.ProfileIds.SequenceEqual(new[] { "A", "B" }));
    Check(e.Range == new TimeRange(98, 103) && e.AnchorSourceTime == 100);
    Check(e.AnchorSourceTimes.SequenceEqual(new[] { 100d, 100.5, 102 }));
});
Test("anchor-invalid-metadata-rejected", () =>
{
    Reject(() => EpisodeUnion.Build([new("x", new(1, 2), [])]));
    Reject(() => EpisodeUnion.Build([new("x", new(1, 2), [double.NaN])]));
    Reject(() => EpisodeUnion.Build([new("x", new(1, 2), [2])]));
    Check(EpisodeUnion.Build([new("legacy", new(1, 2))]).Episodes[0].AnchorSourceTime == 1);
});
Test("stable-order-not-current-frame", () =>
{
    var items = new[] { (Key: new StableReviewOrder(0, 6), Frame: 1), (Key: new StableReviewOrder(1, 1), Frame: 0), (Key: new StableReviewOrder(0, 2), Frame: 900) };
    var expected = new[] { new StableReviewOrder(0, 2), new StableReviewOrder(0, 6), new StableReviewOrder(1, 1) };
    Check(items.OrderBy(x => x.Key).Select(x => x.Key).SequenceEqual(expected));
    Check(items.Select(x => (x.Key, Frame: 10000 - x.Frame)).OrderBy(x => x.Key).Select(x => x.Key).SequenceEqual(expected));
});
Test("visited-stable-distinct-occurrence", () =>
{
    var target = Guid.NewGuid(); var identity = ReviewCandidateIdentity.Create(target, source, 100);
    var visited = new HashSet<ReviewCandidateIdentity> { identity };
    Check(visited.Contains(ReviewCandidateIdentity.Create(target, "c:\\recording\\VIDEO.mkv", 100)));
    Check(!visited.Contains(ReviewCandidateIdentity.Create(Guid.NewGuid(), source, 100)));
    Check(!visited.Contains(ReviewCandidateIdentity.Create(target, source, 100.5)));
});
Test("move-only-current-projection", () =>
{
    var root = Item(); var l = Lineage(root); var moved = root with { Mapping = root.Mapping! with { StartFrame = 900 } };
    Check(l.Resolve(3).Occurrence!.Mapping!.FirstFrame(new(3, 3.1)) == 220);
    l.Refresh([moved]); Check(l.Resolve(3).Occurrence!.Mapping!.FirstFrame(new(3, 3.1)) == 1020);
    Check(l.KnownReferenceCount == 1);
});
Test("head-trim-per-candidate", () =>
{
    var root = Item(); var l = Lineage(root);
    l.Refresh([root with { Mapping = root.Mapping! with { StartFrame = 400, LengthFrames = 300, OffsetSeconds = 6 } }]);
    Check(!l.Resolve(3).Available); Check(l.Resolve(7).Available); Check(l.Resolve(6).Available); Check(!l.Resolve(11).Available);
});
Test("tail-trim-per-candidate", () =>
{
    var root = Item(); var l = Lineage(root); l.Refresh([root with { Mapping = root.Mapping! with { LengthFrames = 300 } }]);
    Check(l.Resolve(3).Available); Check(!l.Resolve(6).Available); Check(!l.Resolve(8).Available);
});
Test("split-partition-50-100-200", () =>
{
    foreach (double rate in new[] { 50d, 100d, 200d })
    {
        var root = Item(rate: rate); var l = Lineage(root); var (left, right) = Split(root); l.Refresh([right, left]);
        double cut = right.Mapping!.OffsetSeconds;
        Check(l.Resolve(1).Occurrence!.ReferenceId == left.ReferenceId);
        Check(l.Resolve(cut).Occurrence!.ReferenceId == right.ReferenceId);
        Check(l.Resolve(cut).Occurrence!.Mapping!.FirstFrame(new(cut, cut + .1)) == 340);
        Check(!l.Resolve(root.Mapping!.SourceRange.End).Available); Check(l.KnownReferenceCount == 3);
    }
});
Test("repeated-split-and-piece-move", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root); l.Refresh([left, right]);
    var (middle, last) = Split(right, 180); l.Refresh([last, left, middle]);
    var moved = last with { Mapping = last.Mapping! with { StartFrame = 50 } }; l.Refresh([moved, middle, left]);
    Check(l.Resolve(9).Occurrence!.ReferenceId == last.ReferenceId); Check(l.Resolve(9).Occurrence!.Mapping!.FirstFrame(new(9, 9.1)) == 110);
    Check(l.Resolve(6).Occurrence!.ReferenceId == middle.ReferenceId); Check(l.KnownReferenceCount == 5);
});
Test("copy-known-lineage-priority", () =>
{
    var root = Item(); var l = Lineage(root); var copy = Item(frame: 900);
    l.Refresh([copy, root]); Check(l.Resolve(3).Occurrence!.ReferenceId == root.ReferenceId); Check(l.KnownReferenceCount == 1);
});
Test("preexisting-copy-not-replacement", () =>
{
    var root = Item(); var (left, right) = Split(root); var l = Lineage(root, left, right);
    l.Refresh([left, right]); Check(!l.Resolve(3).Available && !l.Resolve(7).Available); Check(l.KnownReferenceCount == 1);
});
Test("full-clone-not-split-evidence", () =>
{
    var root = Item(); var l = Lineage(root); l.Refresh([Item()]);
    Check(l.Resolve(3).Status == RebindStatus.Ambiguous); Check(l.KnownReferenceCount == 1);
});
Test("ambiguous-partition-fail-closed", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root);
    var rightCopy = right with { ReferenceId = Guid.NewGuid() }; l.Refresh([left, right, rightCopy]);
    Check(l.Resolve(3).Status == RebindStatus.Ambiguous && l.Resolve(8).Status == RebindStatus.Ambiguous);
    Check(l.KnownReferenceCount == 1); l.Refresh([left, right]); Check(!l.Resolve(8).Available, "Removing one copy later is not new lineage proof");
});
Test("incomplete-partition-not-guessed", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root);
    l.Refresh([left, right with { Mapping = right.Mapping! with { StartFrame = 341 } }]);
    Check(!l.Resolve(3).Available && !l.Resolve(8).Available);
});
Test("wrong-source-offset-not-partition", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root);
    l.Refresh([left, right with { Mapping = right.Mapping! with { OffsetSeconds = 5.1 } }]); Check(!l.Resolve(8).Available);
});
Test("layer-discriminator-not-ignored", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root);
    l.Refresh([left with { Layer = 3 }, right with { Layer = 3 }]); Check(!l.Resolve(3).Available);
});
Test("delete-piece-only-and-undo", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root); l.Refresh([left, right]);
    l.Refresh([right]); Check(!l.Resolve(3).Available && l.Resolve(8).Available);
    l.Refresh([right, left]); Check(l.Resolve(3).Occurrence!.ReferenceId == left.ReferenceId);
});
Test("undo-redo-historical-references", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root); l.Refresh([left, right]);
    l.Refresh([root]); Check(l.Resolve(8).Occurrence!.ReferenceId == root.ReferenceId);
    l.Refresh([right, left]); Check(l.Resolve(8).Occurrence!.ReferenceId == right.ReferenceId);
    l.Refresh([root]); Check(l.Resolve(3).Occurrence!.ReferenceId == root.ReferenceId); Check(l.KnownReferenceCount == 3);
});
Test("deleted-target-later-paste-not-adopted", () =>
{
    var root = Item(); var l = Lineage(root); l.Refresh([]); var (left, right) = Split(root); l.Refresh([left, right]);
    Check(!l.Resolve(3).Available); l.Refresh([root, left, right]); Check(l.Resolve(3).Occurrence!.ReferenceId == root.ReferenceId);
});
Test("unsupported-known-ref-not-deleted", () =>
{
    var root = Item(); var l = Lineage(root); var copy = Item();
    l.Refresh([root with { Mapping = null, Problem = "reverse" }, copy]); Check(l.Resolve(3).Status == RebindStatus.Unsupported);
    Check(l.KnownReferenceCount == 1); l.Refresh([root, copy]); Check(l.Resolve(3).Occurrence!.ReferenceId == root.ReferenceId);
});
Test("source-reference-change-is-not-edit", () =>
{
    var root = Item(); var l = Lineage(root); l.Refresh([root with { Mapping = root.Mapping! with { SourceKey = "C:/other.mkv" } }]);
    Check(l.Resolve(3).Status == RebindStatus.SourceChanged);
});
Test("source-mutation-stamp-and-deletion", () =>
{
    var original = new SourceStamp(100, 200); Check(original.Matches(true, original));
    Check(!original.Matches(false, original)); Check(!original.Matches(true, new(101, 200))); Check(!original.Matches(true, new(100, 201)));
});
Test("analyzed-coverage-stays-immutable", () =>
{
    var root = Item(); var l = Lineage(root); l.Refresh([root with { Mapping = root.Mapping! with { LengthFrames = 1200 } }]);
    Check(l.Resolve(10).Available); Check(!l.Resolve(11).Available); Check(!l.Resolve(15).Available);
});
Test("navigation-skip-and-all-unavailable", () =>
{
    Check(ReviewNavigation.Next(4, -1, 1, i => i == 1 || i == 3) == 1);
    Check(ReviewNavigation.Next(4, 1, 1, i => i == 1 || i == 3) == 3);
    Check(ReviewNavigation.Next(4, 3, -1, i => i == 1 || i == 3) == 1);
    Check(ReviewNavigation.Next(4, 3, 1, _ => true) is null);
    Check(ReviewNavigation.Next(4, -1, 1, _ => false) is null);
});
Test("navigation-rebinding-pure-no-media", () =>
{
    var root = Item(); var l = Lineage(root); var (left, right) = Split(root); l.Refresh([left, right]);
    double[] anchors = [2, 4, 6, 8]; int cursor = -1;
    for (int i = 0; i < 4; i++) { cursor = ReviewNavigation.Next(4, cursor, 1, n => l.Resolve(anchors[n]).Available)!.Value; Check(cursor == i); }
    Check(!typeof(OccurrenceLineage).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Any(f => f.FieldType == typeof(FfmpegBackend)));
});
Test("partition-path-count-bounded", () =>
{
    var root = Item(length: 6000); var l = Lineage(root);
    var pieces = Enumerable.Range(0, 100).SelectMany(i => new[] { Item(100 + i * 60, 60, 1 + i), Item(100 + i * 60, 60, 1 + i) }).ToArray();
    l.Refresh(pieces); Check(l.Resolve(3).Status == RebindStatus.Ambiguous && l.KnownReferenceCount == 1);
});
Test("duplicate-observation-token-rejected", () =>
{
    var root = Item(); var l = Lineage(root); Reject(() => l.Refresh([root, root]));
});
int arg = Array.IndexOf(args, "--out"); string output = arg < 0 ? "out/rebinding-tests" : args[arg + 1];
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { schema = "navigator.rebinding-tests.v1", checkout = Environment.GetEnvironmentVariable("GITHUB_SHA"), failures, assertions, cases = results }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Rebinding cases {results.Count}; assertions {assertions}; failures {failures}");
return failures == 0 ? 0 : 1;
