# Implementation Roadmap — v0.4

Authority: `docs/DESIGN.md` §17を実装向けに展開したもの。GoalやMaterial Decisionを変更するRoadmapではない。

## Current state

**BOOTSTRAP READY / IMPLEMENTATION NOT STARTED**

- v0.4.0 design authority: ready
- repository/lab boundary: defined
- native validation policy: defined
- product code: not started
- active product native workflow: intentionally not created yet（W1 integration codeが無い状態でActionを回さないため）

## Dependency overview

```text
W1 Target / Projection Spine ─────┐
                                 ├─> W4 Multi-Profile Runtime Review ─┐
W2 Shared Feature Engine ─┬─> W3 Learning Corpus Intake ─> W5 Reverse Classification ─┤
                          │                                             ↓              │
                          └──────────────────────────────────────> W6 Profile Refinement ┘
                                                                 ↓
                                                           W7 First Value / Distribution
```

W1のLab確認とW2のpure implementationは並行可能。

---

## W1 — YMM4 Target / Projection Spine

**Purpose:** YMM4上の対象Video ItemとSource timeをsnapshotし、dummy candidateを正しいTimeline occurrenceへJumpできる最小spineを作る。

**Lab first questions:**

- selected/target VideoItem identityを安全にsnapshotするsurface;
- `FilePath / ContentOffset / Length / PlaybackRate / ItemStartFrame / Timeline FPS` のcurrent semantics;
- trim / nonzero start / positive constant PlaybackRateのrounding;
- Tool PluginからTimeline seek/jumpする最小surface;
- public/private/reflection boundary。

既存Lab evidenceで足りるClaimは再利用し、不足ClaimだけProbeする。

**Product implementation:**

- narrow YMM4 Target Adapter;
- immutable target snapshot;
- Source Range Planner skeleton;
- dummy Source Episode -> Item occurrence projection;
- Review Navigator minimal Prev/Next/Jump spine。

**Exit:**

- same source / multiple occurrenceを含むfixtureでsource-time -> timeline mappingが再現可能;
- trim / nonzero start / supported positive PlaybackRateでJump一致;
- Selection変更で既存Target Setが黙って変わらない;
- private/reflection dependencyがAdapter外へ漏れない。

**Action budget:** Labでhost factを確定してから、Navigator側native smokeはExit claimだけを確認する。

---

## W2 — Shared Feature Engine

**Purpose:** Runtime長尺録画とLearning clipの両方が同じprimitive Feature schemaを生成し、同じProfile evaluatorへ入る基盤を作る。

**Start pure:**

- FFmpeg child process abstraction;
- deterministic media fixture generator;
- visual/audio primitive extraction;
- source-adaptive normalization;
- schema/version model;
- Session Feature Index writer/reader;
- Learning Feature Pack writer/reader。

**Initial probe defaults:** `docs/DESIGN.md` §11。数値は固定RequirementではなくProbe default。

**Exit:**

- Runtime IndexとLearning Packから同じProfile evaluatorへ入力できる;
- Pack reload後もprimitive values/metadata/schemaが一致;
- cancel/backend failureがhost processを巻き込まない設計境界がある;
- performance/size measurementを取れる。

**Action budget:** Feature math/serializationはpure tests。GPU/codec/backendとYMM4 background integrationだけ必要時native。

---

## W3 — Learning Corpus Intake

**Purpose:** 人間分類FolderをbatchでPack化し、元動画なしでpersistent Corpusを再利用できる状態にする。

**Implementation:**

- folder/group/profile intake preview;
- batch import;
- positive membership relation;
- sample fingerprint / dedupe;
- Feature Pack persist + reload validation;
- consumptive Inbox transaction boundary;
- source deletion only after verified commit;
- Partial/Error/Cancel preservation;
- schema compatibility reporting。

**Exit:**

- `X4/戦闘` 等の少数Folderを一括Importできる;
- source videoを外してもCorpus reload/replay可能;
- duplicate importが安全;
- deletion ONでもfailed/uncommitted sourceは残る;
- per-video metadata入力を要求しない。

**Action budget:** 原則pure/integration tests。YMM4 host不要。

---

## W4 — Multi-Profile Runtime Review

**Purpose:** 同じSession Feature Indexへ複数Profileを独立適用し、HitをUnionして重複だけまとめ、YMM4上で高速巡回する。

**Implementation:**

- Profile evaluator;
- per-Profile hit sets;
- OR/Union;
- overlap/nearby merge;
- attribution preservation;
- unique candidate count vs hit total;
- Global Sensitivity;
- no-redecode requery;
- Timeline projection / Prev / Next / List。

**Exit:**

- 例 `戦闘30 + ステーション10` のraw hit total 40を保持;
- overlap時はReview candidate数だけ減り、attributionは残る;
- Profile ON/OFF / sensitivity変更で再Decodeしない;
- Timeline順ReviewとJumpがnative hostで成立。

**Action budget:** Union/Merge/Profile evaluatorはpure。YMM4 Jump/UI integrationのみproduct native smoke。

---

## W5 — Reverse Classification / Hard Example

**Purpose:** Human-labeled sampleをCURRENT frozen Profilesで評価し、現Profileが説明できない教材を優先できるようにする。

**Implementation:**

- frozen Profile revision evaluation;
- expected-profile gap;
- competing-profile match;
- novelty vs existing positive Corpus;
- invalid/low-information penalty;
- Hard Example ranking;
- score UI semantics = `一致度` / `近いProfile`、確率表示禁止。

**Exit:**

- Human label `戦闘` なのにcurrent戦闘Profileで弱いsampleを再現可能に抽出;
- Human labelは自動変更されない;
- ranking input/outputがVersion/revisionと結びつく。

**Action budget:** pure Corpus/Profile tests only。

---

## W6 — Profile Refinement / Regression

**Purpose:** Commonality + Contrast + Hard Exampleから改善Candidateを作り、既存Corpusを壊さず新revisionへ反映する。

**Implementation:**

- commonality ranking;
- cross-profile contrast without hard-negative assumption;
- candidate Profile update;
- full Corpus replay;
- regression/hit-explosion/query-cost checks;
- preview;
- explicit apply;
- revision history / immediate rollback。

**Exit:**

- Hard Example coverageが改善;
- existing positive coverageへMaterial regressionなし;
- cross-profile hit explosionを可視化/guard;
- silent updateなし;
- previous revisionへ戻せる。

**Action budget:** pure tests。Profile algorithm変更だけでnative YMM4 Actionを回さない。

---

## W7 — First Value / Distribution

**Purpose:** 実際のYMM4編集環境でFirst Valueを通し、配布可能なPlugin Candidateにする。

**Acceptance layers:**

1. product functional integration;
2. UI/UX acceptance;
3. package/install/upgrade proof;
4. real-user hands-on acceptance。

**Exit:**

- X4 Profile Groupで複数Profileを高速巡回;
- raw-video-free Learning Corpusを維持;
- background analysis / progress / cancel;
- supported環境でGPU path + software fallback;
- stable `.ymme` install root / compact distribution;
- release evidenceを独立consumerで検証;
- 実YMM4で通常導入して使える。

Goal到達後、他GenreやHeavy Detectorへ自動拡張しない。

---

## First implementation target

次に着手する標準順は:

1. W1に必要なYMM4 host Claimを `LAB_REFERENCES.md` で棚卸し;
2. 不足ClaimだけLabへ最小Probe;
3. 同時にW2のYMM4非依存Feature schema / fixture skeletonを開始;
4. W1 native Exitが通った時点でNavigator product native workflowを追加。
