# Implementation checkpoint — v0.4.3 見たいもの Main Review candidate

設計正本は **DESIGN v0.4.2**。この文書は実装・検証済みの範囲を記録する。初期学習経路が動いたことと、全機能完成・一般配布・実ゲーム品質は別のClaim。

## 見たいもの / Filter management checkpoint — 2026-09-24 / PR #13

**Main Reviewを膨らませず、稀な整理・削除操作を専用surfaceへ分離した管理sliceが実YMM4 GREEN。**

- Branch: `feature/v0.4.3-management`; Draft PR #13。BaseはPR #12。
- Exact product/native tested source: `afb427e7ff7e383d8803f55a877163c5939b4853`。
- Run `35884088478`: Pure / product-native 両job PASS。
- 実YMM4 Lite **4.56.1.0** / .NET10: **123 independent IDs PASS**。
- Product / proof build: **0 warnings / 0 errors**。
- Native artifact: `10761518055` / SHA256 `023a161acc9288bd99df12fb74a5add1deb75ed2ad8c2e414807caea6f14ef97`。
- Pure artifact: `10761697260` / SHA256 `933459db6a6f08a608b07c896cfdf963d1ddf997c53bdcc1c659fc8ffac64b70`。
- Review: **34 cases PASS**。
- Learning: **36 cases PASS × ordinary + stream-copy**。

実装済み:

- `見たいものを整理` window:
  - built-in / user-created一覧
  - built-in / 既存から複製
  - user-created rename
  - 可変長classification path編集（例: `動画 > ゲーム`）
  - user-created delete
  - built-inはduplicate可、直接rename/delete不可
  - duplicate / rename / classification move / deleteでworking review configurationを破壊しない
- Main Reviewには `整理` entryのみ追加し、日常操作面を管理機能で膨らませない。
- `フィルターを整理`:
  - search
  - `すべて / 使用中 / 未使用`
  - 保存済み「見たいもの」のusage destinations
  - current working stateでのusage
  - presentation name / Filter Group編集
  - deleteは **未使用 + 自作 + 保存済みrevision** のみ
- learned Filter deleteはactive headだけを外し、Corpusとimmutable revision historyを保持。再作成時も既存revision fileを上書きしない。

Native追加required:

- `ux_view_intent_manager_layout`
- `ux_view_intent_duplicate_preserves_working`
- `ux_view_intent_rename_move_preserves_config`
- `ux_view_intent_delete_preserves_working`
- `ux_filter_usage_metadata`
- `ux_filter_usage_filtering`
- `ux_unused_learned_filter_safe_delete`

Retained failures:

- run `35882557512`: 新Learning case自身はPASSしたが、独立gate固定件数35→36未更新でfalse-negative。
- run `35882750436`: Pure GREEN後、FilterManager XAML GroupStyle閉じタグ欠落でProduct build FAIL。
- run `35883069468`: Product build GREEN、見たいもの管理native項目もPASSしたが、Filter管理Window経路でproof result publication前に終了。
- Filter管理のprojection/bindingを単純化して、required caseを削らずrun `35884088478`で123/123 PASS。

UI screenshot目視:

- `view-intent-manager-420x560.png`: 「何を探す構成か」を一覧で認識し、複製・名前/分類・削除の役割が読める。
- `filter-manager-380x540.png`: 使用状況とusage destinationsが先に見え、削除可能条件/理由も文脈内で確認できる。

未完了:

- Genre別built-in basics。
- 最小Generic Filter Pack。
- management UXの実ユーザーHands-on。
- favorites / recent / hiddenなどcatalog scale依存機能。

## 見たいもの Main Review checkpoint — 2026-09-23 / PR #12

**内部ReviewSet/Targetを維持したまま、ユーザー向けMain Reviewを「見たいもの」中心へ変更し、Pure + 実YMM4統合がPASS。管理surfaceの完成・Generic Filter Pack・Hands-onは後続。**

