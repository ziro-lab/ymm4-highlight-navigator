# Implementation continuation — v0.4.2 UI/UX foundation

この文書は現mainから次の実装を開始するための入口。Authorityは `AGENTS.md` → `docs/DESIGN.md` → `docs/UI_UX_GENERIC_FILTER_PLAN.md` → `docs/ROADMAP.md` → verified evidence の順で確認する。

## Current resume point

Current main: `b27c3d98950e2fdf7546278d531b6a0217a4c9fb`

- W1-R Edit-time Rebindingは実装済みcheckpoint。
- W3/W5の教材取込・Transition抽出・Filter生成・trial/save/runtime接続は再実装しない。
- W4 Multi-Filter Review / attribution / Global Sensitivity / Prev-Next-Listの既存spineを再利用する。
- W4-M Highlight MemoとW6 Explicit Negativeは有効な既存予定だが、**先にUI/UX foundationを整えてから revised Review UXへ統合する**。
- 次の主作業は `docs/UI_UX_GENERIC_FILTER_PLAN.md` の NOW scope。
- Generic Filterを大量に先行実装しない。

## Immediate implementation order

### UX-1 — information architecture + persistence contract

ユーザーの通常フローを次で固定する。

```text
対象動画
→ 確認セット / Filter
→ 解析
→ 候補Review
→ 必要なら見どころ確保
```

Concept:

- Filter = detector/query
- Group = 整理のみ
- Set = Filter参照 + review設定
- UI表記は **確認セット**
- Genre presetという第4概念を作らない。built-in Setとして扱う。

Main Reviewでは対象が最上位。Setを対象より上位概念にしない。

### UX-1.5 — prevention / work preservation

Filter authoringのvalid Draftを次の理由で失わせない。

- Cancel
- transient Error
- Save failure
- replacement generation failure

新Draftの生成成功または明示的discardまでは、直前のvalid Draftを保持する。

確認セットでも:

- saved Set
- current working review state

を分離する。Filter ON/OFFやSensitivity変更でsaved Setをsilent overwriteしない。

Filter rename / Group moveでSetが壊れないstable identityを使う。disabled/deleted Filter参照はsilent ignoreせず、UI上でdegraded stateを示す。

### UX-2 — existing-capability hands-on flow

新しい骨格を既存Filterだけで通す。

Acceptance:

```text
対象固定
→ 確認セット選択
→ Filter追加/削除
→ Sensitivity変更
→ 候補理由確認
→ Prev/Next/List
→ 現在の確認設定を保存
→ 別Set適用
→ 元の設定へ戻れる/失わない
```

Preserve:

- explicit target capture
- selection変更でTargetがsilent changeしない
- no-redecode Sensitivity
- candidate count / raw hit count
- attribution
- Progress / Cancel / Status
- Filter trial と Save の分離
- source media non-destructive

### UX-2.5 — high-frequency review path

最低限keyboard-accessible command pathを持つ:

- Previous candidate
- Next candidate
- Jump selected

W4-M / W6を統合する際は:

- 見どころを確保
- これは違う

もshortcut-readyなcommandとして追加する。

Exact key assignmentやユーザーによるremap UIは後回し。

### UX-3 — minimal Generic Filter Pack

UI骨格が既存Filterで成立した後だけ、最初の3つを追加する。

1. 大きな場面切替
2. 暗転 / フェード
3. 静穏 → 高活動

目的はFilterカタログ拡張ではなく、以下のpressure-test。

- multiple active Filters
- attribution
- Set save/switch
- candidate density
- management
- Sensitivity

### UX-4 / UX-5

3 Filterを入れた状態でhands-onし、必要な修正後にinteraction modelをfreezeする。

freeze後にだけ remaining Generic Filters → Audio → Reference Image/State search の順で拡張する。

## Main Review target shape

概念レイアウト:

