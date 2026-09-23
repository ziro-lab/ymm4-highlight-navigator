# UI/UX + Generic Filter planned direction

Status: **PLANNED / not yet implementation-frozen**

This memo records the product direction decided on 2026-09-22 and refined on 2026-09-23 through a first-time-user cognitive walkthrough. Existing W1-R/W4-M/W6 work remains valid; this document changes the preferred sequencing and user-facing information architecture, not the already-verified host/core evidence.

## Why this pass comes first

Before adding many more filters, make the product easy to:

1. create filters,
2. use filters,
3. manage filters.

Do not let a growing filter catalog dictate the UI by accident. First establish the interaction model, validate it with existing filters, then add only a small generic pack and use that pack to pressure-test the UI.

The goal is not to finish visual polish before feature work. The first pass is **information architecture + workflow**, with visual polish following only where needed.

## Implementation boundary — NOW vs LATER

This plan intentionally separates **the UI/UX foundation to implement in the next pass** from improvements that should stay visible but wait until real hands-on use justifies them.

### NOW — implement in the next UI/UX pass

These items affect the mental model, persistence contract, work preservation, or the high-frequency review loop. Changing them later would be expensive.

#### A. Review information architecture

Keep the everyday task order explicit:

```text
解析対象
→ 見たいもの
→ 必要ならFilterをその場でON/OFF
→ 解析
→ 見どころ候補をReview
→ 必要なら見どころ確保
```

- Preserve explicit target capture. YMM4 selection changes must not silently change the review target.
- The internal concept remains `ReviewSet` / Set. The primary user-facing label is **見たいもの**.
- Internal `Target` continues to mean the captured analysis/review target. Do not reuse Target for the user-facing “見たいもの” concept.
- “ジャンル” and any future parent category organize `見たいもの`; they are not additional runtime detector semantics.
- Do not force the user through separate category dropdowns on every review. Main Review selects **見たいもの directly**, while the picker shows its classification path such as `ゲーム > X4`.
- A future extra level before Genre must be addable without changing the everyday review flow. Avoid a persistence contract that assumes a fixed two-level hierarchy.
- Keep Main Review focused on the analysis target, current “見たいもの”, active Filters, candidate navigation, and current status.

#### B. Filter / Filter Group / View Intent semantics and persistence

Freeze the meanings separately; similar labels must not share one storage field accidentally:

- **Filter** = detector/query.
- **Filter Group** = organization of Filter parts only. Existing presentation Group belongs here.
- **ReviewSet / Set** = internal reusable review configuration referencing Filters.
- **見たいもの** = user-facing meaning of a ReviewSet.
- **見たいもの classification path** = organizational metadata for “見たいもの” such as `ゲーム > X4`. This is distinct from Filter Group and may gain another parent level later.

Implement the minimum “見たいもの” lifecycle now:

- stable Filter IDs; Filter rename/group move must not break “見たいもの”;
- stable ReviewSet ID; rename/classification move must not change its identity;
- built-in “見たいもの” vs user-created distinction;
- deleted/missing Filter references degrade visibly instead of silently changing meaning;
- save current working review configuration explicitly;
- duplicate an existing “見たいもの” cheaply and use the copy as a starting point;
- delete user-created “見たいもの” explicitly; built-ins are not deleted or overwritten;
- applying/editing must not silently overwrite the saved “見たいもの”.

Do not add separate “genre preset” runtime semantics. `ゲーム基本`, `配信基本`, etc. are ordinary built-in ReviewSets with classification metadata.

#### C. Work preservation / prevention

Do not discard a valid user-created state merely because the next operation failed or was cancelled.

For Filter authoring:

- keep the current valid Draft until a replacement Draft succeeds;
- Cancel keeps the previous valid Draft;
- transient Error keeps the previous valid Draft;
- Save failure keeps the Draft so the user can retry;
- only a successful replacement or explicit discard clears it.

For review Sets:

- distinguish **saved Set** from **current working review state**;
- changing Filter ON/OFF or Sensitivity does not silently mutate the saved Set;
- switching/applying another Set must not silently destroy the previous working state. The exact lightweight restore UI may be implemented as part of the hands-on pass, but the preservation rule is fixed now.

