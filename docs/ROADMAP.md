# Implementation Roadmap — v0.4.2

Authority: [DESIGN.md](DESIGN.md) §14。検証済みのsource/run、境界は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Current state

**UI/UX FOUNDATION CANDIDATE — PR #9 / NOT UX-ACCEPTED OR RELEASED**

- PR #9のUI/UX foundationを実装。exact source `42263615642dc94907fbefc7471fc3015a1512ce` / run `35675127362` でpureと製品native105 PASS。詳細は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。main未merge、物理操作Hands-on/interaction freezeは未完了。
- W1/W2/W4の既存基盤を維持。
- W3-A非破壊batch取込、W3-B元動画なしでの再利用、W3-C局所Transition抽出を実装。
- W5の初期Filter生成、複数Pattern OR、教材再判定、試用/保存、Runtime/感度接続まで実装。
- W6は最小のApply/revision/Positive回帰/rollbackが先行実装済み。Explicit NegativeとContrast/densityはまだOPEN。
- 2026-09-19のLabでSplit/Trim/Move/Duplicate/UndoRedoとmemo Scene shelfのhost挙動を確定。W1-Rの実装・検証は [W1_R_CHECKPOINT.md](W1_R_CHECKPOINT.md) を参照。**次は実装したUI/UX foundationを既存FilterでHands-onし、その後に最小Generic Filter Packでpressure-testする**。W4-M/W6は取り消さず、revised Review UXへ統合して続ける。
- W7実素材精度・長尺性能・GPU・配布・ユーザー受入はOPEN。

最新の正確なsource/run別証拠は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。元のW1-R専用証拠は [W1_R_CHECKPOINT.md](W1_R_CHECKPOINT.md)。Baseline27、Learning35×2条件と専用Pure/製品nativeを区別する。教材候補一致を実X4のRecall合格へ拡大しない。

## Fixed product direction

主目的は場面の切り替わりを高速に確認し、見逃しを減らすこと。教材の前後余白も状態変化の手がかりに使う。1 Filterは複数PatternをORで持ち、他Filterと非排他的に併用する。

教材分類はPositive membership。現Filterで拾えるものは再発見の優先度を下げるが、回帰確認から外さない。他Folder/未所属をHard Negativeへ変えない。NegativeのAuthorityは人間が実際の誤検出を指定したものに限る。

Global SensitivityはMain Reviewで変更しやすく保つ。再Decode/再学習なし。録画アーカイブは任意の教材準備ツールであり、Navigatorへ入れる素材を専用形式へ限定しない。

編集後ReviewではSource identity / analyzed source range / Candidate AnchorSourceTimeをAuthorityにし、current Timelineへ都度rebindする。Split/Trim/Move/UndoRedoだけでFeature Indexを捨てない。copy/pasteで同一source rangeが複数存在できるため、occurrence lineageなしの任意jumpは禁止する。

`見どころを確保` は完成尺抽出ではなくmemo shelf。Anchorからdefault30秒（設定可）、pre-rollなし、専用 `見どころメモ` SceneのFrame0へ1Layer1本で置く。元Review Sceneは切り替えない。

## W1 — Target / Projection — basic implemented

session-local target identity、immutable source session、atomic capture、source-mutation stale rejection、PlaybackRateMap、同じSourceの別occurrence、integer seek、日本語Toolを維持する。

YMM4の版番号だけで拒否しない。必要な依存先が使えなければ該当操作の失敗として扱う。検証版固定とruntime互換性は別。

残りは実Project reload/scene switch/Undoや解析中の終了など広いlifecycle、physical input、decoded-frame correspondence。

## W1-R — Edit-time rebinding — implemented checkpoint

Labで確定したhost facts:

- Splitは元VideoItemを左右の新objectへ置換する。
- head/tail Trimはsame objectを変更する。
- Moveはsame objectのFrameだけを変えsource rangeを維持する。
- Undo/Redoはsemantic stateが戻り、observed cycleでは過去referenceも再導入し得る。
- Copy/Pasteは同じFilePath + source rangeの別occurrenceを作れる。

実装済みの方針:

- 現TargetAdapterのfrozen snapshot完全一致による全Session staleをやめ、ReviewSourceSession + occurrence lineageへ分離。
- source file fingerprint/stampのstaleは維持。
- CandidateへAnchorSourceTimeを追加。Transition Filterは実match centerを使う。
- Prev/Nextはcapture時Target順 + AnchorSourceTimeの安定順。current Frameは都度projection。
- known lineage referenceがcurrent Timelineに残る場合はそれを優先。
- Split replacementは直前bindingのsource/timeline partitionと一致するunambiguous replacementだけ採用。
- Trim/DeleteでAnchorを含まなくなったcandidateは個別Unavailable/skip。
- duplicate ambiguityはfail closed。別copyへ勝手に移動しない。
- positive constant PlaybackRateのcurrent mapで再投影する。unsupported rateは該当projectionだけ使用不可。

Pure → 製品nativeの同一source gateを実装。結果とunsupported境界は [W1_R_CHECKPOINT.md](W1_R_CHECKPOINT.md) を参照。

## W2 — Shared Feature Engine — CPU implemented

FeaturePack/PackStore/FFmpegBackend/FeatureTableをRuntimeと教材で共有する。YMM4同梱backendを遅延解決し、CoreはYMM4非依存。無音と音声なし、schema欠損と0を混同しない。

stream-copy素材の映像/音声末尾の差に対応し、実際の映像domainをPackに残す。詳細は [FEATURE_FORMAT.md](FEATURE_FORMAT.md)。

長尺/multiple-sourceの性能・メモリ、別codec/VFR/特殊timestamp、GPU resize/fallbackはまだ要評価。現在の短いfixture成功から性能目標達成を推定しない。

## W3 — Learning Corpus + Transition Extraction — initial implemented

### W3-A — Non-destructive intake

普通の動画、選択フォルダー直下の動画を一括取込。group/filter名のpreview、source fingerprint、dedupe、複数Positive membership、Pack→reload→catalog commit、partial/error/cancelを実装。

元動画は変更・削除しない。消費型Inboxは後段の独立した所有権/削除transactionであり、この完了範囲には含めない。

### W3-B — Raw-video-free replay

元教材を外してPackを再読込し、同じprimitive/TransitionIndexを再構築できる。corrupt/incompatible Packを黙って飛ばして成功率を上げない。

### W3-C — Transition proposals

局所before/transition/afterを生成。複数候補、source time、clip先頭の偽切替防止、余白長が違っても同じ局所patternになることを検証した。

初期はvisual signature中心。audioは保持されるが学習matcherへ未接続。現候補floor/時間窓/特徴重みのゲーム品質はまだ評価対象。

## W4 — Multi-Filter Review — learned path connected

汎用条件と学習Filterを同じ候補投影へ流す。per-filter hit、OR/Union、attribution、別Item occurrence、capture時Target順 + AnchorSourceTime順のPrev/Next/Listを維持する。

学習Filterの試用はsessionだけ。保存版は次回ロードで使える。Global Sensitivityは常時見える位置に置き、同じTransitionIndexを再検索する。Review Presetに相当する確認セットの明示保存・複製・上書き・1段復元はPR #9で実装。未保存working state/Draftの再起動復元や広いlifecycleは残る。

## W4-M — Highlight memo capture — planned after UI foundation / not implemented

目的は、使えそうなCandidateを本編Timelineから手作業で切る代わりに、開始地点だけをreference Clipとして別Sceneへ確保すること。

初版仕様:

- button: `見どころを確保`
- selected CandidateのAnchorSourceTimeから開始。review pre-rollは使わない。
- 長さはdefault30秒（ユーザー設定可）。素材末尾で短縮。
- Scene名 `見どころメモ`。0件なら作成、1件なら再利用、同名複数ならfail closed。
- active Review Sceneを保持したままnon-active memo Timelineへ追加。
- Frame=0固定。
- memo Scene内でitemが存在しない最小positive Layerを使い、1 memo = 1 Layer。
- Remark=`見どころナビ｜<Filter names>`。
- same source+anchorのNavigator memoは二重追加しない。
- 元Review Item / source mediaを変更しない。FFmpeg cut/re-encodeなし。

Plugin側ではHost-private MainModel lookupを `MemoSceneAdapter` のような狭い境界へ隔離し、CoreへYMM4 objectを持ち込まない。

製品native checkpointではMain active保持、Layer1/2/3、Frame0、30s/別尺、Remark、save/reload、source終端shortening、duplicate memo guardを確認する。
## W5 — Initial Filter authoring / coverage — implemented checkpoint

### W5-A/B — Candidate and multiple patterns

観測されたTransition signatureを代表候補として選び、distinct clip supportを見て複数PatternをORにする。既存Patternを保ち、未検出教材を優先する。全教材を1平均へ潰さない。

現行の探索上限や固定係数は最初の実装値。Pattern数の上限へ達しても、残りの教材を拾えたことにしない。

