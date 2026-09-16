# Implementation checkpoint — W1 / W2 foundation

設計全体は v0.4.0。これは初回の動作する実装範囲を記録する文書で、完成版・一般配布版の宣言ではない。

## Current state

- **W1: basic native projection/review spine implemented.** 実YMM4のTool作成、対象取得、定速時間投影、候補巡回までを統合。
- **W2: CPU feature/Pack foundation verified.** 元動画を読まずにPackから同じevaluatorへ再入力できる基盤。
- **W4: basic runtime path implemented.** 複数Profileの独立評価・OR統合・重複整理・no-redecode再検索。
- **W3 / W5 / W6: not implemented yet.** 分類フォルダのCorpus登録、逆分類・学習価値、Profile改善・履歴は次工程。
- **W7: open.** GPU経路、実長尺の性能、実X4のRecall、配布FFmpegの条件、通常 `.ymme` 導入と利用者受入は未完了。

## What actually runs

実YMM4で登録された日本語Toolを開き、選択VideoItemを明示的に対象へ固定する。CPUで背景解析し、汎用Profileを同時評価、候補を時系列に並べて前・次・一覧から移動する。選択変更では対象を変えず、対象の位置・長さ・速度・参照先・ファイル情報が変わった場合は古い対象へのJumpを拒否する。

同じ録画の重複Source Rangeは一つの解析範囲へまとめるが、タイムラインに別々に置かれたItem occurrenceは消さない。ON/OFFと感度変更は既存のFeature Tableを再検索する。クエリ中止・未計算を「0件で成功」や古い計算結果として表示しない。

初期Profile名は **映像の急変／音の強い場面／明るい場面**。まだ学習していない「戦闘」「ステーション」の精度を主張しない。訪問チェックは「候補へJumpした」印であり、映像全体を確認し終えたことを自動判定しない。

## Core verification