#### D. Main Review essentials

Implement/retain:

- explicit target capture + target summary;
- “見たいもの” selector with classification path + short automatic Filter summary;
- enabled Filter chips / quick ON-OFF;
- a compact “フィルター n/m” control to expose all Filters belonging to the current “見たいもの” without opening asset management;
- explicit modified-state indicator when current Filter/Sensitivity choices differ from the saved “見たいもの”;
- Global Sensitivity;
- candidate count / raw hit count;
- Prev / Next / List / Jump;
- candidate attribution/reason;
- Progress / Cancel / Status;
- W4-M Highlight Memo controls when W4-M is integrated.

Do not expose low-level Pattern weights, primitive feature names, or many per-Filter thresholds.

#### E. Authoring simplification

Keep authoring separate from Main Review and reduce simultaneous decisions.

Preferred first-use flow:

```text
教材を選ぶ
→ 既存分類を選ぶ / 新規作成
→ 取り込む
→ Filter候補を作る
→ 長尺で試す
→ 保存
```

- Preserve folder-name inference for Group / Filter name.
- Show Group / Filter name entry prominently only when creating a new classification or editing it.
- Keep Preview/試用 separate from Save/適用.
- Disable rollback when no rollback target exists rather than letting a predictable command fail.

#### F. High-frequency review shortcuts

Provide a keyboard path for the repeated review loop, at minimum:

- previous candidate;
- next candidate;
- jump to selected candidate.

When Highlight Memo and Explicit Negative are integrated, add shortcut-ready command paths for those operations too.

Exact key assignments and user-configurable remapping can wait; the commands should not require mouse-only operation.

#### G. Minimal Generic Filter pressure test

After the above UI skeleton works with existing Filters, add only:

1. 大きな場面切替
2. 暗転 / フェード
3. 静穏 → 高活動

Use them to pressure-test multi-Filter state, attribution, Set switching, candidate density and management.

### LATER — keep planned, do not implement in this UI/UX pass

These are useful, but do not need to block the first interaction-model freeze.

#### Management refinements

Initial management now includes the lifecycle needed to prevent accumulation of dead assets:

- separate **見たいものを整理** and **フィルターを整理** responsibilities;
- duplicate / rename / classification move / delete for user-created “見たいもの”;
- Filter search;
- Filter usage count / usage destinations;
- all / used / unused Filter views;
- explicit delete impact before deleting a Filter referenced by any “見たいもの”.

Still later:

- favorites;
- hidden Filters / Groups;
- recently used;
- richer sort/filter views;
- import/export of Sets/Filters if distribution later needs it.

#### Candidate-list richness

- Before / Hit / After thumbnails;
- richer visual confidence/explanation;
- density visualization beyond the existing counts;
- per-candidate preview polish.

Start with time + attribution/reason and validate whether richer preview materially speeds review.

#### Shortcut refinements

- user-configurable key bindings;
- shortcut profiles;
- discoverability overlays / cheat sheet.

#### Visual polish

- final spacing, iconography, animation, color semantics and compact-density tuning beyond what is needed for the hands-on test.

Do not freeze decorative polish before the information architecture survives real use.

#### Additional Filter families

After the interaction model is frozen:

- remaining cheap Generic Filters;
- Audio Generic Filters;
- Reference Image / State search;
- only then reconsider OCR, embeddings or other heavier detectors if real recordings justify them.

### Scope rule

If a change alters **what Filter/Group/Set means, how saved work survives, or the high-frequency review path**, it belongs in NOW.

If it mainly improves **catalog convenience, presentation richness, customization, or adds a new signal family**, it belongs in LATER unless hands-on use proves it blocks the core workflow.


## Core concepts

Keep internal detector organization and user-facing review organization distinct.

- **Filter** — detector/query that produces candidate hits.
- **Filter Group** — organization of Filter parts only. Moving/renaming it must not change detection semantics or ReviewSet identity.
- **ReviewSet / Set** — internal reusable runtime combination of Filter references/enabled states plus Global Sensitivity and other proven review settings.
- **見たいもの** — user-facing concept backed by a ReviewSet: “what I want to look for in this video”.
- **Classification path** — organization of “見たいもの”; initially it may look like Genre, but storage/UI must allow another parent category later.

