# Implementation Kickoff

この文書は初回着手点だけを固定する。詳細Authorityは `docs/DESIGN.md` と `docs/ROADMAP.md`。

## Immediate next work

### W1 — Target / Projection Spine

Branch: `feature/w1-projection-spine`

1. `docs/LAB_REFERENCES.md` のW1 evidence checklistを棚卸しする。
2. 既存Lab evidenceで直接証明できないYMM4 host Claimだけを `chat-native-work-lab-001` へ最小Probeする。
3. Navigator側ではTarget Adapter / immutable snapshot / Source Range Planner / dummy candidate projectionを実装する。
4. host fact探索にはNavigator Actionsを使わない。
5. W1 product integrationができた時点で、Exit claimだけを検証するnative smoke workflowを追加する。

### W2 — Shared Feature Engine

W1のLab待ちと並行してYMM4非依存で開始可能。

- primitive Feature schema;
- deterministic FFmpeg fixture;
- Session Feature Index / Learning Feature Packの共通input contract;
- serializer/schema version;
- Profile evaluatorへ渡す最小model。

W2のpure workはW1 native proofを待たない。

## Do not do yet

- Heavy ML / OCR / Object Detection導入;
- Profile自動relabelling;
- Archive Pluginへの分類UI追加;
- full native/release suiteの常時実行;
- 実X4録画やprivate Learning Corpusのrepo commit;
- W3以降のUIを先行して大規模実装。

## First checkpoint

次の状態になったら最初の大きなcheckpoint:

```text
Lab: W1で必要なhost factがpin済み
+ Navigator: dummy Source Episode -> exact YMM4 occurrence Jump
+ W2: deterministic fixtureからprimitive Feature schemaを生成/reload可能
```

ここまではW1/W2を並行し、その後W3/W4へ進む。
