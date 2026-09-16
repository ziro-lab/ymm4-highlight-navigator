# Feature representation v1 — W2 implementation

This is the implementation contract for the existing DESIGN §8/§11 primitives, not a new product scope.

## Shared representation, separate ownership

`FeaturePack` contains a single continuous source range. Runtime may keep it only in a Session index; the learning corpus may persist it through `PackStore`. The representation is shared, the lifetime is not. Disjoint source ranges use separate extractors so an unobserved gap cannot create a fake visual transition. `TargetSnapshot` and `TimeRange.Union` permit overlapping occurrences to share one decoded range while retaining separate Timeline identities.

The v1 extractor uses software FFmpeg child processes with explicit executable paths (no end-user PATH contract), 64×36 RGB working frames, 32×18 luma descriptors, 4×4 luma/delta grids, 16-bin histograms, and video sampling at 1/2/4 fps. Audio is mono 8 kHz, with 50 ms hops and up to 100 ms RMS/peak windows. Temporal patterns and relative salience are calculated from the retained primitive series rather than stored as the only knowledge.

`HasAudio=false` is not a silent audio track. A Profile requiring a missing feature is incompatible; the evaluator never silently treats it as zero or changes an ALL/ANY rule to a compatible subset.

## Clocks

Header ranges and VideoFeature times use source seconds. Sample k is at `start + k/fps`; the sampling grid is the FFmpeg output grid, not an assertion that every underlying compressed source frame has that exact PTS. AudioFeature time is its window END. Profile evaluation aggregates audio windows into the corresponding visual intervals. Exact decoded preview-frame correspondence still belongs to later media validation, not this scalar representation.

`TargetSnapshot.FirstFrame` applies the product's earliest-representable-frame (ceiling) policy after intersecting with the half-open Item source range. The native adapter must compare/use the host's PlaybackRateMap and reject stale targets; detached math is not a replacement for unverified host behavior.

## Persistent file

`.navfp` layout: 8-byte `NAVFP001`, little-endian Int64 uncompressed size, SHA-256 of the uncompressed JSON payload, then Brotli data. The JSON stores versioned typed primitives and immutable vectors, not a final scene score or full-resolution video. Readers bound both encoded and expanded sizes, require all constructor fields, reject nulls/unknown fields/schema mismatches, validate clocks/vector dimensions/ranges and verify the payload hash.

Writer: validate -> unique same-directory staging file -> flush -> reload and validate -> cancellation check -> no-overwrite atomic rename. There is no source-media deletion in W2. A verified Pack is a prerequisite, not permission to delete someone else's recording or archive.

The 128 MiB expanded-file ceiling is a defensive format limit, not a claimed per-sample storage target. Capacity/performance for real game recordings and High-density long ranges remains to be measured. Missing future feature families cannot be reconstructed after raw media has been removed.

## Current limits

CPU extraction only in this slice; GPU capability/fallback is still OPEN. Source fingerprint is full-file SHA-256; before/after file metadata detects ordinary concurrent edits but is not a malicious-writer proof. W3 must separately establish any destructive Inbox transaction and ownership contract. No semantic game Profile quality, learning-value ranking or Profile refinement is claimed by W2 tests.

Core CI runs generated lossless media and negative fixtures without YMM4. It does not certify product native integration, UX, packaging, real X4 recall or user acceptance.