Short rule:

> 分類は探しやすくする。見たいものは目的を表す。Filterは実際に探す。

Examples:

```text
見たいもの
ゲーム
├─ ゲーム基本          (built-in)
├─ X4                  (user)
└─ Minecraft           (user)

動画シリーズ
├─ シリーズ基本        (built-in)
└─ 小夜ミコちりつも宇宙記 (user)
```

A future extra level remains valid:

```text
動画
└─ ゲーム
   └─ X4
```

Main Review must not gain another mandatory dropdown merely because this hierarchy becomes deeper.

Built-in basics are ordinary built-in ReviewSets. Do not create a fourth “Preset” object model solely for them.

## UI surfaces

### 1. Main Review — everyday surface

Keep the main review surface small.

Primary content:

- explicit target capture / target summary
- active 確認セット selector
- enabled Filter chips / quick ON-OFF
- Global Sensitivity
- candidate count / raw hit count
- Prev / Next / List
- selected-candidate attribution / reason
- highlight memo controls

Conceptual layout:

```text
解析対象
[ 選択動画を対象に ]  recording_01.mp4 / 1個

見たいもの
[ ゲーム > X4                         ▼ ] [編集]
  戦闘開始・MAP切替など5フィルター

使うフィルター
[戦闘開始 ×] [MAP切替 ×] [大きな場面切替 ×]
[ フィルター 3/5 ▼ ]

● 変更あり
[この内容で保存] [別名で保存]

検出感度
少なく拾う ─────●──── 多く拾う

候補 36件 / ヒット 48
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]

確保時間 [30] 秒
[ ★ 見どころを確保 ]
```

Do not expose Pattern weights, primitive feature names, or many per-filter thresholds on this surface.

The “見たいもの” picker should be directly searchable and grouped by classification. Each row should provide recognition instead of requiring memory:

```text
ゲーム > X4
戦闘開始・MAP切替など5フィルター

ゲーム > ゲーム基本
場面切替・暗転・活動変化
```

Do not require a separate Genre selection before this picker. Classification is visible context, not a mandatory navigation step.

The active Filter chips are the fast path. Their × action means **OFF for the current working state**, never deletion. True Filter deletion exists only in Filter management.

Saving controls should become prominent only when the working state materially differs from the saved “見たいもの”. Avoid presenting save/reload administration as an equal first-step choice before the user has even analyzed a video.

Sensitivity should continue to re-query the existing Feature Index without re-decoding.

### 2. Filter Authoring — create/improve

Separate authoring from everyday review.

Preferred flow:

```text
教材を追加
→ 解析
→ Filter候補生成
→ Positive replay
→ 長尺動画で試す
→ 感度を確認
→ 保存
```

Avoid an empty advanced rule builder as the default starting point.

Future entry points can share one concept:

```text
フィルターを作る
- 動画から作る
- 画像から作る
- 既存フィルターを複製
```

### 3. Management — organize assets, not everyday review

Do not combine frequent Filter ON/OFF with rare asset management in one ambiguous “選ぶ・整理” surface.

#### 見たいものを整理

Initial scope:

- browse/search by classification path;
- create;
- duplicate an existing “見たいもの” as the primary starting route;
- rename;
- move to another classification;
- edit referenced Filter composition;
- delete user-created items;
- built-ins: readable/duplicable, but not overwritten or deleted.

#### フィルターを整理

Initial scope:

- Filter Group;
- rename/presentation organization where identity semantics permit;
- duplicate user Filter where supported;
- search;
- all / used / unused;
- show usage count and concrete “見たいもの” destinations;
- delete only through this management surface;
- before deleting a referenced Filter, show the affected “見たいもの” names instead of a generic confirmation.

Main Review gets a lightweight Filter chooser for the current working state. It does not need to expose Filter asset deletion.

Later only if real use needs them:

- favorites
- hidden filters/groups
- recent filters

## Set model

A Set should reference Filters rather than copy them.

Initial Set payload should be kept small:

- Set id/name
- Filter ids and enabled state
- Global Sensitivity
- only other user-facing runtime options proven necessary