```text
対象 [ 選択動画を対象に ]  recording_01.mp4 / 1個

確認セット [ X4録画チェック ▼ ]   [現在の確認設定を保存]

有効なフィルター
[戦闘開始 ×] [暗転 ×] [高活動→静穏 ×] [+追加]

検出感度
少なく拾う ─────●──── 多く拾う

候補 36件 / ヒット 48
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]

[ status / progress ]
```

W4-M統合時:

```text
確保時間 [30] 秒
[ ★ 見どころを確保 ]
```

通常画面へ出さない:

- Pattern weights
- primitive feature names
- many per-filter thresholds
- general rule builder

## Authoring target shape

通常フロー:

```text
教材を選ぶ
→ 既存分類を選ぶ / 新規作成
→ 取り込む
→ Filter候補を作る
→ 長尺で試す
→ 保存
```

Rules:

- folder-nameからのGroup / Filter推定を維持。
- Group / Filter名入力は新規分類/編集時を主にする。
- Preview/試用とSave/適用を分離。
- rollback targetが無い時は操作をdisabledにし、predictable failureを発生させない。

## Likely code touch

First passで確認する候補:

- `Plugin/NavigatorModel.cs`
  - review working state / Set application
  - enabled Filter state
  - candidate attribution exposure
  - shortcut-ready commands
- `Plugin/NavigatorView.xaml`
  - target-first layout
  - 確認セット
  - active Filter presentation
- `Plugin/LearningModel.cs`
  - valid Draft preservation
  - rollback availability
  - authoring state
- `Plugin/LearningView.xaml`
  - progressive disclosure of classification inputs
- small new persistence/model files for Review Set if existing storage does not already cover it.

Do not force Set persistence into FilterStore or duplicate Filter revision ownership.

## Testing order

1. Pure/model tests for Set identity, broken refs, saved-vs-working semantics.
2. Learning tests for Draft preservation on Cancel/Error/Save failure.
3. Existing Core/Learning regressions.
4. Plugin/UI model tests.
5. Native product checkpoint only when host/UI integration needs proof.

Do not use native Actions for every doc-only or pure-model edit.

## Existing regression to preserve

- Baseline Core 27.
- Learning 35 × 2 generated-media conditions.
- W1-R checkpoint behavior.
- Existing product-native integration baseline.
- version番号だけでYMM4を拒否しない。
- YMM4 bundled FFmpeg only.
- Slider/Filter toggleで再Decodeしない。
- Learning Filter trial/save/raw-free reuse。
- Candidate attribution。
- same-source separate occurrence / edit-time rebind semantics。

## Deferred — visible but not part of this first implementation

- favorites / hidden / recent
- "used by these Sets"
- richer management sorting
- Before / Hit / After thumbnails
- configurable shortcuts
- final visual polish
- Audio Generic Filters
- Reference Image / State search
- OCR / embeddings / object detection
- complex cross-filter logic

詳細は `docs/UI_UX_GENERIC_FILTER_PLAN.md` の LATER sectionをAuthorityとする。

## W4-M / W6 after UI foundation

W4-M Highlight MemoとW6 Explicit Negativeは捨てない。

UI/UX freeze前後で、既存のLab evidenceを使って revised Main Review / candidate UXへ統合する。Host factを既にLabで証明済みなら再調査せず、product integrationだけnativeで確認する。

## Branch / PR discipline

次の実装はmainへ直接積み上げず、小さいDraft PRで行う。

推奨:

- `feature/v0.4.2-ux-foundation`
- 必要ならwork-preservationを同PR内の独立commitにする
- minimal Generic Filter PackはUI skeletonが成立してから別commitまたは別PR

PRのScopeを越えてGeneric Filter大量追加やW6まで一気に進めない。

## Stop / reopen conditions

- Set semanticsがFilter revision ownershipと競合する -> 実装便利さで決めず再設計。
- saved Setとworking stateが区別できず作業消失が起きる -> freezeしない。
- Filterを3個程度有効化しただけでMain Reviewが破綻する -> Generic拡充前にUX修正。
- shortcut実装にhost input-routeの未知事実が必要 -> その狭いfactだけLabへ戻す。
- host capability変更 -> 該当機能だけfail closed。
