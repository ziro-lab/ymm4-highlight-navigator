# Implementation continuation — after initial W3/W5

最初に `AGENTS.md`、`docs/DESIGN.md`、[実装状況](IMPLEMENTATION_STATUS.md)、[ROADMAP](ROADMAP.md) と現在のmain/PRを確認する。設計はv0.4.1。W3/W5の初期コードは既にあるので、旧「W3-Aから作る」指示へ戻らない。

## Current reusable spine

```text
普通の教材動画 / 直下フォルダー
→ LearningModel / CorpusStore
→ FeaturePack / catalog / positive membership
→ TransitionIndex
→ FilterAuthor / TransitionMatcher
→ FilterDraft
→ trial or FilterStore.Apply
→ NavigatorModel learned evaluator
→ Episode Union / Target projection / Prev/Next
```

主要実装:

- `Core/Learning/CorpusStore.cs`: 非破壊batch、dedupe、分類、verified Pack/catalog publication。
- `Core/Learning/TransitionFeatures.cs`: 局所before/after・時間方向signature、候補生成。
- `Core/Learning/TransitionFilters.cs`: 複数OR Pattern、候補作成、固定感度replay。
- `Core/Learning/FilterStore.cs`: compare-and-set Apply、Positive回帰拒否、revision/rollback。
- `Plugin/LearningModel.cs` / `LearningView`: 教材→候補→試用/保存の日本語UI。
- `Plugin/LearningIntegration.cs`: saved Filter load、session trial、メインRuntimeへの接続。

raw videoは変更・削除しない。YMM4同梱FFmpegのLocator、no-redecode、TargetAdapter、FeaturePackを再実装しない。現在のmatcherはvisual-firstで、audioは保持のみ。詳細契約は [FEATURE_FORMAT.md](FEATURE_FORMAT.md)。

## Next primary slice — W6-A false-positive feedback

目的は、実際に不要だった候補を **そのFilterのExplicit Negative** として、Featureだけから改善に使えるようにすること。

1. Runtime candidateがどのFilter/revision/Transition center/source windowから来たかを保持する。現在のCandidate表示名だけで永続的なNegative identityを作らない。
2. ユーザーの「これは違う」で対象Filterと局所windowを記録する。1候補に複数Filterがある場合、別Filterへ自動で否定を広げない。通常1Filterなら反復入力を増やさない。
3. 既存Featureから必要windowを保存し、原動画の再切り出しや別FFmpeg再Decodeを要求しない。before/after contextとsource-time対応、extractor/algorithm/revisionを残す。
4. Negativeは人間が付けたラベル。別フォルダー/未所属/低一致/音声なしを自動Negativeにしない。取消・誤指定の修正を可能にする。
5. 保存完了とUI操作成功を分け、失敗/中止でFalse Positiveが登録できたと表示しない。通常Reviewの元動画/ymmpを変更しない。

この段階で、新しい汎用分類器や大規模な管理画面を先に作らない。

## Then W6-B/C — bounded refinement

現行Authorは未検出Positiveを優先してPattern追加し、CoveredCandidateも回帰に残す。ここへExplicit Negativeとの比較を加える。

- Positiveを落とす広いNegative-vetoではなく、識別に役立つ特徴差・閾値・Pattern修正候補を作る。
- 既存の固定条件replayへPositive/Negativeを合わせて流し、見逃しを重く扱う。
- 教材に一致があるだけでは狙ったTransitionの確認ではない。独立した実素材/期待地点の評価は別に設ける。
- Runtime感度は利用時の調整。学習回帰の基準感度1と結果を混ぜない。
- Patternが増えるだけの改善、候補が録画の大半を覆う改善を成功扱いしない。raw center数・統合候補数・レビュー区間長・query costを区別する。
- 全教材を既に拾える場合、不要な探索や同内容revision追加を避ける変更なし経路を整理する。
- Preview/Apply/rollbackは今のFilterStoreへ接続し、第二の保存正本を作らない。

## Fixtures / acceptance to add

Coreで先に再現する:

- 同じ候補でAは誤検出、Bは正しい。NegativeはAだけ。
- 教材PositiveとNegativeに似た画面変化があるが、前後の局所差だけが違う。
- Negativeを抑えるだけの修正が既存Positiveも落とすので拒否される。
- ラベル取消・duplicate negative・異なるFilter revision・Corpus変更中のstale draft。
- Raw videoがない状態でNegative特徴/改善候補を再読込できる。
- 感度変更は保存Filter/Negativeラベル/基準感度の回帰結果を変更しない。
- 関係ない強い切替だけを拾った教材を、正しいTransitionの意味的成功と報告しない。

必要な製品native checkpointでは、実候補→指定FilterのNegative登録→画面を閉じてもReview継続、例外がホストへ漏れない通常失敗経路を確認する。物理入力や将来の全API互換をそのPASSに含めない。

## Regression to preserve

- Baseline Core27ケース。
- Learning35ケース。通常generated mediaとvideo6.000s/audio6.016sのstream-copy素材の両方。
- Product native47項目。正確なtested source/runはIMPLEMENTATION_STATUS参照。
- 実行時にversion番号だけでYMM4を拒否しない。FFmpeg host assemblyはoptional/lazy、private copy/PATH fallbackなし。
- Candidate attributionと同じSourceの別Timeline occurrenceを保持。
- SliderとFilter切替で再Decodeしない。統合後の候補件数はmergeで変化するため、感度包含はraw centersでも確認する。

## Open but not blockers for W6-A

- 実長尺の性能・メモリ・実X4品質、GPU、通常 `.ymme` 導入/upgrade。
- wider project/scene/Undo/cancel lifetime、codec/VFR/timestamp variation。
- 明示された消費型Inboxの削除経路。現在は非破壊であり、Pack保存だけを削除許可にしない。
- Review Preset、教材結果の位置確認補助、長いラベル/多数FilterのUI調整。

## Execution discipline

Pure workにnative Actionsを毎回使わない。変更をまとめ、Linux learning regression→host/UI変更checkpointのnative確認へ進める。文書だけの更新に新native runは不要。未確定のhost事実はLabの既存Evidenceを先に調べ、不足部分だけ新Probeにする。

録画アーカイブは依存先ではない。教材がその出力でも普通の動画として扱い、Archiveのコード/分類UI/Probeへ作業を広げない。private録画、学習Corpus、YMM4/FFmpeg binaryはcommitしない。