- Branch: `feature/v0.4.3-view-intent-main`; Draft PR #12。Baseはdesign PR #11。
- Exact product/native tested source: `3598261b42fc9f1444fba106e58ebac52f06906b`。後続test/docs HEADをtested product sourceへ読み替えない。
- Native + preflight run `35878382449`: 両job PASS。
- Product-native: 実YMM4 Lite **4.56.1.0** / .NET10、**116 independent IDs PASS**、Product/proof build **0 warnings / 0 errors**。
- Native artifact `10759282442` / SHA256 `e42ab021da8734efec90d77e58b23f04d29d9a7ca6902e327da2b2a4fca5a408`。
- Same-run Pure artifact `10759930348` / SHA256 `d6340b95a6b52f9bb6982ff5818bc4f84a2490efcb40b5702262c79cfa1d30de`。
- Product-tested Pure: Core **27 / 11,147 / 0**, Rebinding **29 / 91 / 0**, Review **30 cases PASS**, Learning **35 / 60 / 0 × ordinary + stream-copy**。
- 後続Pure-test-only HEAD `6acf42af11a2f8c8fe7b7aa67ded885d2f629095` / run `35878998086`: Review **31 cases PASS**。旧v0.4.2保存データにClassificationPathが無い場合を空pathとして読み込む `view-intent-legacy-no-classification-compatible` を追加してPASS。artifact `10759648542` / SHA256 `4cf06792d82de3279bb2a43b549a3af70c38969bb27348e83390b2c6ad3df978`。

実装済み:

- Main Reviewのユーザー向け主語を `確認セット` から **`見たいもの`** へ変更。内部 `ReviewSet` は維持。
- 内部 `Target` は解析対象の意味を維持し、見たいものとは別。
- ReviewSetへ可変長 `ClassificationPath` を追加。Main pickerは `汎用 > 基本` のようにpathを表示し、Genreを別Dropdownで必須にしない。
- Filter presentation Groupと見たいものclassificationを別保存。
- 見たいものpickerへFilter自動要約を表示。
- active Filter chipの×はworking stateでOFFにするだけ。asset削除ではない。
- `フィルター n/m` progressive disclosureから全Filterを即ON/OFF可能。
- working stateが保存済みと異なる時だけ `変更あり` と保存/別名保存を提示。
- Save Asは現在のClassificationPathを継承。
- target未取得時は他Review操作を隠し、`解析対象` Empty State + `選択動画を対象に` の次の1操作だけを表示。
- Filter ON/OFFをMainへ寄せ、別windowを `フィルターを整理` としてasset管理側へ寄せた。

UI native proof + screenshot目視:

- `ux-empty-360x360.png`: 初回の次操作が一意。
- `ux-review-360x480.png`: 解析対象 → 見たいもの → Filter → 感度 → 候補が収まる。
- `ux-review-filters-360x600.png`: quick Filter展開でも横隠れなし。
- `filter-manager-360x480.png`: ON/OFFはMain、管理は別という役割分離が読める。
- 新required: path/summary、modified state、classification継承save、quick Filter disclosure、Empty State。

Retained failure:

- run `35877992082` はPure PASS後、追加XAMLの `DataTemplate` 閉じタグ欠落でProduct build FAIL。ロジック/host failureではない。4箇所を修正し、required IDを削らずexact source `3598261b...` で再実行してPASS。失敗runを成功証拠へ読み替えない。

未完了:

- `見たいものを整理` のduplicate/rename/classification move/delete UI。
- Filterのused/unused、使用先一覧、duplicate/delete。
- Genre別built-in basicsと最小Generic Filter Pack。
- physical input / user Hands-on / interaction freeze。


## Portable Data checkpoint — 2026-09-23 / PR #10

**PR #9のUI/UX foundation上にPortable保存を追加し、Pure + 実YMM4製品統合がPASS。通常 .ymme配布・manual delete/reinstall・Hands-onは別Claim。**

- Branch: `feature/v0.4.2-portable-data`; Draft PR #10。Baseは `feature/v0.4.2-ux-foundation` / PR #9。
- Exact tested source: `ef21cca6db459a138f9390b906411c42425cc01c`。後続docs-only HEADをtested sourceへ読み替えない。
- Native + pure run `35864132936`: 両job PASS。
- Pure job `107191338941`: Core **27 cases / 11,147 assertions / 0 failures**、Rebinding **29 / 91 / 0**、Review settings **27 cases PASS**、Learning **35 cases / 60 assertions / 0 failures × 通常/stream-copyの2素材条件**。
- Product-native job `107191814610`: 実YMM4 Lite **4.56.1.0** / .NET 10、**111独立IDすべてPASS**（PR #9の105 + Portable 6）。Product/proof buildはwarnings-as-errorsで **0 warnings / 0 errors**。
- Native artifact `10752465378` / SHA256 `fdc9181f09c4901f414323f62bac52f1d287d23d366a7f45add2f94097eb4238`。
- Pure artifact `10751765755` / SHA256 `8125cdb7ed73794d1e06dc3bda8648aec32a25747e2c30b07d64da4ea84532e1`。

