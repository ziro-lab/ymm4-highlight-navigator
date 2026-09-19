# Implementation checkpoint — W3/W5 learning baseline + v0.4.2 implementation prep

設計正本は **v0.4.1 Transition Filter Learning / Recall-First Review**。この文書は実装・検証済みの範囲を記録する。初期学習経路が動いたことと、全機能完成・一般配布・実ゲーム品質は別のClaim。

## Current state

| 領域 | 実装と検証の範囲 |
|---|---|
| W1 / W2 / W4 | 既存Target/Projection、CPU特徴、Pack、複数フィルターReviewを維持 |
| W3-A | 普通の動画/直下フォルダーの非破壊batch取込、重複検出、Positive membership、登録transactionを実装 |
| W3-B | 元動画なしでPack/Corpusを再読込し、同じ特徴・Transitionを再構築 |
| W3-C | before/transition/afterの局所変化候補を実装。初期はvisual中心 |
| W5 | 複数教材から候補生成、複数Pattern OR、基準感度での教材再判定、試用/保存、Runtime/感度へ接続 |
| W6の最小基盤 | 明示Apply、既存教材への回帰確認、immutable revision、rollbackを実装 |
| W6残り | Explicit Negativeの登録・Contrast改善・candidate-density評価は未実装 |
| W7 | 実X4/独立素材の精度、レビュー時間、長尺性能、GPU、通常配布・user acceptanceはOPEN |

## End-to-end implementation

メイン画面の「動画からフィルターを作る」で実際の学習Windowを開き、教材の選択・取込・候補作成・試用・保存を行う。元動画は変更・削除しない。入力を取り込んだだけではActive Filterを変更しない。

Corpus正本は `%LOCALAPPDATA%\Ymm4HighlightNavigator\Learning` のcatalog、primitive Pack、分類membership。動画のフルパスを永続identityにはせず、source hash・extractor・sampling・実際のsource rangeからsample keyを作る。同じ教材を再取込して二重学習しない。別のPositive分類は既存Packへのmembership追加で扱う。

取込はPack検証→保存→reload→catalog publicationを分け、writer lockで通常の同時書込を拒否する。完了済みファイルは保持し、後続の失敗/中止は別結果になる。既存catalogやPackの破損を「教材0件」として上書きしない。これは悪意ある外部writerや任意のfilesystem/power failureへの完全保証ではない。

学習の初期方式は、局所2秒程度のbefore/afterと変化中心を用いる32次元visual signature。画面差分の立ち上がり、明暗・色・edge・histogram・gridの前後差、6つの時間binを保持し、観測された代表パターンを選ぶ。1本の平均へ全教材を押し込まない。音声の局所値も保持するが、現行の学習matcherはvisual-onlyであり音声必須ではない。

現在のパターンで一致する教材は新たなprototype探索で優先せず、未検出教材を先に扱う。既存Patternは保持し、候補を全教材へ再適用する。現行heuristicの上限は12patterns、探索候補は1clipあたり最大4・1iteration最大512。これは実装予算であり、12patternsで実ゲームの見逃しがなくなるという保証ではない。上限に達しても未検出を成功扱いにしない。Runtimeの走査自体にはclip単位のtop-K候補制限を使わない。

## Coverage / sensitivity semantics

UIは **候補あり（位置未確認）／未検出・重点教材／切替候補なし** を区別する。

- `CoveredCandidate`: 教材内に現Filterの一致があるという弱いclip-level判定。狙ったTransitionの位置が正しいという確認ではない。
- `HardPositive`: Transition候補はあるが、そのFilterでは拾えていない。
- `NoTransition`: 現在の特徴/提案条件で切替候補を得られない。Negativeにはしない。

同じ教材で生成・評価しているため、教材coverageを独立素材上のRecallと呼ばない。別の関係ない切替だけを拾ってもclip-level一致は成立し得る。意味的な見逃しの評価には、独立した素材と確認された期待地点を別に用意する。毎教材への手動timestampを通常操作の必須条件にはしない。

教材replayは常に基準感度1。通常Reviewのスライダーはその場の検索条件であり、教材の分類や保存Filterを変更しない。感度を上げるとraw match centersは包含的に増えるが、前後余白のUnionで候補区間同士がつながり、統合後の件数は必ずしも単調に増えない。

試用はrevision0のsession候補。保存時にCorpus revision/Active Filter revisionを再確認し、現Corpusを再計算する。以前一致していた教材を落とす候補は拒否。新revisionは別ファイルへ保存し、Active headを最後に更新する。完全なW6誤検出学習・密度gateを実装済みとは扱わない。