Required lifecycle decisions before implementation:

- Filter rename must not break Sets.
- Group move must not break Sets.
- Deleted/disabled Filter references need a friendly degraded state.
- Built-in Sets and user Sets must be distinguishable.
- "Save current as Set" and Set duplication should be cheap operations.

Do not turn Set into a second Filter-definition format.

## Candidate list UX

Always preserve attribution: the user should know **why** a candidate exists.

Initial:

```text
00:34:12
戦闘開始 / 静穏→高活動
```

Later, if useful and cheap enough:

```text
[Before] [Hit] [After]
```

A before/hit/after preview is more aligned with Navigator's transition-first model than a single unexplained thumbnail.

## Initial Generic Filter Pack

Do not build a large catalog before the UI is validated.

Start with only 2–3 useful filters that exercise different behaviors:

1. 大きな場面切替
2. 暗転 / フェード
3. 静穏 → 高活動

Use these as a pressure test for:

- multiple active Filters
- attribution
- Set save/switch
- candidate density
- management/search/grouping
- sensitivity behavior

After the UI/UX model is hands-on validated, expand only as justified.

Candidate backlog:

- 高活動 → 静穏
- 強いフラッシュ / 明度急変
- 長時間ほぼ静止
- 画面構成の大きな変化
- 全画面UIへの切替
- 全画面UIからの復帰

## Deferred extensions — keep visible, do not forget

### Audio generic filters

Feature Packs already retain audio evidence. Later candidates include:

- 音量急増
- 無音 → 音あり
- 音あり → 無音
- large audio peak
- 高活動音 → 静穏

Keep them in the same Filter/Set UX rather than inventing an audio-specific product mode.

### Reference image / state search

Future lightweight path:

```text
参照画像
→ compatible visual descriptor
→ query existing Feature Index
→ similar-state candidates
```

Potential stronger UX:

```text
「この画像に似た場面」
+
existing TransitionIndex
→ 「この画面になった瞬間」
```

Do not require CLIP/Heavy ML for first value. Revisit embedding/OCR/object detection only if cheap descriptors fail real use.

### Management refinements

Possible later additions:

- favorites
- hidden groups
- recently used
- filter usage by Set
- import/export of Sets/Filters if distribution needs it

These are deliberately deferred until catalog size makes them useful.

## Things not to expand into now

Do not make the UI/UX pass an excuse to add:

- OCR as a baseline dependency
- CLIP/heavy image embedding as a baseline dependency
- object detection
- automatic genre classification
- automatic "interestingness" scoring
- complex cross-filter AND/N-of-M builders
- large per-filter parameter panels
- general visual programming/rule-builder UI
- genre-specific analysis engines
- fully automatic editing

## Preferred execution sequence

The NOW/LATER boundary above is authoritative for scope.

### UX-1 — information architecture + preservation contract

Freeze the user meaning of Filter / Group / Set and the responsibilities of:

- Main Review
- Filter Authoring
- Filter Management

Define Set persistence, broken-reference behavior, and the Draft/working-state preservation contract before polishing the visuals. Keep the user task order as target → review settings → analysis → review.

### UX-1.5 — prevention / work-preservation implementation

Fix predictable work-loss paths before broad UI polish: preserve a valid Draft across cancel/error/save failure, separate saved Set from working state, and make unavailable rollback/actions disabled rather than failure-driven.

### UX-2 — first-time cognitive walkthrough + existing-feature hands-on

Implement/prototype the new flow using existing Filters first.

Primary first-time path:

```text
Navigatorを開く
→ 解析対象が無ければ「選択動画を対象に」が次の一手として見える
→ 見たいものを直接選ぶ（分類はpicker内で見える）
→ 選んだ内容のFilter要約が見える
→ 解析
→ 候補数と理由が見える
→ 前/次/一覧で巡回
→ Filter ON/OFFやSensitivityをその場で調整
→ 変更ありを認識
→ 必要なら保存 / 別名保存
```

Cognitive walkthrough questions for every step:

1. 初見ユーザーは「次に何をしたいか」を自然に持てるか。
2. その目的に対応する操作が画面上で見つかるか。
3. ラベルから押した結果を予測できるか。
4. 操作後、成功・変更・未保存状態が画面から分かるか。