Navigator-owned user dataの正本をloaded Plugin root基準の `Data/` へ変更した。

```text
<YMM4>/user/plugin/Ymm4HighlightNavigator/Data/
├─ Learning/  # Corpus / Pack / learned Filter revisions
└─ Review/    # 確認セット / Filter表示情報
```

旧 `%LOCALAPPDATA%\Ymm4HighlightNavigator\Learning|Review` はPortable側が未作成の場合だけ初回移行元として扱う。移行前後で既存Storeを検証し、staging copyとSHA256 copy verification後にpublishする。旧データは削除せずbackupとして残す。Portable側が一度存在した後はlegacyをsilent fallbackに使わず、Portable破損を古いデータで隠さない。

製品nativeでは plugin-local root、Learning/Reviewのlegacy移行、Portable authority優先、Portable破損時no fallback、legacy破損時no publicationの6 required IDを追加してPASS。学習400×640とFilter管理360×480の既存narrow viewport proofにも保存パスと「保存先を開く」controlを含め、領域内であることを確認した。

最初のrun `35863689258` はPure全回帰と製品buildまでPASSした後、破損Portableを期待拒否するtest helperが `JsonException` を想定例外に含めておらずproof自身がFAILした。製品は意図どおり壊れたJSONを拒否していた。helperだけを修正し、required IDを削らず上記runで再実行してPASS。失敗runを成功証拠へ読み替えない。

YMM4側のupdate保持はLab PR #85 / run `35701097895`（YMM4 Lite 4.55.1.1）で、実 `.ymme` v1→v2更新後もpackageに含まれないplugin-local `Data/` ファイルが保持されることを観測済み。ただしNavigatorの通常package install/reinstall、将来全YMM4版、manual plugin削除は未証明。将来のpackage payloadへuser `Data/` を含めない。

自作Filter共有は今回未実装。ただし内部revision JSON / Corpusを公開交換形式に固定しない。将来はversionedな明示Export/Importとし、Runtimeに必要なPattern・algorithm/schema互換情報・表示metadataだけを持たせる。教材動画、Corpus、local revision history、Review sets、`SupportSampleIds`等の学習provenanceは既定で含めない。

## UI/UX foundation checkpoint — 2026-09-22 / PR #9

**基盤の製品統合はPASS。ユーザー操作受入・interaction freeze・通常配布はまだOPEN。**