### W5-C — Replay

基準感度1で全Positiveを再評価する。UIは「候補あり（位置未確認）／未検出・重点教材／切替候補なし」。clip-level一致と狙ったTransitionの成功を区別する。既存Patternで拾える教材も回帰に残す。

### W5-D — Trial / save / runtime

`教材動画 → 候補作成 → 全教材再判定 → 長尺で試用 → 明示保存 → 感度変更 → Prev/Next` の経路は実YMM4で動いた。生成mediaの機能合格であって、実録画に対する製品価値の最終合格ではない。

## W6 — Explicit Negative / Contrast / quality gate — AFTER W1-R / W4-M

### W6-A — Human false-positive feedback

Runtime候補へ「これは違う」を追加。その前後Featureと、対象Filter・revision・center/source rangeを保存する。動画再切出しを必須にしない。統合候補の別Filterへ誤ってNegativeを波及させない。取消/修正可能なラベル関係にする。

### W6-B — Refinement

未検出Positiveを拾う改善を優先し、Explicit Negativeとの局所差を用いて誤検出を減らす。Negative signatureを無条件vetoとして差し引かず、Positiveも落とす提案は採用しない。既に全教材が拾える場合の変更なし判定もここで整理する。

### W6-C — Regression / review cost

全PositiveとExplicit Negativeを固定条件で再評価。以前のPositive coverageを守り、Negative hit、長尺candidate density、review time/query costの変化を比較する。たくさん出せば成功という学習へしない。

独立した動画/セッションと確認された期待地点を用いた評価を別に持つ。教材自身へのreplayだけを汎化性能や意味的Recallと呼ばない。全教材への細かい手動指定は通常UXに要求しない。

### W6-D — Revision lifecycle

最小のPreview/Apply/revision/rollbackとstale draft拒否は実装済み。次はNegative/Coverageの整合したsnapshotと密度比較を、この経路へ追加する。保存機構を新規に二重実装しない。

## W7 — Real use / performance / distribution — OPEN

実X4のRecall・candidate density・確認時間、3h録画の解析/メモリ/query latency、Pattern増加のコスト、感度の実用性、GPU/software fallback、`.ymme`導入/upgrade、host更新互換性とuser acceptanceを確認する。

通常配布はまだ行っていない。NavigatorはFFmpegを独自配布せず、host bundleを使う。

## Planned UI/UX-first sequencing

Before expanding the generic filter catalog, perform the UI/UX structure pass recorded in [UI_UX_GENERIC_FILTER_PLAN.md](UI_UX_GENERIC_FILTER_PLAN.md).

Preferred sequence:

1. define/freeze **Filter / Group / Set** semantics, with user-facing **確認セット**, and keep the task order target → review settings → analysis → review;
2. implement work-preservation/prevention rules for authoring Drafts and review working state;
3. validate the revised flow with existing capabilities, including keyboard-accessible high-frequency navigation;
4. add only a **2–3 filter minimal Generic Filter Pack**;
5. pressure-test the UI with multiple active filters and attribution;
6. freeze the interaction model;
7. only then expand Generic Filters and later Audio / Reference Image-State search.

The exact NOW/LATER implementation boundary is recorded in [UI_UX_GENERIC_FILTER_PLAN.md](UI_UX_GENERIC_FILTER_PLAN.md); favorites, rich thumbnails, configurable shortcuts and visual polish are explicitly deferred unless hands-on testing proves they block the core workflow.

This is a sequencing change, not a cancellation of W4-M/W6. Highlight Memo and Explicit Negative remain planned capabilities and should be surfaced through the revised review UX rather than driving a separate UI structure.

## Next execution and budget

次着手は [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) の **PR #9のUX-2/UX-2.5 Hands-on**。UX-1/UX-1.5の保存・保全基盤とUIの製品統合は検証済みだが、UX受入とは区別する。その後、最小Generic Filter PackでUXをpressure-testしてinteraction modelをfreezeする。W4-M / W6-Aは取り消さず、revised Review UXへ統合して継続する。W3/W5を作り直さない。必要に応じて少数の独立した実教材で初期Filterの弱点も確認するが、未提供データを持っている前提では進めない。

Anchor/queue identity/lineage判定のpure部分、Corpus/Transition/Contrastはcheap tests中心。host/UIに変更がある製品checkpointでのみnativeを実行する。hostの未知事実だけLabへ戻し、既存のArchive実験を変更しない。private素材/Corpusやhost binaryをrepoへcommitしない。
