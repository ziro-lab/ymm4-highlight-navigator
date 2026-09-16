# Implementation Roadmap — v0.4.1

Authority: [DESIGN.md](DESIGN.md) §14。正確なrun/source/未証明範囲は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Current state

**FIRST WORKING CHECKPOINT / NOT A COMPLETE RELEASE**

- W1 basic Target/Projection/Review spine: implemented, current product native **27 assertions PASS**。
- W2 CPU primitive/Pack/pure engine: implemented, Core27 cases PASS。YMM4 bundled FFmpeg locator integrationもnative PASS。
- W4 no-redecode multi-profile runtime: basic path implemented with generic seed Profiles。
- W3 Transition-aware Corpus intake, W5 Filter authoring/coverage, W6 hard-positive/negative refinement: not implemented yet。
- W7 real-recording quality/performance/GPU/distribution/user acceptance: open。

今回の方針変更は、既存W1/W2/W4を作り直すものではない。**次は「教材動画 → Transition抽出 → Filter生成 → 長尺Review」へ最短で到達する。**

## Dependency overview

```text
W1 Target / Projection ───────────────────────────────┐
                                                      ├─> W4 Runtime Review ──────┐
W2 Shared Feature Engine ─> W3 Corpus+Transition ─> W5 Filter Authoring/Coverage ├─> W6 Refinement
                                                      └───────────────────────────┤
                                                                         W7 Real Value / Distribution
```

## Product direction fixed for W3-W6

- Primary goal is **Recall / 見逃し低減**。
- Main learning target is **Transition**: `before → transition → after`。
- Clip前後の余白はnoise前提ではなく、状態差を見るための教材として使える。
- 1 Filterは複数Transition PatternをORで持てる。
- Positive教材は現Filterで再判定し、`Covered Positive / Hard Positive`へ分ける。
- Covered Positiveは重点学習から外してよいが、Regressionから外さない。
- Negative Authorityは実際の誤検出に対する人間の`これは違う`。
- 他Profile corpus / Folder absenceは自動Hard Negativeにしない。
- Global SensitivityはMain ReviewのPrimary control。変更時に再Decode/再学習しない。
- Recording Archiveは教材切り出しを楽にする任意ツールで、Navigatorの前提ではない。

## W1 — YMM4 Target / Projection Spine — IMPLEMENTED

**Purpose:** explicit Target Setからcandidateを正しいTimeline occurrenceへJumpする。

**Baseline:** session-local identity、immutable target snapshot、stale rejection、PlaybackRateMap境界、source-range union、same-source別occurrence、integer seek、日本語Tool。

**Compatibility:** YMM4 build番号だけで拒否しない。必要surfaceが変わった場合は該当機能をfail closedし、YMM4 processへ通常の未処理例外を逃がさない。

**Remaining:** broader reload/scene/Undo lifecycle、physical input、decoded-frame correspondence。

## W2 — Shared Feature Engine — IMPLEMENTED

**Purpose:** RuntimeとLearningが同じsemantic前primitive/schemaを使う。

**Baseline:** visual/audio primitives、FeaturePack、PackStore、missing-feature compatibility、real FFmpeg child process、generated media、cancel/failure separation。

Plugin runtimeはYMM4同梱FFmpegをpublic locator capability経由で利用し、独自FFmpeg copy/PATH fallbackを持たない。Coreはexplicit path inputでYMM4非依存。

**Remaining:** temporal derived featureの拡充、長尺/multiple-source実測、容量/メモリ、GPU resize/fallback、他codec/VFR。

## W3 — Learning Corpus + Transition Extraction — NEXT

**Purpose:** 普通の動画/Folderを教材としてPersistent Pack化し、各Clipの場面切り替わり候補を再利用可能にする。

### Slice W3-A — Non-destructive batch import

- Folder / Group / Filter name preview;
- fingerprint / dedupe;
- multiple Positive membership;
- provenance / schema / extractor version;
- Pack write → reload validation → sample registration transaction;
- Partial / Error / Cancelを成功扱いしない。

**Exit:** 少数教材を一括Importし、重複取込が安全。

### Slice W3-B — Raw-video-free corpus replay

- 元動画を外してCorpus reload;
- FeatureTable/Profile evaluatorへ再入力;
- incompatible schemaを明示。

**Exit:** 元動画なしで同じprimitive/評価入力を再構築できる。

### Slice W3-C — Transition candidate extraction

