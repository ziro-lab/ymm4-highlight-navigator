# Feature representation v1 — shared runtime / learning implementation

This document specifies the implemented representation. Product goals remain in [DESIGN.md](DESIGN.md); tested source and limits are in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

## Primitive Pack: shared representation, separate ownership

`FeaturePack` contains one continuous source range. Runtime keeps it in a Session index; the learning corpus persists it through `PackStore`. Disjoint ranges use separate visual extractors, so an unobserved gap is not treated as a visual transition. Source intervals can share decoding while different Timeline Item occurrences remain distinct.

The v1 extractor uses software FFmpeg child processes with explicit executable paths, 64x36 RGB working frames, 32x18 luma descriptors, 4x4 luma/delta grids, 16-bin histograms and video sampling at1/2/4fps. Audio is mono8kHz, 50ms hops with up to100ms RMS/peak windows. Learning import currently uses2fps. Derived patterns are computed from retained primitives, not saved as the only knowledge.

`HasAudio=false` is not silent audio. Existing scalar Profiles requiring absent audio remain incompatible. The new visual-transition matcher does not require audio: nullable audio summaries are retained as auxiliary data, not silently inserted as zero.

## Clocks and usable video domain

Header ranges and video feature timestamps use source seconds. Sample k is `start + k/fps`, the FFmpeg output sampling grid, not proof of every compressed source frame's exact PTS. AudioFeature time is the observed window END. Physical preview-frame correspondence is a separate validation claim.

The first video stream's end is preferred over the overall container duration, which may include a longer audio tail. `MediaInfo` records both `DurationSeconds` (usable video end in the source domain) and `ContainerDurationSeconds`, plus `DurationBasis`:

- `video-stream-duration`: stream start relative to format origin plus stream duration;
- `matroska-video-end-tag`: Matroska/WebM track `DURATION` end timestamp minus format origin;
- `container-fallback`: when neither video-specific field provides a valid positive value.

The Matroska clock parser handles fractional seconds beyond TimeSpan's standard parser. Metadata validity is checked, and normal Pack coverage validation remains. This is not a universal repair for malformed durations, every VFR source or timestamp discontinuity.

Extraction beyond the usable video end but within the container ends at the video boundary. The returned Pack records that actual boundary. It does not synthesize repeated final frames to fill audio-only tail coverage. A requested range entirely outside observed video is rejected. Baseline range/source identity semantics remain unchanged.

`TargetSnapshot.FirstFrame` chooses the first representable frame at/after the source interval start, excluding the Item end. The Plugin checks the host PlaybackRateMap and stale targets; detached math is not a substitute for host evidence.

## .navfp persistence

Layout remains8-byte `NAVFP001`, little-endian Int64 expanded size, SHA256 of uncompressed JSON, then Brotli. Readers bound encoded/expanded sizes, require constructor fields, reject incompatible schema and invalid dimensions/clocks, and verify the hash.

Writer: validate → unique same-directory staging file → flush → reload/validate → cancellation check → no-overwrite publication. The128MiB expanded limit is defensive, not a promised learning-sample footprint. Missing future feature families cannot be reconstructed after raw media is removed.

## Corpus catalog v1

`LearningSample` binds a Pack to Positive memberships and provenance. Sample identity hashes source content SHA256, extractor ID, sampling FPS, start/end. It is not a pathname. Catalog entries also retain the committed Pack file hash, original display names and import time. A second positive label can share the same Pack.

`corpus.json` uses a strict checksummed envelope with version1 and a base64 JSON payload, bounded to24MiB. Write order is verified immutable Pack first, catalog registration last under a local writer lock. A cancelled orphan Pack is not a registered sample and may be safely reused only after revalidation. Corrupt registered files do not become empty data.

No destructive input handling is implemented. This format's successful publication is not permission to delete a source owned by a YMM4 project or another tool.

## Local TransitionIndex v1

Algorithm ID: **`local-transition-v1`**.

Each proposal retains source center, before/transition/after intervals, full-context flag, strength and a32-dimensional visual signature:

- before/after/instant frame delta;
- signed changes in luma/contrast/chroma/edge/extreme ratios;
- histogram distance and changed-grid fraction;
-16 signed grid differences;
-6 aligned delta time bins.

Local prefix statistics use roughly2seconds either side. There is no assumed central positive timestamp. No proposal is fabricated at the first sample without an observed before state. Multiple proposals per clip are allowed. A fixed low proposal floor0.02 and deterministic local suppression form a reusable index; this proposal floor is a current recall limitation, not an oracle for all important scene changes.

Before/after/peak audio is nullable auxiliary evidence. The current matcher is visual-only. Different input sampling densities still carry their Header FPS; invariance across every density is not claimed.

## Filter / match / replay

`TransitionFilter` schema1 stores algorithm ID, normalized Group/Name, revision and parent,1-12 OR Patterns and review context. Each Pattern stores an observed representative signature, radius, minimum strength and supporting sample IDs. Authoring uses up to4 prototypes per uncovered clip and512 per iteration; these bounds do not cap runtime scanning to a top-K per video.

Current visual distance weights are0.35 activity,0.35 state change,0.15 grid and0.15 temporal shape. Default radius0.18 and minimum strength proportional to prototype strength are initial heuristics, not calibrated semantic probabilities.

At runtime, sensitivity s expands the acceptable radius to `radius*s` and relaxes minimum strength to `minimum/s`. Match centers are then wrapped in review context and passed to existing interval union/projection. Matching reuses the TransitionIndex and never requires raw decoding. Because review intervals merge, final episode counts need not increase monotonically with sensitivity.

Coverage replay is fixed at s=1 and returns `CoveredCandidate`, `HardPositive` or `NoTransition`. A clip-level match is not confirmation of the intended event. Corpus replay used for authoring/regression is not independent generalization evaluation.

`FilterStore` persists immutable revisions and an active-head index in the same bounded envelope format. Apply compares expected Corpus/Filter revisions, replays current data and rejects lost previous positive matches. Publication and rollback are explicit. Negative labels, Contrast refinement and density gates are not represented as completed functionality in this version.

## Current validation boundary

Generated media exercises codecs and clocks; synthetic patterns exercise deterministic math, multi-pattern OR, replay and negative failure paths. Product native tests separately validate the compiled UI and runtime connection on a pinned host. None alone certifies long-recording latency, real X4 detection quality, every future host version, packaging or user acceptance.

CPU only. GPU remains OPEN. Full-file SHA256 plus before/after file metadata guards normal accidental change, not malicious concurrent mutation. Real recording memory/throughput and representation-size optimization remain to be measured.
