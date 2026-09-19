# Native Validation Policy

YMM4見どころナビでは、**YMM4本体の挙動調査**と**製品Pluginとしての実機確認**を分離する。これはActions使用量の節約だけでなく、Evidenceの意味を混ぜないための境界でもある。

## Claim layers

```text
1. YMM4 host/API behavior           -> chat-native-work-lab-001
2. Navigator functional integration -> this repository native smoke
3. Navigator UI/UX acceptance       -> this repository native UI proof + human audit
4. package/install/upgrade behavior  -> this repository release proof
5. real-user acceptance              -> actual editing environment
```

前段PASSを後段PASSとみなさない。

## Current pinned host precedent

2026-09-16時点でLab/Garageのcurrent native workflowsは、少なくとも次のexact hostを使っている。

```text
YMM4: 4.56.1.0 Lite
ZIP SHA256: 49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a
```

Navigator最初のnative laneも、別versionを選ぶMaterial reasonが出るまではこのexact hostを第一候補とする。Version更新時はURLだけでなくhashも更新し、Lab evidenceのapplicabilityを再確認する。

## Action-budget policy

### A. Pure CI — cheap/default

対象:

- Feature math;
- Pack schema/serialization;
- Profile evaluator;
- Corpus replay;
- union/merge/attribution;
- reverse classification / hard-example ranking;
- deterministic FFmpeg fixture checks where host UI is不要。

CodeができたらPR/pushでpath-gated実行してよい。可能ならnative Windows hostを不要にする。

### B. Lab native probe — host fact only

YMM4 API/behavior/version差分が未知ならNavigator Actionを追加せずLabへ送る。

Labの結果は `docs/LAB_REFERENCES.md` に採用Claimをpinする。

### C. Navigator native smoke — waypoint acceptance

W1/W4等、製品統合でしか証明できないclaimだけを確認する。

Native workflowを作る時は:

- `windows-latest`;
- exact YMM4 URL + SHA256 pin;
- `permissions: contents: read`;
- `persist-credentials: false`;
- short timeout;
- path filter;
- `workflow_dispatch`;
- `concurrency` + `cancel-in-progress: true`;
- compact artifact retention;
- private recordingsではなくgenerated fixture;

を基本形にする。

W1/W3/W5/W1-Rの製品native laneは実装済み。W1-Rから同一checkoutのPure + media + Learning回帰gateを先行させ、成功時だけWindowsを起動する。未実装機能用の空workflowや、Lab host事実の再探索workflowは追加しない。

### D. Release proof — sparse/manual

Release Candidate / distribution checkpointだけで実行する。通常commitごとに回さない。

最低限別Claimとして:

- distribution build;
- real YMM4 load/smoke;
- UI/UX proof;
- package archive layout;
- stable internal plugin root;
- packaged DLL hash == native-smoked distribution DLL hash;
- evidence manifest validation;

を確認する。

## UI/UX native proof

UIが「存在する」ことと「使える」ことを分ける。

重要Controlについて可能なら:

```text
semantic state / visibility
-> ActualWidth / ActualHeight > 0
-> tested viewport内へboundsが完全に収まる
-> screenshot for human audit
```

Progressive disclosureはControl存在ではなくtransitionをassertする。

Navigation/Profile切替が非破壊とClaimする場合は、host/project semantic signatureを前後比較する。

## Package / install / upgrade proof

直接DLLを一時YMM4へコピーしてloadできても、`.ymme`のinstall/upgrade correctnessは未証明。

Release proofでは:

- outer package versionと独立したstable internal plugin root;
- archive entry path allowlist;
- flat-root DLLやversion-named internal rootをreject;
- packaged DLL hash consistency;
- old Candidateのbad layoutがあればcleanup/migration guidance;

を検証する。

想定shape:

```text
Ymm4HighlightNavigator-vX.Y.Z.ymme
└─ Ymm4HighlightNavigator/
   ├─ Ymm4HighlightNavigator.dll
   └─ allowed dependencies...
```

