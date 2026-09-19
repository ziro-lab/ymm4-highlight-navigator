# W1-R Edit-time Rebinding — v0.4.2 checkpoint

**IMPLEMENTED / PURE + PRODUCT NATIVE VALIDATED / NOT A COMPLETE RELEASE**

DESIGN v0.4.2 D-16/D-17を実装。D-18のAnchor契約に互換な情報は保持するが、W4-M「見どころを確保」は未実装。W6 Explicit Negative、録画アーカイブへの変更・依存追加は含まない。

`decided != implemented != validated != accepted`。ここでの合格は生成素材による機能統合であり、実素材の検出品質、物理操作、配布・通常導入・アップグレード、ユーザー受入を意味しない。

## Exact evidence chain

- Tested source: **`f9ce3cd9bb59ddc3f72774064339a596b60c099d`**
- Tested tree: `d847485adca53f2e2e8f09c6638fae0e484da317`
- [Run 35437820506](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35437820506), attempt 1, both jobs successful
- Pure preflight job: `105883380189`
- Product native job: `105883497783`
- Exact host: **YMM4 Lite 4.56.1.0 / Windows / Timeline 60 FPS**
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`

| Evidence | Artifact ID | ZIP SHA256 |
|---|---|---|
| `navigator-core-evidence` | `10582966996` | `a2690fd093465204dcff645aaa892394516f1b4b226b3caadd9f42fbdb0a8be1` |
| `navigator-native-checkpoint` | `10582327936` | `56ca5fc5e527c442abc7b42ec06da6f9eb1ba03c3eaf27a9e715efcaf23ca7d8` |

Exact source ZIP in the pure artifact: SHA256 `1ad72e0cc0144e6cb017bf532309e8394afb8a13f3044d4abcda57cddb7c601c`.

Actual native-loaded distribution DLLs:

- Plugin: `1d59a59ed778d76f0cd812de3e90cdd4facdc503c9c6387f886493635c9c52b1`
- Core: `6a3af398f09b6cc9106110f50b976d2e28ffe814d91d4bfa271bd6ba75642727`

ZIPs were downloaded and hashed; required IDs, checkout, statuses, decoder trace and DLL/native hash correspondence were independently checked. Product and proof builds both had **0 warnings / 0 errors**. Artifact retention remains 7 days. Later documentation commits/main merge are not relabeled as the tested source.

## Verification results

| Suite | Result |
|---|---|
| Existing Core | 27 cases / 11,147 assertions / 0 failures |
| New W1-R Pure | 29 cases / 91 assertions / 0 failures |
| Existing Learning, ordinary generated media | 35 cases / 60 assertions / 0 failures |
| Same Learning, stream-copy trimmed media | 35 cases / 60 assertions / 0 failures |
| Real product native | **69 independently required assertions PASS** |

Learning is 35 distinct cases repeated under two media conditions, not 70 independent functions. Core's many interval assertions are deterministic coverage, not 11,147 separate features. Native 69 includes 46 unchanged old assertions, the old strict-stale assertion split into move-rebind/source-mutation checks, one stronger 50%/200% anchor projection check, and 20 W1-R checks.

The old strict test was not silently removed: `stale_target_rejected_atomically` became `timeline_move_rebind` and `source_mutation_rejected_atomically`. Fractional-frame/exclusive-end, learning/runtime, host-bundled backend, non-destructive media/item, background heartbeat and narrow-view checks remain.

### Observed product sequence

The 24-second generated fixture has anchors 2/6/10/14/18/22 seconds. Its review ranges include pre-roll but Jump uses the actual hit anchor. Analysis ran once for this fixture. A monotonic actual `ExtractAsync` call counter increased from 1 to 2 for that analysis and remained **2 throughout all subsequent edits/requeries**. The test-owned host's ffmpeg.exe was temporarily unavailable during the sequence and restored in `finally`.

| Stage | Actual result |
|---|---|
| Initial Jump | Source 2s -> Frame 2620 |
| Real Split at source 8s, Next | Source 6s -> Frame 2860; old object replaced, Index kept |
| Head Trim right piece to source 11s, Next | Only anchor 10s unavailable; skips to 14s -> Frame 3340 |
| Move right piece to Frame 100, Next | Anchor 18s -> **Frame 520**, not cached 3580 |
| Real Copy/Paste of identical source range | Anchor 22s -> original-lineage **Frame 760**, not copied occurrence |
| Second Split / actual emitted Undo / Redo | Each Next resolves anchor 22s -> Frame 760 from current semantic state |
| Tail/head Trim removes remaining anchors | All 6 candidates retained but unavailable; no Jump to remaining valid copy |
| Requery with decoder absent | Same 6 stable identities/order and existing visited state; one Feature Index retained |

The outer runner verified that YMM4 was still alive; normal unavailable Jump did not escape the guarded command, and the native dispatcher unhandled counter stayed 0. Generated source bytes were unchanged. Main 360x480 and learning 400x640/640x640 views were rendered with required controls inside bounds; this is not physical input/installation/user acceptance.

## Implementation boundaries

`ReviewSourceSession` retains capture-time target ID/order, normalized source key, analyzed source coverage and source stamp. `OccurrenceLineage` handles mutable host bindings using detached reference tokens; Core stores no YMM4 object/decoder. Adapter uses weak tokens for host references and reads native PlaybackRateMap on the UI thread. Candidate Frame is nullable/refreshable display projection, never navigation authority.

Transition hits carry `TransitionMatch.CenterSeconds`; generic hits retain actual matched sample times. Union retains all contributing hits/anchors and attribution. One merged episode remains one candidate, whose primary anchor is the earliest retained real hit. Review range changes do not define visited identity; identity is ReviewTargetId + normalized source + anchor.

Prev/Next uses capture-time target order + anchor. Known references survive Trim/Move; historical refs can recover after Undo/Redo. Only disappeared bindings may adopt fresh replacements. Adoption requires a **unique complete source/timeline partition**, same source/rate/FPS/layer, and excludes previously observed foreign references. Path-counting is capped at two to avoid exponential ambiguous-partition enumeration. Whole clones are not split evidence. Unexplained disappearances are not reconsidered later merely because a paste appears.

A missing anchor invalidates only that candidate. Source deletion/length/write-stamp mutation still requests reanalysis; unsupported mapping affects only the affected projections. FFmpeg packaging/locator, FeaturePack/Table, TransitionIndex, learning/Corpus, sensitivity no-redecode, OR/Union and capability-based compatibility remain intact. No version-number-only refusal was introduced.

### Remaining unsupported or unproven cases

- Split plus other edits before a unique complete partition can be observed may fail closed. A missing/repositioned/trimmed partition is not guessed. Ambiguous replacements and indistinguishable duplicates are not arbitrarily selected.
- Undo/Redo without a known historical ref must meet ordinary replacement evidence; arbitrary new merged/cloned objects are not presumed lineage.
- Variable, zero and reverse playback are outside scope. Positive constant 50%/100%/200% cases are covered; this is not a claim about every future host mapping.
- Source checking retains the existing existence/length/write-timestamp contract, not a full-media hash on each Next. A deliberately rewritten file preserving both size and timestamp is not detected by that stamp. Rename/delete/referenced-file changes require reanalysis or affected-operation recovery.
- Visited/lineage are review-session-local, not persistent across project reload. Wider scene/reload/cancel races, long-recording performance, arbitrary codecs/VFR and future ABI behavior remain separate work.
- When a Union episode's primary anchor is removed, that candidate is unavailable even if another contributing hit survives. All anchor metadata remains retained; the implementation does not silently move its identity to another hit.

## Adopted Lab evidence / failure history

No host experiment was reimplemented or changed. Contracts reused from [LAB_REFERENCES.md](LAB_REFERENCES.md):

- Split lifecycle: Lab merge `6ec7e54ef4b398322b6eabeee0daa2e97841306f`, run `35359881285`, 23 required assertions.
- Trim/Move/Copy/Paste/UndoRedo: Lab merge `5748cc3f691ca4029e701c5fef0d145ff0d4ee2f`, tested source `df92cf66d64b3beea3b7e4fc4324539a6f373d27`, run `35424106866`, 25 required assertions.

Initial W1-R pure source `e713ac57686c7541125a56d1c001557a89cbf6a3`, run `35437024948`, exposed one old ProfileHit value-equality regression after anchor arrays were added. It was fixed by content-based equality/hash, not by weakening/removing the old test. Source `0eba16f4299e1c51c2d19f5e5fdcddf3b0421add`, run `35437180810`, passed all pure/learning suites. The final product source above reran these same suites before the first W1-R product-native execution, which passed all 69 requirements.
