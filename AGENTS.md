# AGENTS.md

This repository is the product repository for **YMM4見どころナビ**.

Before making a material change, read:

1. `docs/DESIGN.md`
2. `docs/ROADMAP.md`
3. `docs/DEVELOPMENT.md`
4. `docs/NATIVE_VALIDATION.md`
5. `docs/LAB_REFERENCES.md`

## Authority

- Current user intent and explicit decisions are highest authority.
- `docs/DESIGN.md` is the current product-design authority unless the user changes it.
- Lab evidence establishes YMM4 host behavior; it does not silently change product requirements.
- Implementation convenience is not authority to weaken Goal, Scope, safety, data semantics or acceptance.
- Keep `decided != implemented != validated != accepted` explicit.

Preserve these current material decisions unless the user reopens them:

- Product name/scope is **YMM4見どころナビ**; X4 is the initial Profile Group, not a hard-coded product boundary.
- **Primary learning target is scene transition, not static state classification.** Prefer `before → transition → after` temporal behavior and change tendencies.
- **Recall / 見逃し低減 is the primary quality goal.** Precision is secondary; candidate density is the practical guard against unusable over-detection.
- A learning clip may contain multiple transition candidates; never assume the clip center is truth.
- One Filter/Profile may contain multiple transition patterns combined by OR. Do not average materially different entry patterns into one pattern if that reduces recall.
- Human folder classification is Positive membership authority. Absence from another folder is not a Hard Negative.
- After a Filter/Profile exists, replay it over all Positive material. Samples it already catches are `Covered Positive`; misses are `Hard Positive` and get learning priority. Covered Positive still participates in every regression replay.
- Negative authority comes from actual false-positive review feedback such as `これは違う`. Other Profile corpora and folder absence are contrast sources at most, not automatic Hard Negatives.
- Runtime Profiles are non-exclusive and combine by OR/Union. Overlapping review episodes are merged without losing Profile attribution.
- `ANY / N-of-M / ALL` is primarily Profile-internal Condition composition, not exclusive Profile selection.
- **Global Sensitivity is a primary runtime control.** Higher sensitivity means wider recall / more candidates; lower sensitivity means stricter matching / fewer candidates. Sensitivity changes must not trigger re-decode or re-training.
- **Edit-while-review is a first-class runtime workflow.** Split / trim / move / Undo-Redo of Timeline items must not automatically invalidate already-decoded source Feature data when the source file and analyzed source range remain valid. Rebind source-time candidates to the current Timeline instead.
- Candidate identity must preserve an explicit **AnchorSourceTime** in addition to the wider review range. Learned Transition Filters use the actual matched transition center as the anchor; memo capture starts at that anchor with no pre-roll.
- Source identity + source time is not sufficient to identify a Timeline occurrence after copy/paste. Preserve an occurrence lineage/discriminator and never jump to an arbitrary duplicate.
- **「見どころを確保」 is a lightweight memo operation, not final cutting.** Create/reuse a dedicated `見どころメモ` Scene, place each memo at Frame 0 on one separate Layer, default to 30 seconds with user-configurable duration, store hit Filter attribution in Remark, and do not split/re-encode/mutate the review item merely to create the memo.
- Session Feature Index, Persistent Learning Feature Pack and Profile knowledge have separate lifecycles.
- Raw learning video is not the Corpus authority. Preserve enough primitive Feature data to replay Profile changes without raw video.
- No silent self-training. Profile updates require replay/regression, preview and explicit apply/revision; rollback must remain possible.
- Recording Archive is an optional convenience for preparing short clips. Navigator must also accept ordinary videos/folders and must not depend on Archive-specific APIs or manifests.
- Recording Archive remains a separate product responsibility. Do not add classification/learning UI to it from this repository.
- For verified YMM4 4.56.1.0, use the host's bundled FFmpeg capability. Do not ship a private FFmpeg copy or silently fall back to PATH.
- Do **not** reject Navigator solely because the YMM4 build number changed. Use required capabilities while available; if a dependency changed, fail closed at the affected feature and avoid propagating ordinary unhandled failures into the YMM4 process.
- Heavy ML/OCR/Object Detection are not CURRENT requirements.

## Repository boundary: Lab first, product proof here

Canonical YMM4 behavior lab:

`ziro-lab/chat-native-work-lab-001`

Use the Lab when the question is about **what YMM4 itself does**, including:

- host/plugin API availability;
- selection, scene/item identity, timing, seek or rounding semantics;
- version differences;
- undocumented/private surfaces or reflection boundaries;
- package/install behavior that is not yet established;
- a minimal native experiment whose result should be reusable by more than this product.

