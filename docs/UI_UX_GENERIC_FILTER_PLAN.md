# UI/UX + Generic Filter planned direction

Status: **PLANNED / not yet implementation-frozen**

This memo records the product direction decided on 2026-09-22 so that deferred UI/UX and generic-filter work is not lost. Existing W1-R/W4-M/W6 work remains valid; this document changes the preferred sequencing, not the already-verified host/core evidence.

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
対象動画
→ 確認セット / Filter
→ 解析
→ 候補Review
→ 必要なら見どころ確保
```

- Preserve explicit target capture. YMM4 selection changes must not silently change the review target.
- The internal concept remains `Set`, but the user-facing label should be **確認セット**.
- A Set is important, but is not the top-level task. The target comes first.
- Keep Main Review focused on target, active review settings, candidate navigation, and current status.

#### B. Filter / Group / Set semantics and persistence

Freeze the meaning of the three concepts now:

- Filter = detector/query.
- Group = organization only.
- Set = reusable review configuration referencing Filters.

Implement the minimum Set lifecycle now:

- stable Filter IDs; rename/group move must not break Sets;
- built-in Set vs user Set distinction;
- deleted/disabled Filter references degrade visibly instead of silently changing meaning;
- save current review configuration as a Set;
- duplicate a Set cheaply;
- applying/editing a Set must not silently overwrite the saved Set.

Do not add a fourth "genre preset" model. Genre presets are built-in Sets.

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
- 確認セット selector;
- enabled Filter chips / quick ON-OFF;
- add Filter entry;
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

- favorites;
- hidden Filters / Groups;
- recently used;
- "used by these Sets";
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

Keep three roles distinct:

- **Filter** — detector/query that produces candidate hits.
- **Group** — organizational category only; moving/renaming a Group must not change detection semantics.
- **Set** — reusable runtime combination of enabled Filters plus user-facing review settings such as Global Sensitivity. Internal name is `Set`; user-facing UI label is **確認セット**.

Short rule:

> Group organizes. Filter detects. Set defines how the user reviews.

Genre presets should not become a fourth concept. Ship them as built-in Sets.

Examples:

```text
Groups
- 汎用
- ゲーム
- X4
- 配信
- 解説
- 自作

Built-in Sets
- ゲーム基本
- 配信基本
- 解説・画面収録
- シーン切替チェック
```

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
対象 [ 選択動画を対象に ]  recording_01.mp4 / 1個

確認セット [ X4録画チェック ▼ ]   [現在の確認設定を保存]

有効なフィルター
[戦闘開始 ×] [暗転 ×] [高活動→静穏 ×] [+追加]

検出感度
少なく拾う ─────●──── 多く拾う

候補 36件 / ヒット 48
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]

確保時間 [30] 秒
[ ★ 見どころを確保 ]
```

Do not expose Pattern weights, primitive feature names, or many per-filter thresholds on this surface.

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

### 3. Filter Management — organize

Keep management separate from Main Review.

Initial management scope:

- Group
- rename
- duplicate
- disable/delete
- search
- collapse/expand

Later only if real use needs them:

- favorites
- hidden filters/groups
- recent filters
- "used by these Sets"

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

### UX-2 — existing-feature hands-on

Implement/prototype the new flow using existing filters first.

Acceptance path:

```text
対象固定
→ 確認セット選択
→ Filter追加/削除
→ Sensitivity変更
→ 候補理由確認
→ Prev/Next/List
→ 現在の確認設定を保存/再選択
```

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

## Final intent

> Build the interaction model first, validate it with existing capabilities, then use a deliberately small Generic Filter Pack to stress-test it. Once the UI/UX is stable, expand generic/audio/image filters without changing the mental model.

This keeps Navigator extensible without turning it into a large settings application.
