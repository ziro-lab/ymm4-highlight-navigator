# W2 Shared Feature Engine — continuation

最初のW2 CPU sliceは実装・Core regression済み。現在の状態は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)、形式は [FEATURE_FORMAT.md](FEATURE_FORMAT.md)。初回skeleton作成へ戻らない。

## Existing code

`src/Ymm4HighlightNavigator.Core/` にFeatureHeader、immutable Video/Audio primitives、FeaturePack validation、PackStore、FFmpegBackend、salience、Profile evaluator、intervalsを持つ。

Profile名・戦闘/ステーションのSemanticをprimitiveの正本に混ぜない。欠けたfeatureを0やNegativeへ変換せず、schema compatibilityを明示する。Runtime IndexとLearning Packは表現を共有してもlifecycleを分ける。

## Existing acceptance

Generated lossless black/white + gated-tone fixtureで、full/trimmed clock、serialize/reload、missing audio、corrupted Pack、backend cancellation/source preservationを検査。正確なケース数/runはチェックポイントを参照。

## Next

W3は現在のPackStore/FeaturePackを使い、Folderとpositive membership、sample登録、dedupeを追加する。Pack保存成功はCorpus commitや入力削除の許可ではない。

W2自身の残りは実長尺のサイズ/速度/メモリ測定、別codec/VFR、normalization比較、GPU capability/resize/fallback。tiny scalar queryの300ms目標や実X4品質を短い生成fixtureのPASSから推定しない。

pure変更はCoreのLinux lane、host/GPU統合だけ必要なnative checkpoint。Heavy ML/OCR/object detectionへ自動拡張しない。