- Branch: `feature/v0.4.2-ux-foundation`; [Draft PR #9](https://github.com/ziro-lab/ymm4-highlight-navigator/pull/9)。mainへ未merge。
- Base main: `dc2de6e2d037c895d2b16f0dd0b45e8c0e59ea0f`。
- Exact tested source: `42263615642dc94907fbefc7471fc3015a1512ce`。後続docs-only HEADをtested sourceへ読み替えない。
- [Native + pure preflight run 35675127362](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35675127362): 両job PASS。
- Pure job `106579915032`: Core **27 cases**、Rebinding **29 cases**、Review settings **27 cases**、Learning **35 cases × 2素材条件**、すべて0 failures。35×2は70種類の独立機能ではない。
- Product-native job `106580152574`: 実YMM4 Lite **4.56.1.0** / .NET 10、**105独立IDすべてPASS（既存69 + UX36）**。製品DLLとproof assemblyはwarnings-as-errorsで0 warnings / 0 errors。
- Runnerの独立gateで結果ID/checkout、実際にloadした製品DLL、host同梱FFmpeg/ffprobe、YMM4生存を照合。

### Implemented in this slice

対象固定 → 確認セット → 有効Filter → 感度 → 候補巡回というMain Reviewを実装した。セットの保存操作は折り畳み、Filter選択・検索・Group折り畳み・表示名編集は別surfaceに分離。候補理由、候補数/ヒット計、Progress/Cancel、既存のsource-time移動を維持する。

確認セットはFilter ID参照とON/OFF・Global Sensitivityだけを持つ。保存済みセットとworking stateは別で、新規保存（複製用途も含む）・明示上書き・切替前への1段復元を実装。標準セットは上書き不可。未解決参照は消さず警告し、保存先の破損/競合/失敗を空データとして上書きしない。

確認設定の正本はportable root `<YMM4>/user/plugin/Ymm4HighlightNavigator/Data/Review/review-settings.json`。独立したrevision/writer lockを持ち、既存のchecksummed JSON publication primitiveを再利用する。FilterStoreやCorpusの正本は複製しない。旧 `%LOCALAPPDATA%\Ymm4HighlightNavigator\Review` はPortable側が未作成の場合だけ検証付き初回移行元として扱う。

名前変更・Group変更は**表示名と整理用Groupのalias**としてFilter IDへ保存する。教材分類や既存Filterの保存keyを移動する処理ではない。セット参照と検出内容は変わらない。完全な教材分類のrename/migrationや学習Filter自体の複製・削除はこのsliceの完了範囲外。

学習Draftは再生成の中止/失敗、保存失敗、分類変更、作成Windowを閉じる操作で失わない。新しい生成結果の成功、保存成功、明示破棄を区別する。別分類/古いCorpus revision/古い保存版のDraftは保持しつつ試用・保存を止め、分類復帰または再生成へ案内する。保存後のUI反映失敗は、保存そのものの失敗とは区別する。rollbackは実際に前の保存版がある場合だけ有効。

試用Filterを通常の再読込で勝手に解除しない。明示的な「試用を終了」で保存版へ戻す。未保存の試用Filterがある間は、永続セットに不安定な参照を保存させない。

候補一覧にローカルな `Alt+↑ / Alt+↓ / Enter` のPrevious / Next / Jumpを接続した。グローバルキーフックや新規のYMM4-private依存は追加していない。

### Evidence chain

| Evidence | Artifact ID | ZIP SHA256 |
|---|---|---|
| navigator-core-evidence | `10672732745` | `1a8dd0862f7c7256598edde4f58a876df291774d49746a919545373d8e062fa3` |
| navigator-native-checkpoint | `10672638018` | `3e1ae9f48746ef66557a34c60a179338500ed690a0e21c5b6bed38034adb63c4` |

Downloaded ZIP hashes, result JSONs and the required-ID manifest were independently checked.

- Retained `tested-source.zip` SHA256: `e52ab5413bb4a7a486c7728183a658a8b8fd232eba092246597d5ed6d86a004f`。
- Product DLL SHA256: `ab9b26ad835a5393c50e30bf5f4c640f84603ee740a51f8b86a8ee604063be2d`。
- Core DLL SHA256: `027ce7821e1a163d78b83f1c75342c7d348c0e26c41df21228a8be20dfbe39d2`。
- 配布用DLLは上記2個のみ。proof DLL・YMM4本体・FFmpegの独自同梱なし。
- `ux-summary.json`: backend call **1 → 1**、設定変更/試用終了で追加Decodeなし。元VideoItem状態も維持。
- W1-R regressionは自身のfixture解析1回のみ増え、以後のSplit/Trim/Move/Copy/UndoRedoでは追加Decodeなし。正常系失敗のunhandled例外0。

### UI / acceptance boundary

実コンパイルViewのPNG6枚を取得し目視確認した。新Main Review 360×480、Filter管理360×480、学習400×640/640×640等のcontrol renderingで文字の重なりや主要操作の領域外逸脱がないことを確認。分類の既存/新規切替も実bindingで確認した。

ただし**物理キーボード入力、YMM4とのfocus競合、全DPI/theme、長いFilter名・大量Filter・3時間録画、通常 `.ymme` install/upgrade、ユーザー受入は未証明**。KeyBindingの接続とそのCommandによるhost移動の検証は、実キー入力の検証と別。

Draftと未保存のworking stateの保持は、同じNavigatorセッション内が対象。明示保存したセットはディスクへ残るが、YMM4終了・クラッシュを跨ぐDraft自動復元は未実装。セット切替の復元は1段のみ。設定保存の失敗/競合、生成中止、古いDraft拒否をテストしたことを、あらゆる電源断・外部改変に対する無損失保証へ拡大しない。

Generic Filter Pack、Audio/Image検索、W4-M、W6、学習Filter複製/完全削除、お気に入り、key remap、rich thumbnailは今回追加していない。新規汎用Filterは増やさず、既存3 seed Filterと既存学習Filterで基盤を通した。次は既存機能でHands-onし、妨げる点を修正した後、最小Generic Packへ進む。interaction freezeはその後。

### Failure retained, not relabeled

初回UI統合source `d402b6bed3709aa2fa3f6994e90209397561da24` / run `35674574951` は、pure/build PASS後に `ux_classification_progressive_disclosure` でFAIL。分類一覧refreshが直接selectedLabelを更新し、同じ値のsetterを通らないため新規入力欄の表示状態が追随しなかった。

製品側で一覧refresh時の表示状態を同期し、入力とDraftは保持。テストはmodel状態と実Visibilityの両方を、dispatcherのbinding処理後に確認する形へ強化した。必須IDを削除せず、source `4226361` の上記runで105項目を再実行してPASS。

## W1-R checkpoint — retained baseline

W1-Rのcurrent tested source、run/job/artifact/hash、実装境界は [W1_R_CHECKPOINT.md](W1_R_CHECKPOINT.md) に記録する。後続docs commit/main mergeのSHAをtested sourceへ読み替えない。

`decided != implemented != validated != accepted`。編集後の機能統合と、通常配布・物理操作・実素材精度・ユーザー受入は別。

## Current state

| 領域 | 実装と検証の範囲 |
|---|---|
| Portable Data | Plugin-local `Data/Learning|Review`、legacy移行、保存先表示/openを実装。native111 PASS、通常package/reinstallはOPEN |
| UI/UX foundation | 確認セット/作業保全/3 surface/local keyboard pathを実装。PR #9 native105 PASS、Hands-on未受入 |
| W1-R | Source anchor、安定順、known-lineage優先、一意partition再bind、個別Unavailable、current-map Jumpを実装。検証結果は上記checkpoint参照 |
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

Corpus正本はportable root `<YMM4>/user/plugin/Ymm4HighlightNavigator/Data/Learning` のcatalog、primitive Pack、分類membership。旧 `%LOCALAPPDATA%\Ymm4HighlightNavigator\Learning` はPortable側が未作成の場合だけ検証付き初回移行元として扱い、移行後も旧側はbackupとして残す。Portable側が存在した後はlegacyへsilent fallbackしない。動画のフルパスを永続identityにはせず、source hash・extractor・sampling・実際のsource rangeからsample keyを作る。同じ教材を再取込して二重学習しない。別のPositive分類は既存Packへのmembership追加で扱う。

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

## Earlier W3/W5 pure / media verification — historical

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

## Earlier W3/W5 product native verification — historical

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

## 2026-09-19 host-prep checkpoint — historical design preparation

この準備時点では追加仕様はNavigator製品コードへ未実装だった。W1-Rの後続実装は上記checkpointを参照。W4-M memoは引き続き未実装。Labで必要host factsだけ先に確定した。

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

現在の再開点は [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) の **PR #9で既存機能Hands-on → 必要な修正 → 最小Generic Filter Pack → 再評価**。UI/UXのinteraction freeze前であり、W4-M/W6は後続のまま。W3/W5や今回の保存基盤を再実装しない。

- **W1-Rの残る境界:** 一意partitionを観測できない複合編集・曖昧replacementはfail closed。source length/write stampは維持し、全ファイルの再hashを各Nextへ追加しない。
- **W4-M:** `見どころを確保`。memo SceneへFrame0 / separate Layerでreference Clip追加、default30秒/設定可、Remark attribution。
- その後 **W6-A:** Runtimeの `これは違う` → filter-specific Explicit Negative →保存済み特徴によるContrast改善。
- 誤検出低減と既存Positive保護、独立素材のcandidate density/query cost評価。clip-level一致と期待Transition地点を区別した評価。
- 既に全教材を拾える場合の変更なし候補扱い、細かい学習UX/確認セットのHands-on、Profile増加時の性能改善。
- W4-Mのproduct-native integration、broader reload/scene/cancel race、実codec/VFR/特殊timestamp、memory/ingest/query/GPU、通常 `.ymme` install/upgradeとhands-on acceptance。
- 消費型Inboxの明示所有権・削除transactionは未実装。現在の非破壊Importとは分けて実装・検証する。

現行はraw-video-freeの再利用が可能だが、元動画を削除する権限や機能を追加したわけではない。未知の将来ABIやprocess全体の障害まで無停止保証するものでもない。