- Clip内部のMaterialなFeature change候補を抽出;
- clip中央を正解と仮定しない;
- 1 Clipに複数候補を許す;
- `before / transition / after` windowを保持;
- delta rise / histogram change / grid change / audio burst / stability change等を利用。

**Exit:** 各Positive Clipから再現可能なTransition候補とwindowを得られる。

**Budget:** Core/pure integration中心。YMM4 nativeはhost/UI境界を変えない限り不要。

## W4 — Multi-Profile Runtime Review — BASIC IMPLEMENTED

**Baseline:** per-Profile hit、OR/Union、overlap merge/attribution、unique candidate vs hit total、Timeline順Prev/Next/List、Global Sensitivity、no-redecode re-query。

**Next integration work:** W5で生成したFilter revisionをRuntimeへ登録して同じQuery pathへ流す。

Global Sensitivityは常時見えるPrimary controlとして維持する。

```text
低感度 → 厳しく拾う → 候補少なめ
高感度 → 広く拾う → 見逃しにくい / 候補多め
```

Filter別補正は必要性が確認されるまでMain UIへ増やさない。

## W5 — Transition Filter Authoring + Coverage Loop

**Purpose:** Positive教材の共通する時間方向変化から実際に使えるTransition Filterを生成する。

### Slice W5-A — Initial filter candidate

- 各ClipのTransition候補をalignment;
- common temporal tendencies抽出;
- Before / Transition / After条件生成;
- 最初は最小Patternで開始。

### Slice W5-B — Multiple pattern support

Hard Positiveを既存Patternへ平均するとRecallが落ちる場合、

```text
Filter: 戦闘開始
  Pattern A OR Pattern B OR Pattern C
```

のように追加Patternへ分ける。

### Slice W5-C — Positive replay / coverage

Filter Candidateを全Positiveへ再適用し、

```text
Covered Positive
Hard Positive
```

を得る。

学習優先度はHard Positiveを上げ、Covered PositiveはRegressionへ残す。

### Slice W5-D — Runtime trial

- generated Filter revisionをRuntimeへ登録;
- 長尺Feature Indexへ適用;
- Global Sensitivityで候補量を即時調整;
- 再Decodeなし。

**Exit:** `教材動画数本 → Filter生成 → 全Positive再判定 → 長尺へ適用 → 感度調整 → Prev/Next` が一気通貫で動く。

これは最初の大きなFirst Value checkpoint。

## W6 — Hard Positive / Explicit Negative / Revision

**Purpose:** 見逃しと実際の誤検出だけを材料にFilterを育てる。

### Slice W6-A — Explicit Negative feedback

Runtime候補に`これは違う`を追加し、その前後Feature WindowをNegative Authorityとして保存する。動画再切り出しを必須にしない。

### Slice W6-B — Contrast refinement

- Hard Positive coverageを増やす;
- PositiveにもNegativeにもあるFeatureは識別力を下げる;
- Positive側に残る差を優先;
- 他ProfileをHard Negative化しない。

### Slice W6-C — Regression / density gate

Candidate適用前に:

- Covered Positiveを落としていない;
- Hard Positive coverageが改善;
- Explicit Negative hitが減る、または悪化しない;
- 長尺candidate densityがMaterialに悪化しない;
- query costがMaterialに悪化しない。

Precision最大化は要求しない。**Recallを守りながらReview量を実用域へ下げる。**

### Slice W6-D — Revision lifecycle

- Preview;
- explicit Apply;
- new revision;
- rollback。

**Exit:** 見逃し改善と誤検出低減を繰り返しても既存Positiveを壊さず、前revisionへ戻せる。

## W7 — Real Recording / Performance / Distribution

**Purpose:** 実編集で価値があることと通常配布を成立させる。

評価:

- X4実長尺でRecall / candidate density / review reduction;
- 3h recordingのingest / memory / query latency;
- sensitivity操作の実用性;
- Profile/Pattern数増加時のquery cost;
- GPU path + software fallback;
- `.ymme` install / upgrade;
- supported YMM4更新時のcapability compatibility;
- hands-on user acceptance。

NavigatorはFFmpeg binaryを配布せず、supported YMM4のhost backendを使う。

Goal到達後にGeneral Profile自動cluster、OCR/Object Detection、Heavy Detectorへ自動拡張しない。

## Next execution

[IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) に従い、まず **W3-A → W3-B → W3-C** を実装する。W1/W2/W4を再設計しない。

実録画やprivate Corpusをrepoへcommitしない。初期検証はgenerated/synthetic clipでTransitionを再現し、実Archive/手動切り出し教材は後段のreal-value validationへ使う。
