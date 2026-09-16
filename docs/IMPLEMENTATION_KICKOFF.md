# Implementation Kickoff

この文書は初回着手点だけを固定する。詳細Authorityは `docs/DESIGN.md` と `docs/ROADMAP.md`。YMM4 hostの採用済みEvidenceは `docs/LAB_REFERENCES.md` を索引とする。

## Immediate next work

### W1 — Target / Projection Spine

Branch: `feature/w1-projection-spine`

Recording Archive側のLab検証から、YMM4 Lite v4.56.1.0の以下は**実装開始前に採用済み**。

```text
rate surface        = PlaybackRate2
source-time authority = PlaybackRateMap
constant positive   = ContentOffset + itemTime * rate / 100
inverse boundary    = tested caseで [0, Length)
ContentLength       = Source Range consumed lengthには使わない
```

そのためW1はゼロからtiming modelを調べ直さない。

1. `docs/LAB_REFERENCES.md` のADOPTED timing Claimを実装前提として読む。
2. 未解決の `Target identity / Timeline FPS / absolute frame -> item-local rounding / Timeline seek-jump / Target Set lifecycle` だけを `chat-native-work-lab-001` へ最小Probeする。
3. Navigator側ではTarget Adapter / immutable snapshot / `PlaybackRateMap` boundary / Source Range Planner / dummy candidate projectionを実装する。
4. `PlaybackRateMap` getterがnon-publicなversion-pinned dependencyはTarget Adapter内へ狭く隔離する。
5. `ContentLength`から使用Source durationを推定しない。
6. host fact探索にはNavigator Actionsを使わない。
7. W1 product integrationができた時点で、Exit claimだけを検証するnative smoke workflowを追加する。

### W2 — Shared Feature Engine

W1の残存Lab確認と並行してYMM4非依存で開始可能。

- primitive Feature schema;
- deterministic FFmpeg fixture;
- Session Feature Index / Learning Feature Packの共通input contract;
- serializer/schema version;
- Profile evaluatorへ渡す最小model。

W2のpure workはW1 native proofを待たない。

## Do not do yet

- `PlaybackRate2` / `PlaybackRateMap` / 50・100・200% constant positive mappingを再発見するためだけの重複Lab Action;
- legacy `BaseItem.PlaybackRate`をsource-time Authorityとして実装;
- `ContentLength`をSource Range consumed lengthとして利用;
- Heavy ML / OCR / Object Detection導入;
- Profile自動relabelling;
- Archive Pluginへの分類UI追加;
- full native/release suiteの常時実行;
- 実X4録画やprivate Learning Corpusのrepo commit;
- W3以降のUIを先行して大規模実装。

## First checkpoint

次の状態になったら最初の大きなcheckpoint:

```text
Lab: W1残存host factだけがpin済み
+ Navigator: PlaybackRateMapを隔離したTarget Adapter
+ Navigator: dummy Source Episode -> exact YMM4 occurrence Jump
+ W2: deterministic fixtureからprimitive Feature schemaを生成/reload可能
```

ここまではW1/W2を並行し、その後W3/W4へ進む。