Do not duplicate a Lab experiment here merely to rediscover the same host fact. When a Lab result is adopted, pin its commit/experiment in `docs/LAB_REFERENCES.md` and state exactly what claim is being reused.

Use this repository's Actions when the question is about **whether YMM4見どころナビ works as a product using already-established host behavior**, including:

- Plugin load and integration smoke;
- Target snapshot -> candidate -> YMM4 Jump end-to-end;
- runtime Profile Union/attribution through the actual Plugin;
- background/cancel integration;
- UI/UX behavior of this Plugin;
- package/install/upgrade behavior of this Plugin;
- release-candidate evidence.

## Action budget discipline

Native Windows/YMM4 Actions are an acceptance resource, not an exploratory REPL.

- Prefer pure deterministic tests for Feature math, Pack schema, Transition extraction, Profile evaluation, Union/Merge, dedupe and regression logic.
- Before adding a native test, name the product claim that pure tests cannot establish.
- Reuse Lab evidence rather than rerunning equivalent host probes.
- Path-gate native workflows and use `concurrency.cancel-in-progress`.
- Keep native runs waypoint/release oriented; do not run the full native suite for documentation-only or pure-engine changes.
- Pin the exact YMM4 version and archive hash used by a native proof.
- Keep evidence artifacts compact and time-limited.

## Implementation shape

Prefer explicit boundaries rather than one large Plugin class:

```text
YMM4 Target Adapter
YMM4 FFmpeg Locator
Source Range Planner
FFmpeg Analysis Backend
Shared Feature Engine
Session Feature Index
Persistent Learning Corpus / Feature Pack
Transition Candidate Extractor
Transition Filter Authoring / Coverage Replay
Profile Evaluator
Episode Union / Merge / Attribution
Review Source Session / Occurrence Rebinding
Timeline Projection / Review Navigator
Highlight Memo Scene Adapter / Capture
Hard Positive / Explicit Negative / Revision Refinement
```

YMM4-private/reflection dependencies belong behind the narrow Target/Host adapter. YMM4 FFmpeg path resolution belongs in the Plugin locator; Core receives executable paths and remains host-independent. Feature extraction, Transition extraction, Corpus replay, Filter authoring, Profile evaluation, Union/Merge and most learning logic should remain YMM4-independent and pure-testable.

## Data and safety

- Never commit private recordings, user YMM4 projects, credentials, machine-specific paths or private Learning Corpus data.
- Prefer generated deterministic media/Feature fixtures for tests.
- Do not treat missing/incompatible Feature fields as zero/negative. Feature schema compatibility must be explicit.
- W3 first implementation is non-destructive. Consumptive Inbox deletion is later and destructive: delete only a source file whose Pack write + reload validation + sample registration commit succeeded, and only when the folder was explicitly configured as consumptive.
- Partial/Error/Cancel samples stay on disk.
- Never delete a file merely because Navigator no longer needs it if an external project may own/reference it.

## UI/UX guardrails

Main Review stays simple:

```text
Filter/Profile ON/OFF
Global Sensitivity
candidate/hit count
Prev / Next / List
見どころを確保 + capture duration
```

Keep Filter internals, Feature thresholds and learning/authoring controls out of the primary review flow unless the user explicitly opens an advanced surface.

- Global Sensitivity must be easy to reach; do not bury it in advanced settings.
- Memo capture duration should be directly understandable in the main Review surface; the default is 30 seconds and the memo begins at the hit anchor, not at the review range pre-roll.
- Memo capture must keep the user's active review Scene unchanged. The dedicated memo Scene is a shelf: all clips at Frame 0, one clip per Layer.
- Show Profile hit total separately from unique review candidate count.
- Do not display uncalibrated similarity as a probability or correctness percentage.
- Batch folder import and Filter-update preview; do not require per-video repetitive metadata entry.
- Prefer user language such as `動画からフィルターを作る`, `拾えなかった教材`, `これは違う`, `フィルターを改善` over internal research terminology.
- Long/background work must show truthful state, progress/liveness, cancel and partial/error outcomes.

## Validation layers

Keep these claims separate:

```text
Lab host/API fact
-> product functional integration
-> product UI/UX acceptance
-> package/install/upgrade acceptance
-> real-user acceptance
```

Passing an earlier layer never implies the next. See `docs/NATIVE_VALIDATION.md`.

## Development workflow

- Work in small waypoint-scoped branches/PRs (`feature/w3-*`, `feature/w5-*`, ...).
- State which DESIGN decisions and Acceptance items the change affects.
- If an unknown YMM4 fact blocks the slice, route only that fact to the Lab; continue independent pure work where safe.
- Do not redesign W1/W2/W4 wholesale because one probe fails. Reopen only the affected assumption/range.
- Update the current docs rather than creating parallel handoff documents that compete for authority.
