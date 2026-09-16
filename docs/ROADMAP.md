# Implementation Roadmap — v0.4

Authority: [DESIGN.md](DESIGN.md) §17。Goal/Scopeを変更せず実装の段階を追う。正確なrun/source/未証明範囲は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Current state

**FIRST WORKING CHECKPOINT / NOT A COMPLETE RELEASE**

- W1 basic Target/Projection/Review spine: implemented, current product native **27 assertions PASS**。
- W2 CPU primitive/Pack/pure engine: implemented, Core27 cases PASS。YMM4 bundled FFmpeg locator integrationもnative PASS。
- W4 no-redecode multi-profile runtime: basic path implemented with generic seed Profiles。
- W3 Corpus intake, W5 reverse classification, W6 refinement: not implemented yet。
- W7 GPU/performance/real-game quality/installer/user acceptance: open。

W1 native readiness does not mean all project lifecycle/physical input/decoder correspondence is proved。W2 Pack serialization does not mean a persistent Corpus manager already exists。

## Dependency overview

```text
W1 Target / Projection ────────────────────┐
                                           ├─> W4 Multi-Profile Runtime Review ─┐
W2 Shared Feature Engine ─> W3 Corpus ─> W5 Reverse Classification ─> W6 Refinement
                                           └───────────────────────────────────┤
                                                                  W7 First Value / Distribution
```

W1/W2の最初の並列作業は成立した。次の主工程はW3。作成済みCore/Adapter/fixtures/backend locatorを再実装しない。

## W1 — YMM4 Target / Projection Spine

**Purpose:** explicit Target Setからdummy/real-feature candidateを正しいTimeline occurrenceへJumpする。

**Implemented baseline:** public Timeline/FPS/selectionとsession-local identity、immutable snapshot、atomic capture、stale rejection、version-pinned PlaybackRateMap boundary、source-range union、独立occurrence、半開区間、integer seek、日本語ToolとPrev/Next/List。

**Adopted host facts:** PlaybackRate2、offsetを倍率へ含めないnative map、50/100/200%、inverse end exclusion。ContentLengthはconsumed durationへ使わない。Lab索引を参照し再発見しない。

**Remaining:** broader project reload/scene switch/Undo lifecycle、背景処理中の切替、decoded frame/physical input。基本W1 PASSとは別に追う。

## W2 — Shared Feature Engine

**Purpose:** RuntimeとLearningが同じsemantic前のprimitive/schemaを使う。

**Implemented:** FFmpeg子process CPU経路、generated media、visual/audio primitives、empirical salience、versioned FeaturePack、検証付きPackStore、欠損featureの非互換判定。Plugin runtimeはYMM4 public `FFmpegResourceLocator`からhost同梱backendを取得し、独自FFmpeg copy/PATH探索を持たない。Coreはexplicit path inputのままYMM4非依存を維持する。

**Acceptance retained:** serialize/reload後の値/schema/time、同じevaluator入力、backend failure/cancelの分離、サイズ/速度実測可能、bundled ffmpeg/ffprobe exact pathとprivate-copy不在をnativeで確認。

**Remaining:** 長尺/multiple-source実測、容量/メモリ最適化、GPU capability/resize/fallback、他codec/VFR、normalization比較。YMM4版更新時はhost locator/sibling ffprobeを再検証する。

**Budget:** 数学/serializationはLinux pure tests。GPU/codec/host integrationだけ対応するcheckpointでnative。

## W3 — Learning Corpus Intake — NEXT

**Purpose:** 人間分類FolderをbatchでPackと分類membershipへ取り込み、元動画なしでCorpusを再利用する。

- folder/group/profile preview、一括取込;
- fingerprint/dedupe、複数positive membership、provenance;
- Pack persist/reload検証 + sample登録commit;
- incompatible schemaとpartial/cancel/errorを区別;
- 非破壊Importを先に成立;
- consumptive Inboxは所有権確認と確定後削除を別transaction/negative testsで実装。

**Exit:** 少数X4分類Folderを一括取込、元動画を外してCorpus reload/replay、重複取込安全、削除ONでも未確定/失敗source保持、毎動画metadata入力なし。

**Budget:** pure/integration中心。Folder処理や学習だけの変更でnative YMM4を起動しない。

## W4 — Multi-Profile Runtime Review

**Implemented baseline:** Profile evaluator、per-Profile hit、OR/Union、overlap merge/attribution、unique candidate vs hit total、感度変更とON/OFFのno-redecode、Timeline順Prev/Next/List。query中止を古い成功値/0件へ偽装しない。

**Acceptance retained:** 合成fixture30+10=40hits、重複時uniqueだけ減る、別Item occurrence維持、host Jump成立。

**Remaining:** Learningで作成したProfileを接続、Review Preset/設定の永続化、実長尺query latency、広いreview lifecycle/UX。汎用seedを学習済みゲームDetectorと見なさない。

## W5 — Reverse Classification / Hard Example

**Purpose:** human-labeled sampleをCURRENT frozen Profilesで評価し、説明不足の教材を優先。

- expected-profile gap、他Profileへの一致、novelty、invalid/low-informationの区別;
- Version/revision付きranking;
- 一致度を確率/正答率として表示しない;
- 自動relabelling禁止。

**Exit:** 期待Profileで拾えないsampleを再現可能に抽出、human label保持、評価したrevisionが追える。multi-labelの正当な同時一致だけを誤分類にしない。

**Budget:** pure Corpus/Profile tests。

## W6 — Profile Refinement / Regression

**Purpose:** Commonality/Contrast/Hard Exampleから改善候補を作り、明示反映。

- 他Profile corpusをhard-negativeとみなさないContrast;
- 候補生成、Corpus replay、既存positive回帰/過剰hit/query costの検査;
- preview、explicit apply、新revision、rollback。

**Exit:** Hard Example coverage改善、既存positiveへ大きな回帰なし、同時一致を壊さずhit explosionを検出、silent updateなし、直前版へ戻せる。Corpus上の回帰確認と未見録画上の性能評価は分ける。

## W7 — First Value / Distribution

**Purpose:** 実YMM4編集環境の価値と通常配布を成立させる。

**Backend decision:** NavigatorはFFmpeg binaryを配布しない。supported YMM4が同梱するbackendをpublic locator経由で使い、版更新時に再検証する。

**Exit retained:** X4 Profile複数巡回、raw-video-free learning、background/progress/cancel、supported GPU + software fallback、安定した `.ymme` 内部root、独立release evidence validator、通常導入/upgrade、ユーザーacceptance。

Functional/native、UIUX、package/install/upgrade、human acceptanceを別Claimとして確認。現行DLL artifactは一般配布版ではない。

Goal到達後、他GenreやHeavy Detectorへ自動拡張しない。

## Next execution

[IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) に従いW3を追加し、その後W5/W6を接続。host未知事実だけをLabへ戻す。実録画やCorpus、host runtime binaryはrepoへcommitしない。