The Main Review should pass this path without requiring README, tooltip discovery, or a separate onboarding wizard.

### UX-2.5 — high-frequency review path

Verify keyboard-accessible Prev / Next / Jump and that repeated candidate review does not require mouse-only round trips.

### UX-3 — minimal Generic Filter Pack

Add only the first 2–3 generic filters.

### UX-4 — pressure-test and revise

With several filters active, verify:

- Main Review stays small.
- active-filter state is obvious.
- attribution is understandable.
- Set switching is fast and predictable.
- management remains understandable.
- candidate density does not make review useless.

### UX-5 — UI/UX freeze

Freeze the interaction model after hands-on feedback. Do not wait for every future filter type.

### GF-1 — expand Generic Filters

Add the remaining cheap/high-value generic filters one by one, measuring actual usefulness.

### GF-2 — optional signal families

Only after the above:

- Audio generic filters
- reference image/state search
- other cheap signal families

## Relationship to existing W4-M / W6

Existing roadmap work is **not cancelled**.

- W4-M Highlight Memo remains valid and should be placed cleanly into the revised Main Review.
- W6 Explicit Negative / Contrast remains valid and should be surfaced through the revised candidate/review UX.
- Existing W1-R, FeaturePack, TransitionIndex, FilterStore and revision evidence remain reusable.

The UI/UX pass should wrap and clarify those capabilities, not rewrite their proven cores.

## Cognitive walkthrough findings — current v0.4.2 UI

The current implementation is functional but exposes several concepts too early. These are redesign targets, not regressions in the verified persistence/core behavior.

| Current surface | First-time risk | Intended correction |
|---|---|---|
| `確認セット` | user must infer what a Set means | user-facing `見たいもの`; keep ReviewSet internal |
| separate Set selector + save/reload Expander | administration appears before the main task is learned | direct “見たいもの” selection; save actions become contextual when modified |
| `フィルターを選ぶ・整理` | frequent ON/OFF and rare destructive management are mixed | lightweight current Filter chooser + separate `フィルターを整理` |
| Filter chips only show active Filters | good for fast OFF, weak for discovering available OFF Filters | add `フィルター n/m` disclosure for current “見たいもの” |
| classification not visible in Set selector | same-named items become hard to identify as catalog grows | picker rows show breadcrumb path + automatic Filter summary |
| no explicit modified-state marker | user must infer whether current choices differ from saved state | visible `変更あり`, save / save-as actions nearby |
| empty first-run surface depends on generic controls | user may not know first step or why defaults exist | contextual empty states and built-in basics as starter content |
| asset delete semantics absent | dead Filters / old “見たいもの” accumulate | dedicated management with used/unused and usage-impact confirmation |

### First-run empty states

Before a target is captured, the interface should teach only the next action:

```text
まだ確認する動画がありません

YMM4で確認したい動画を選択してください。
[ 選択動画を対象に ]
```

After target capture, if no user choice exists yet, starter content can teach the mental model:

```text
何を探しますか？

ゲーム > ゲーム基本
場面切替・暗転・活動変化

配信 > 配信基本
場面切替・音量変化・静かな区間

解説・画面収録 > 基本
場面切替・静止・画面構成変化

[すべての「見たいもの」を見る]
```

Do not block use with a tutorial carousel. The live interface and starter “見たいもの” should explain themselves.

### Explicitly avoid for the first interaction freeze

- mandatory Genre dropdown before every “見たいもの” selection;
- automatic genre classification;
- a new public “Preset” concept beside “見たいもの”;
- exposing internal State/Generic/Learned Filter types in normal Review;
- global threshold/rule-builder panels;
- favorites/recent/hidden systems before catalog scale proves they are needed;
- destructive actions on the Main Review surface;
- icon-only primary actions whose meaning must be memorized.

## Final intent

> Build the interaction model first, validate it with existing capabilities, then use a deliberately small Generic Filter Pack to stress-test it. Once the UI/UX is stable, expand generic/audio/image filters without changing the mental model.

This keeps Navigator extensible without turning it into a large settings application.
