# W2 Shared Feature Engine Kickoff

W2はW1のnative host factを待たずに進められるpure lane。

## First slice

目的は「戦闘Profileを作る」ことではなく、RuntimeとLearningが同じprimitive schemaを共有できる最小engineを作ること。

最初に固定するmodel:

```text
FeatureSchemaVersion
SourceMetadata
SampleClock / Timestamp
VisualPrimitiveSample
AudioPrimitiveSample
CompactDescriptor (optional field family)
FeatureSeries
```

Profile名・戦闘/ステーション等のSemanticはこの層へ入れない。

## Deterministic fixture

repoへ巨大動画を置かず、FFmpegで短いfixtureを生成する。

Fixtureは最低限:

- source time/frameを追跡できる視覚pattern;
- brightness/contrast/colorの既知変化;
- stable -> sudden change;
- short audio beep/burst;
- silence / steady interval;
- deterministic duration/timestamps。

## First acceptance

- 同じfixtureから同じprimitive seriesを再現できる;
- serialize -> reloadで値/schema/timestampが保持される;
- Session Feature Index表現とLearning Feature Pack表現が同じProfile evaluator inputへ変換できる;
- missing/incompatible Feature familyを0として扱わない;
- Semantic Profile scoreをPackへ焼き込むことをAuthorityにしない。

## Not yet

- X4戦闘Profile tuning;
- Reverse Classification formula;
- GPU optimization;
- OCR / Object Detection / optical flow;
- final binary format optimization。

まずcorrect/shared schemaを作り、サイズ/速度最適化は実測後に行う。