## Pure / media verification — current

- Tested source/checkout: `2d4a3a355d6f80dd866f2d651cab8430300039e7`
- [Learning run 35140556049](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35140556049)
- Baseline Core: **27 cases / 11,147 assertions / 0 failures**
- Learning Core: **35 distinct cases / 60 assertions / 0 failures** on ordinary generated media
- Same35 cases /60 assertions /0 failures on a stream-copy trimmed fixture
- Learning build: warnings as errors, **0 warnings / 0 errors**
- Artifact: `10464663192`
- Artifact ZIP SHA256: `4fd7325fa4fc0d70bc7a369d2205f205c98b2eef73fb59790794d6b4b098199f`

35ケースを2条件で繰り返したのであり、70種類の独立機能テストではない。Baselineの11,000 assertionsは決定的な乱択interval coverageであり、11,147種類の機能を証明したという意味ではない。

Required-case一覧と結果/checkoutを独立gateで照合。通常素材に加え、映像6.000秒・audio/container6.016秒の切出し素材で取込/replayを実行し、catalogが映像6.000秒を記録することを別consumerで確認した。ZIP/hashとJSON結果も取得して確認した。

ケースにはdedupe、複数Positive、未所属非Negative、writer contention、破損/cancel保存保護、raw-free再生成、clip中央に依存しないTransition、定常素材の偽Transition防止、複数切替、source offset、局所表現の余白長非依存、複数Pattern OR、感度包含、stale draft、Positive回帰拒否、明示rollbackを含む。

## Product native verification — current

- Tested source/checkout: **`4ada8fad20a65a9cf48ad86795a281f36ba5c401`**
- Tested tree: `36b47a014368253fe193facac77560b1c892629f`
- [Native run 35141003784](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35141003784)
- Exact host: **YMM4 Lite4.56.1.0**, observed Timeline FPS60
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- **47 independently required assertions PASS**
- Product/proof builds: **0 warnings / 0 errors**
- Artifact: `10464748918`
- Artifact ZIP SHA256: `507f0d13a4e6df027fbc8c146845d67b7051a41fadf04a6eb1492df846bee673`
- Product DLL SHA256: `85cfe505a8cd4bc92d97c8d5f3d769278b0a5dc6716e56fe1d27e31f834de6fa`
- Core DLL SHA256: `afcffc822333318d4c549bf1fc45b901b333076f30246a294ce111ab5aee65ee`

4ada8faは直前の成功source2d4a3a3からnative感度assertionのみを強化したcommit。Production/Core実装は同じ。後続が文書のみの場合も、証拠のtested checkoutを現在のdocs headへ読み替えない。

実際にhostが作成したNavigatorから学習Windowを開き、生成動画2本を同梱FFmpegで取り込んだ。2samplesから1patternを生成し、試用は未保存、明示saveでrevision1になることを確認。テスト所有の教材コピーだけを削除した後も再生成・再読込・保存FilterのRuntime使用が成立した。

テスト用の一時YMM4でffmpeg.exeを一時退避し、decoderを使えない状態で再検索した。学習FilterのみONにして **感度0.25で候補0件、感度2で候補2件、基準感度1で候補2件**。同じ1個の解析範囲を再利用し、別々の2つのItem occurrenceへ移動できた。ユーザーのYMM4やFFmpegを変更するテストではない。

47項目は既存27項目と学習統合20項目。source/Item状態保持、selected target固定、50%/200%、exclusive end、stale拒否、背景処理、no-redecodeを維持。学習のidle/busy操作、非破壊取込、候補生成、試用/保存分離、raw-free再利用、異常動画取込の局所失敗、Window終了後のReview継続を含む。runnerは結果の全ID、checkout、exact distribution DLL、host backend paths、YMM4 process生存を独立確認する。

### UI / quality boundary

Actual compiled Viewの **学習640x640・400x640 / メイン360x480** を描画し、主要controlの座標・サイズが領域内であることを検査。生成PNG3枚を取得して目視確認した。これらはcontrol-only renderingであり、物理マウス/キーボード・file picker操作・全DPI/theme・通常インストール・ユーザー受入の代用ではない。

短いfixtureでUI heartbeatは9回進んだ。3時間実録画の応答性能や意味的な検出品質は未測定。2教材は同じ生成素材の派生であり、独立したX4テストデータではない。

## Failure history and fixes

