# Implementation continuation — Transition Filter learning

W1/W2/W4の基盤は既にあります。ゼロから作り直さず、[DESIGN.md](DESIGN.md) v0.4.1、[ROADMAP.md](ROADMAP.md)、[IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) と現在のbranch/PRを最初に確認してください。

## 今回の最重要Goal

**教材動画から場面切り替わりFilterを作り、長尺録画の見どころ候補を高速に巡回できるところまで最短でつなぐ。**

Primary metricはPrecisionではなく **Recall / 見逃し低減**。

```text
短尺教材
→ Feature Pack
→ Transition候補
→ Filter Candidate
→ Positive replay
→ Covered / Hard Positive
→ 長尺Runtime
→ Sensitivity調整
```

ここまでが最初の大きなFirst Value。

## 固定方針

1. **Archiveは前提ではない。** 録画アーカイブは教材動画を切り出す手間を減らすだけ。普通の動画/Folderを同じ入口で扱う。Archive専用API依存を作らない。
2. **Transition-first。** Clip全体の平均分類より、`before → transition → after` の時間方向変化を重視する。前後余白は比較材料として使える。
3. **Clip中央を正解にしない。** 1 Clipに複数Transition候補があってよい。
4. **1 Filter = 1 Patternに固定しない。** Hard Positiveを既存Patternへ平均するとRecallが落ちる場合は、Pattern A/B/CをORで追加する。
5. **Positive replayを必須にする。** 現Filterで拾える教材はCovered Positive、拾えない教材はHard Positive。Coveredは学習優先度を下げるがRegressionから消さない。
6. **Negativeは実際の誤検出だけをAuthorityにする。** 他Folder/他Profile/未所属をHard Negative化しない。
7. **SensitivityはMain ReviewのPrimary control。** 高感度=広く拾う、低感度=候補を絞る。変更時に再Decode/再学習しない。
8. **Recallを守りながら候補密度を下げる。** Candidate数が多すぎる場合はSensitivity/Filter改善で調整するが、Positive missを増やす修正は優先しない。
9. **No silent training。** Filter更新はReplay/Preview/Apply/revision/rollback。
10. **YMM4を巻き込まない。** version番号だけで拒否せず、依存変更時は該当機能だけfail closed。元動画/ymmpを通常Reviewで変更しない。

## Next implementation — W3-A Non-destructive Corpus Intake

既存 `FeaturePack` / `PackStore` / `FfmpegBackend` をそのまま再利用する。

実装:

- Folder単位batch import request;
- Group / Filter(Profile) preview;
- file enumeration with supported local media only;
- source fingerprint / sample id;
- dedupe;
- multiple Positive membership;
- provenance / original display name / import timestamp;
- Pack write + reload validation;
- sample registrationをPack commitと分離;
- Partial / Error / Cancelを成功にしない;
- first sliceでは入力動画を削除しない。

### W3-A acceptance

- 1 Folder内の複数clipを一括Importできる;
- 同じclipを再ImportしてもPack/学習weightを二重化しない;
- 同じsampleへ別Positive membershipを追加できる;
- Pack確定前にsample登録済み扱いしない;
- cancel/errorでsourceを変更・削除しない;
- user machine pathをCorpus identityの唯一Authorityにしない。

## W3-B Raw-video-free Replay

W3-AのPackだけでCorpusをreloadし、元動画を外した状態で:

- schema compatibility確認;
- FeatureTable再構築;
- Profile evaluatorへ再入力;
- provenance / memberships復元;
- missing required Featureを0扱いせずincompatible表示。

### W3-B acceptance

**元動画なしでFilter/Profile再評価へ必要なprimitiveを再利用できる。**

## W3-C Transition Candidate Extraction

最初はHeavy AIを使わず、既存primitiveの時系列からMaterialな変化点候補を作る。

初期候補Feature:

- Delta level / rise / acceleration;
- Histogram distance;
- Grid luma/deltaの広がり;
- Luma / contrast / extreme変化;
- Audio RMS/Peak burst;
- Before/Afterのstability差;
- sustained activity。

各candidateは最低限:

```text
TransitionCandidate
├─ center time
├─ score components
├─ before window
├─ transition window
├─ after window
└─ source sample id
```

を持つ。

ルール:

- clip中央固定禁止;
- 1 clip複数candidate可;
- gap/resetを跨ぐ偽deltaを作らない;
- short clip / missing audioでもcompatible Featureだけで候補生成可。ただしmissingを0として意味付けしない;
- exact peak formulaはpure testsで調整。

### W3-C synthetic fixtures

実録画投入前に、generated mediaで最低限:

```text
stable A → hard visual transition → stable B
stable A → audio burst + visual transition → active B
multiple transitions in one clip
no meaningful transition
missing audio
```

を作り、expected window近傍へ候補が出ることを確認する。

## W5を見据えたW3 data shape

W3で最終Filter optimizerを作らなくてよいが、後から以下を比較できるDataは残す。

- before/transition/after primitive distribution;
- candidate ranking components;
- cross-sample alignmentに必要なrelative time;
- sample Positive membership;
- schema/extractor version。

これによりW5で:

```text
Clip A ───▲────
Clip B ─────▲──
Clip C ───▲────
          ↑
 common transition tendency
```

を比較できる。

## W5 — 次のcheckpoint

W3が通ったら:

1. 同一Positive FilterのTransition候補をalignment;
2. common temporal tendenciesから初期Pattern生成;
3. Filter Candidateを全PositiveへReplay;
4. `Covered Positive / Hard Positive`を出す;
5. 必要ならHard PositiveからPatternを追加;
6. RuntimeへFilter revisionを登録;
7. Main Global Sensitivityでno-redecode調整。

**W5 Exit:** `教材数本 → Filter → Positive coverage → 長尺候補 → 感度調整 → Prev/Next`。

## W6 — その次

Runtime候補へ`これは違う`を追加し、誤検出前後のFeature WindowをExplicit Negative化する。

改善優先度:

```text
1. Hard Positive
2. Explicit Negative
3. Covered Positive
```

ただし全Positiveは毎revision Regressionへ参加する。

## UI guidance

Main Reviewは増やしすぎない。

```text
Filter ON/OFF
Global Sensitivity
candidate / hit count
Prev / Next / List
```

Learning surfaceのユーザー語:

- `動画からフィルターを作る`
- `拾えなかった教材`
- `これは違う`
- `フィルターを改善`

`Reverse Classification`、threshold、Feature名等は必要な詳細表示に留める。

## Repository / validation boundary

- CoreはYMM4非依存。
- Corpus / Transition extraction / Filter authoringはpure/integration tests中心。
- YMM4 native ActionsはFilterを実Tool Runtimeへ接続するW5 checkpointなど、host境界を変える時だけ。
- YMM4自身の未知挙動はLabへ最小問合せ。既知PlaybackRate/FFmpeg factsを再探索しない。
- Private recordings / Corpus / ymmpをrepoへcommitしない。
- Generated media / synthetic Feature fixturesを初期proofに使う。

## 最初に着手するとき

1. current main / open PRを確認;
2. Core regressionを実行;
3. Corpus data model / transactionを最小設計;
4. W3-A pure tests;
5. W3-B raw-free replay;
6. W3-C Transition candidate extractor + synthetic fixtures;
7. W3 exit docs更新。

W1/W2/W4を再実装・再設計しない。