Exact package shapeは実装時にcurrent YMM4 behaviorをLab evidenceで確認してから固定する。

## Evidence must not certify itself

高価値のrelease manifestはproducerの`PASS`だけをAuthorityにしない。

```text
producer emits evidence
-> independent consumer validates identity/version/stages/requirements/failure markers
-> negative fixtures prove weakened evidence is rejected
```

Negative fixture候補:

- missing required stage;
- duplicate requirement id;
- stale manifest version;
- explicit failed requirement;
- wrong host identity/version;
- required stage list weakened;
- native failure marker present;
- packaged DLL hash mismatch。

## Product-native acceptance candidates by waypoint

### W1

- Plugin load;
- Target snapshot;
- dummy candidate;
- timeline jump accuracy;
- same-source multiple occurrence;
- supported PlaybackRate/offset cases。

### W4

- Profile ON/OFF changes candidate queue without re-decode;
- overlapping hits keep attribution;
- Prev/Next/List -> correct YMM4 occurrence;
- UI remains responsive during query/background work。

### W1-R — edit-time rebinding

- one analysis / one Feature Index remains reusable after real host split;
- head/tail trim only invalidates candidates whose AnchorSourceTime is no longer contained;
- moved split piece is resolved at its new Frame without re-decode;
- Undo/Redo semantic state can be rebound from current Timeline;
- copied identical source occurrence is not chosen arbitrarily;
- ambiguous replacement fails closed rather than jumping to the wrong occurrence;
- source file mutation still invalidates source Feature authority.

Use the Lab's already-adopted split/trim/move/duplicate/UndoRedo facts. Product native smoke proves only Navigator integration against those facts.

W1-Rのrequired-case一覧は `tests/Ymm4HighlightNavigator.Plugin.Tests/required-native-cases.json`。native runnerは結果全ID・件数・checkout・exact distribution DLL・host backend paths・process生存を独立照合する。`rebinding-summary.json` の完了flag、decode call count、unhandled countも照合する。生成24秒素材でSplit/Trim/Move/Copy/Paste/実emit済みUndoRedo commandを使う。各Nextのsource anchor/current Frame/既読をtraceへ残す。編集中にテスト用ffmpeg.exeを一時退避し、解析Backend呼出回数が増えないことを確認する。

旧 `stale_target_rejected_atomically` は削除して成功数を減らすのでなく、`timeline_move_rebind` と `source_mutation_rejected_atomically` へ意味分解した。曖昧replacementの決定的分岐はCore Pureで、実コピーの誤Jump防止は製品nativeで確認する。失敗runを成功sourceの証拠へ混ぜない。

現行のtested source/runと限界は [W1_R_CHECKPOINT.md](W1_R_CHECKPOINT.md)。物理キー/マウスgesture、通常install/upgrade、ユーザー受入の完了はclaimしない。

### W4-M — Highlight memo capture

- `見どころメモ` Scene create/reuse through the product adapter;
- active Review Scene remains unchanged;
- selected Candidate AnchorSourceTime becomes memo ContentOffset with no pre-roll;
- memo clips all start at Frame0 and occupy separate Layers;
- default30s and another configured duration both work;
- source-end shortening is safe;
- Remark keeps hit Filter attribution;
- same source+anchor duplicate capture is rejected/no-op;
- duplicate memo Scene name fails closed;
- source Review Item semantic signature is unchanged;
- save/reload keeps memo Scene / Guid / Layer / Frame / ContentOffset / Length / Remark.

UI proof should include the capture-duration control and `見どころを確保` button inside the tested viewport. It need not claim physical Layer ON/OFF interaction unless separately exercised.
### W7

- full background analysis/cancel;
- GPU -> software fallback boundary;
- compact distribution;
- `.ymme` install/upgrade;
- UI/UX viewport proof;
- evidence manifest validation。