- [PR #1](https://github.com/ziro-lab/ymm4-highlight-navigator/pull/1)
- Source head: `9c59979ed5ffc3b9e54d72042b5259d94816deca`
- Actual PR checkout: `01cfa0f6995424282cafa9788b8f6564ff3bfefe`
- [Run 35122141470](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35122141470)
- **27 cases / 11,147 assertions / 0 failures**, build **0 warnings / 0 errors**.
- 11,000 assertions are deterministic randomized interval-coverage checks; the total is not a count of independent feature scenarios.
- Artifact: `10457412473`
- Artifact ZIP SHA256: `c0da26445f8df9d36c8065a53a5632b3ad4bf97a799fd7781749c761a4381e95`

Generated media tests cover full/trimmed FFmpeg video+audio, clocks, raw-video-independent Pack reload, corrupted or oversized Pack rejection, missing-audio compatibility, source preservation on failure, active backend cancellation/termination and half-open intervals. The independent count/required-case gate passed.

The illustrative **30 battle hits + 10 station hits -> 40 total / 36 unique episodes** is a synthetic interval test, not measured X4 detection quality.

## Product native verification

- [PR #2](https://github.com/ziro-lab/ymm4-highlight-navigator/pull/2)
- Source head / actual checkout: `32f2b4b841c3ccf595893354262d30ae7c0d4460`
- Source tree: `606857fa3a92cbd826b7124a1a4a1282347035a9`
- [Run 35126507934](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35126507934), job `104896749162`
- **24 independently required assertions PASS**, product/proof builds **0 warnings / 0 errors**.
- Exact host: YMM4 Lite **4.56.1.0**; observed timeline FPS **60**.
- Host archive SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- Artifact: `10458634589`
- Artifact ZIP SHA256: `92ec4bfdfd21635b060b51de71a6b21205567d1322c70f5cc33c7d605e6539b6`
- Product DLL SHA256: `5599bddc4e16a8b40cf9a83d25f06c18663102ef70ddd35119885ecc27258e92`
- Generated fixture result: **1 shared source range / 2 separate review occurrences / 4 raw hits**. UI heartbeat advanced 10 times during the short analysis; this is not a 3-hour responsiveness benchmark.

The artifact was downloaded, its ZIP checksum and exact tested product DLL checksum matched, all 24 result entries were inspected, and the narrow-layout PNG was visually reviewed. Documentation-only changes after this source do not replace its tested code identity.

The proof is a separate assembly using the exact built distribution Plugin/Core DLLs. No proof-only implementation replaces the production model. The required IDs are enumerated independently in `RunNative.ps1`, and the loaded product DLL hash is checked against the distribution DLL.

Verified categories include explicit target snapshots, same-source 200% and 50% occurrences, host-map projection, exclusive-end rejection, no mutation of selected Item signature fields, original fixture bytes unchanged, empty-capture atomicity, background UI heartbeat, one overlapping decoded range, chronological candidates, exact integer playhead seek, no-redecode toggles/sensitivity, stale-target rejection and cleanup.

Two hardening cases additionally require a cancelled query request to stop exposing the old result and then recover from the existing Feature Table. The deterministic cancellation input is a pre-cancelled token; it does not certify every possible mid-flight UI cancellation race.

The no-redecode proof temporarily makes the **test host's** FFmpeg executable unavailable before changing Profile switches/sensitivity and restoring query results. It does not alter any user's FFmpeg installation.

### UI evidence boundary

The real host creates the actual compiled Tool view. Separately, its 360x480 control layout is measured and important control rectangles must lie completely inside the surface. An opaque control-only PNG is retained for visual review. This is not a full desktop screenshot, physical mouse/keyboard test, every theme/DPI test or user-usability acceptance.

### Failures found and fixed

- `35123554196`, `35124382029`: the test's generic menu traversal did not activate the registered Tool; no native assertions ran. Do not call these successful because builds/artifact upload succeeded.
- `35124984453`: opening the real Tool exposed a read-only `Progress` property incorrectly bound with the control's default TwoWay behavior. Product XAML was fixed to explicit OneWay.
- `35125240537`: the 22-assertion baseline then passed. This is retained as history, not substituted for the final hardening run.

## Lab adoption

Recording Archive timing facts remain pinned to `0b69aed70a3f8a84cc2ede539d794cb4e8108677`. The independent navigation-context experiment tested source `3b52a3acff544ed0ec759d657f48364a415c485c`, run `35120206452`, with **16 required native assertions**. Lab PR #12 merged it; evidence wording was corrected in `5666810491cc7b6c82c60260538f41efa2b7d325`.

That Lab probe proves callback receipt and public Timeline/FPS/selection/playhead model behavior, **not** visible Tool/menu activation. Its original stronger wording was removed rather than treating a callback as UI proof. Product view activation is separately checked here.

See [LAB_REFERENCES.md](LAB_REFERENCES.md) for exact reuse/limitations. Recording Archive code, experiments and product branches were not modified by this Navigator work; only a separate navigation experiment and its own evidence document were added/updated.

## Remaining work / do not infer

1. W3: batch folder intake, Group/Profile membership, dedupe, durable sample registration and recoverable Corpus transactions. Pack serialization alone is not a finished Corpus.
2. W5/W6: frozen-profile ranking, label uncertainty handling, commonality/contrast, proposed revision, replay and explicit apply/rollback. Other folders are not hard negatives.
3. Persisted review presets/Profile configuration and complete project/scene/reload/Undo lifecycle tests.
4. GPU/software fallback validation, realistic long-recording memory/query/ingest benchmarks, other codecs/VFR and decoded preview-frame correspondence.
5. Distribution licensing/version pin for bundled FFmpeg, stable installer layout, install/upgrade proof and hands-on usability/Recall acceptance.

Current code does **not** delete input media. Consumptive Inbox behavior is a separate W3 transaction/ownership feature; neither analysis success nor a checksummed Pack alone permits deleting Archive Project dependencies.

## Resume

Start from the current main and [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md). Reuse the existing Core, adapter and regression fixtures rather than recreate W1/W2. Keep pure changes out of Windows native runs unless their integration boundary needs revalidation. Generated source media and runtime/backend binaries stay out of repository history.