- `35137993044`: 初回pure learningではInvalidDataExceptionを想定したtest helperのcatchに不足があり2ケースがFAIL。既存Core27はPASS。helperとbatchの明示的InvalidDataException分類を修正した。
- `35139354911`: 通常素材のLearning35 + Baseline27 PASS。
- `35139355007` / `35139716499`: native learning取込でstream-copy素材だけが `Incomplete video coverage`。詳細結果では1ファイル成功・1ファイル失敗。成功扱いにはせず診断した。
- 原因はcontainer/audioの6.016秒を映像の6.000秒にも要求していたこと。video stream duration / Matroska DURATIONから映像終端を使うようCoreを修正し、存在しない末尾映像を水増ししなかった。fixtureを簡単な別素材に交換して逃げていない。
- `35140556049` / `35140556148`: 修正後、pure2条件とnative47 PASS。
- `35141003784`: 感度を変えた結果が実際に0→2件になるassertionまで強化して47 PASS。

## Earlier adopted checkpoints

- Core PR#1 / run `35122141470` / source `9c59979ed5ffc3b9e54d72042b5259d94816deca`: original27-case foundation。
- Native run `35126507934`: original24-case projection/review baseline。
- Bundled backend run `35129626271` / source `61126ef4be5b630118ae574823c9ba26d7c07f51`: public locator integration27 PASS。
- Capability guard run `35131453875` / source `a74d04e5a96c9cc85ad23af986f65bc90635c3d3`: exact-version refusalを外して27 PASS。
- Original Tool startup/readonly Progress binding failures and their fixes remain in preceding commits/PRs; old failures are not relabeled PASS。

Lab timing `0b69aed70a3f8a84cc2ede539d794cb4e8108677`、navigation-context source `3b52a3acff544ed0ec759d657f48364a415c485c`、FFmpeg locator evidence merge `c1acd43297f9a1c2dd5053e7c9667fb84fa237b5` を再利用した。Labのcallback receiptはvisible UIの証明ではないという訂正も維持する。今回Lab/録画アーカイブのコード・Probeは変更していない。詳細は [LAB_REFERENCES.md](LAB_REFERENCES.md)。

## 2026-09-19 host-prep checkpoint — decided / not yet implemented

今回の追加仕様は**まだNavigator製品コードへ未実装**。Labで必要host factsだけ先に確定した。

- Split lifecycle: run `35359881285` / 23 required assertions PASS。
- Trim / Move / Duplicate / UndoRedo lifecycle: run `35424106866` / 25 required assertions PASS。
- Highlight memo Scene shelf: run `35430972362` / 37 required assertions PASS。documentation headもrun `35431074898`で同contract再確認。

採用した製品方針:

- Timeline-only editでFeature Indexを捨てず、source-time Candidateをcurrent Timelineへrebindする。
- CandidateへReview Rangeとは別の `AnchorSourceTime` を持たせる。
- copy/paste duplicateへ備え、source+timeだけで任意occurrenceを選ばない。
- `見どころを確保` はAnchor開始・pre-rollなし・default30秒（設定可）。
- 専用 `見どころメモ` SceneへFrame0固定、1 memo = 1 Layer。
- Remarkへhit Filter名を残す。
- memo作成のために元Review ItemをSplit/Trimせず、media cut/re-encodeもしない。

これらはLab host fact + product designの確定であり、Navigator integration PASSではない。

## Remaining work / resume

次は [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) の **W1-R → W4-M → W6-A**。W3/W5をゼロから作り直さない。

- **W1-R:** edit-time rebinding。split/trim/move/undo後も再Decodeなしで候補巡回し、duplicate ambiguityはfail closed。
- **W4-M:** `見どころを確保`。memo SceneへFrame0 / separate Layerでreference Clip追加、default30秒/設定可、Remark attribution。
- その後 **W6-A:** Runtimeの `これは違う` → filter-specific Explicit Negative →保存済み特徴によるContrast改善。
- 誤検出低減と既存Positive保護、独立素材のcandidate density/query cost評価。clip-level一致と期待Transition地点を区別した評価。
- 既に全教材を拾える場合の変更なし候補扱い、細かい学習UX/Review Preset、Profile増加時の性能改善。
- W1-R/W4-Mのproduct-native integration、broader reload/scene/cancel race、実codec/VFR/特殊timestamp、memory/ingest/query/GPU、通常 `.ymme` install/upgradeとhands-on acceptance。
- 消費型Inboxの明示所有権・削除transactionは未実装。現在の非破壊Importとは分けて実装・検証する。

現行はraw-video-freeの再利用が可能だが、元動画を削除する権限や機能を追加したわけではない。未知の将来ABIやprocess全体の障害まで無停止保証するものでもない。
