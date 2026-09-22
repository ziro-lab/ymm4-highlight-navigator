# Implementation continuation — v0.4.2 UI/UX foundation candidate

最初に `AGENTS.md`、`docs/DESIGN.md`、`docs/UI_UX_GENERIC_FILTER_PLAN.md`、`docs/ROADMAP.md`、`docs/IMPLEMENTATION_STATUS.md` と現在のmain/PRを確認する。現在のユーザー決定が最上位。未実装・機能統合済み・UX受入済みを混同しない。

## Current resume point — 2026-09-22

- Draft PR: [#9](https://github.com/ziro-lab/ymm4-highlight-navigator/pull/9)
- Branch: `feature/v0.4.2-ux-foundation`
- Base main: `dc2de6e2d037c895d2b16f0dd0b45e8c0e59ea0f`
- Exact tested source: `42263615642dc94907fbefc7471fc3015a1512ce`
- Native + pure preflight: run `35675127362`、両job PASS。Native105項目（既存69 + UX36）。結果とhashは `IMPLEMENTATION_STATUS.md`。
- 後続docs-only commitがあっても、上記tested sourceを新しいHEADへ読み替えない。
- main未merge。PRはDraft維持。通常配布やユーザー受入は未完了。

## Already implemented — do not rebuild

既存のW1-R source-time/rebinding、FeaturePack/TransitionIndex、学習Corpus、FilterAuthor、FilterStore/revision、no-redecode queryを維持する。

今回追加した基盤:

- `ReviewConfiguration / ReviewSet / ReviewWorkspace`: 保存済みとworking stateの分離、Filter ID参照、切替前への1段復元。
- `ReviewSetStore`: 明示保存/複製/上書き、read-only built-in、missing-reference保持、revision/writer lock、破損を空成功にしない保存。
- 表示名・整理Groupのalias。既存教材の分類やFilter保存keyは変更しない。
- `ReviewSettingsIntegration.cs`: Main Reviewへのセット接続、active Filter、別の選択・整理surface。
- `LearningModel.cs`: 有効Draftを生成中止/失敗/保存失敗/分類変更で消さず、不適用Draftは試用/保存を止める。
- `LearningIntegration.cs`: 同じNavigatorセッション内の作成Window再オープンでDraftを保持。明示終了まで試用も保持。
- 既存/新規分類の入力表示切替、実際にrollback先がある時だけ操作可能。
- 候補一覧内だけの `Alt+↑ / Alt+↓ / Enter`。global key hookは追加していない。

Generic Filter Pack、W4-M、W6、画像/音声検索、学習Filter自体の複製/完全削除は未追加。基盤sliceは既存3 seed Filterと既存学習Filterで検証した。

## Next — UX-2 / UX-2.5 hands-on before generic expansion

実装済みのモデルや保存機構を再設計せず、日常操作で破綻する点を確認する。

```text
対象固定
→ 確認セット選択
→ Filter ON/OFF・検索
→ 感度変更
→ 候補理由確認
→ 前/次/一覧/Jump
→ 新規セット保存・上書き・切替前に戻す
→ フィルター試用・保存・試用終了
```

確認点:

1. 実キー入力で候補一覧のAlt+↑↓/Enterが使え、TextBoxやYMM4の編集操作を奪わないか。Command呼出しによるnative PASSは物理input/focusの証明ではない。
2. 狭いDock、高DPI、長い名称、Filterが増えた状態でも候補一覧・状態・保存エラーへ到達できるか。save/管理のExpander展開時も確認する。
3. セット切替/保存/再読込でworking stateが意図せず変わらず、保存済み・変更あり・試用中・参照切れを区別できるか。
4. Draftを保持したまま分類変更→復帰、再生成中止、保存失敗→再試行が自然か。Windowを閉じる操作がDraft破棄に見えないか。
5. 表示aliasと教材分類は別であること、フィルターのOFFは削除ではないことが伝わるか。

Preview/保存の物理操作、入出力picker、theme/DPI、実録画精度・レビュー時間は未受入。結果に応じた狭い修正を行い、CI greenだけでUXをfreezeしない。

## Then UX-3 → UX-4 → UX-5

既存Filterで操作を確認した後、別commitまたは別PRで最小Generic Filter Packを追加する。

- 大きな場面切替
- 暗転 / フェード
- 静穏 → 高活動

これらは候補であり、実素材で成立しない検出器を名称だけで標準搭載しない。新しい巨大Engineを作らず、既存Featureを使って少数を検証する。

複数Filterのattribution・件数/密度・Set切替・管理を再評価し、その後にinteraction modelをfreezeする。画像からの意味的な類似検索や、ゲーム固有UIの高精度判定まで保証しない。

## Persistence and preservation boundary

- 確認セット: `%LOCALAPPDATA%\Ymm4HighlightNavigator\Review\review-settings.json`。
- Corpus/学習Filter: 既存の `%LOCALAPPDATA%\Ymm4HighlightNavigator\Learning`。正本は分離。
- 明示保存したセットはディスクへ残る。未保存working state/Draftはセッション内保持であり、YMM4再起動/クラッシュ後の自動復元は未実装。
- 表示aliasで既存Filter IDを変えない。教材分類のrename/migrationや学習Filter複製は別の所有権・回帰仕様が必要。
- broken referencesを勝手に除去しない。破損ファイルを空として上書きしない。
- Draft保持とDraft適用可否を分ける。保持しているから古い教材/revisionへ保存できるわけではない。

## Regression / execution discipline

Pure: Core27、Rebinding29、Review27、Learning35×2素材条件。Native: required-native-cases.jsonの105 IDとsource/製品DLLを独立gateで照合する。assertion数を増やすこと自体を目的にしない。

UI/host統合に変更があるcheckpointでだけnativeを使う。docs-onlyやpure変更でWindowsを重複実行しない。unknownなhost input-routeが必要になった時だけ狭くLabへ戻し、既存host factsを再調査しない。

元動画/Review Itemの非破壊、YMM4 bundled FFmpeg、source-time再bind、no-redecode、複数Filter非排他ORとattributionを維持する。private素材・ユーザーCorpus・host binariesをcommitしない。

## Still deferred

お気に入り/最近使った、rich thumbnail、key remap、Generic大量追加、Audio/Image検索、OCR/embeddings、W4-M memo、W6 Explicit Negativeは予定として残す。W4-M/W6の既存Lab evidenceと設計は有効で、新しいReview UIへ後続統合する。

通常の `.ymme` install/upgradeや配布資料は別acceptance。ActionsのDLL artifactを完成インストーラーと呼ばない。明示指示なしにPRをmergeしたりreleaseしない。
