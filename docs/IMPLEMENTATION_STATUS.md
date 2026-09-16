# Implementation checkpoint — W1 / W2 foundation

設計全体は **v0.4.1 Transition Filter Learning / Recall-First Review**。これは現在実装済みのcheckpointと、次に実装する方向を分けて記録する文書であり、完成版・一般配布版の宣言ではない。

## Current state

- **W1: basic native projection/review spine implemented.** 実YMM4のTool作成、対象取得、定速時間投影、候補巡回までを統合。
- **W2: CPU feature/Pack foundation verified.** 元動画を読まずにPackから同じevaluatorへ再入力できる基盤。YMM4 Plugin runtimeはhost同梱FFmpegをpublic locator capability経由で利用する。
- **W4: basic runtime path implemented.** 複数Profileの独立評価・OR統合・重複整理・no-redecode再検索、Global Sensitivity。
- **W3 / W5 / W6: not implemented yet.** v0.4.1ではW3をCorpus+Transition抽出、W5をTransition Filter生成+Positive coverage、W6をHard Positive / Explicit Negative / revision改善として進める。
- **W7: open.** GPU経路、実長尺の性能、実X4のRecall/candidate density、通常 `.ymme` 導入/upgrade、利用者受入は未完了。

## Current design direction — decided, not implemented

今後のPrimary Goalは **場面切り替わりを高速に拾い、見逃しを減らすこと**。

- 教材Clipは普通の動画/FolderからImport可能にする。録画アーカイブは教材切り出しを楽にする任意ツールであり、Navigatorの前提ではない。
- `before → transition → after` の時間方向変化を主な学習対象にする。Clip中央を正解と仮定しない。
- 1 Filterは複数Transition PatternをORで持てる。
- 現Filterで拾える教材 = `Covered Positive`、拾えない教材 = `Hard Positive`。Hard Positiveを改善優先し、Covered PositiveはRegressionへ残す。
- Negative Authorityは実Runtimeで人間が`これは違う`とした誤検出。別Folder/別Profile/未所属を自動Hard Negative化しない。
- RuntimeのGlobal Sensitivityは常用Primary control。高いほど広く拾い、低いほど候補を絞る。変更で再Decode/再学習しない。
- Precision最大化よりRecallを優先するが、candidate densityが高すぎてReview Reductionを失うFilterは実用不可として評価する。

この節は**設計済みであり実装済みではない**。実装手順は [ROADMAP.md](ROADMAP.md) / [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md) を参照。

## What actually runs

実YMM4で登録された日本語Toolを開き、選択VideoItemを明示的に対象へ固定する。CPUで背景解析し、汎用Profileを同時評価、候補を時系列に並べて前・次・一覧から移動する。選択変更では対象を変えず、対象の位置・長さ・速度・参照先・ファイル情報が変わった場合は古い対象へのJumpを拒否する。

同じ録画の重複Source Rangeは一つの解析範囲へまとめるが、タイムラインに別々に置かれたItem occurrenceは消さない。ON/OFFと感度変更は既存のFeature Tableを再検索する。クエリ中止・未計算を「0件で成功」や古い計算結果として表示しない。

初期Profile名は **映像の急変／音の強い場面／明るい場面**。まだ学習していない「戦闘」「ステーション」等の精度を主張しない。訪問チェックは「候補へJumpした」印であり、映像全体を確認し終えたことを自動判定しない。

## Core verification

- [PR #1](https://github.com/ziro-lab/ymm4-highlight-navigator/pull/1)
- Source head: `9c59979ed5ffc3b9e54d72042b5259d94816deca`
- Actual PR checkout: `01cfa0f6995424282cafa9788b8f6564ff3bfefe`
- [Run 35122141470](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35122141470)
- **27 cases / 11,147 assertions / 0 failures**, build **0 warnings / 0 errors**.
- 11,000 assertions are deterministic randomized interval-coverage checks; the total is not a count of independent feature scenarios。
- Artifact: `10457412473`
- Artifact ZIP SHA256: `c0da26445f8df9d36c8065a53a5632b3ad4bf97a799fd7781749c761a4381e95`

Generated media tests cover full/trimmed FFmpeg video+audio, clocks, raw-video-independent Pack reload, corrupted or oversized Pack rejection, missing-audio compatibility, source preservation on failure, active backend cancellation/termination and half-open intervals. The independent count/required-case gate passed。

The illustrative **30 battle hits + 10 station hits -> 40 total / 36 unique episodes** is a synthetic interval test, not measured X4 detection quality。

## Product native verification — current

Latest compatibility-guard regression:

- Tested source / checkout: `a74d04e5a96c9cc85ad23af986f65bc90635c3d3`
- Run `35131453875`, job `104913201144`
- Exact regression host: YMM4 Lite **4.56.1.0**
- Product/proof builds: **0 warnings / 0 errors**
- **27 independently required assertions PASS**
- Product DLL SHA256: `77224A17056AB255B18A22D7503704ACD37D35A0FFE05D5632AA23A3CEDF0C98`
- Artifact: `10460733639`; ZIP SHA256 `f07fcae5ca4fb6c01893caddf9775381f659a0280463c1a9e1584776a456b4d2`

This run preserves the existing target/projection/background/no-redecode/backend assertions after removing the exact YMM4-version refusal and making optional FFmpeg host dependency resolution lazy/capability-based.

Previous bundled-backend integration checkpoint:

- Tested source / checkout: `61126ef4be5b630118ae574823c9ba26d7c07f51`
- [Run 35129626271](https://github.com/ziro-lab/ymm4-highlight-navigator/actions/runs/35129626271), job `104907097095`
- Host archive SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- **27 independently required assertions PASS**
- Product DLL SHA256: `2BAA88D9F9DD06E05AC40A6330534A587557CEAB12052406477125EC49B4DF50`
- Artifact: `10461030953`; ZIP SHA256 `eb13ebfed6605dd49cb358005c40fc28c60f791b43b52fc58cbbc82ba694968c`

Backend assertions prove:

```text
YMM4 bundled ffmpeg.exe resolves
sibling ffprobe.exe resolves
resolved backend is outside Navigator Plugin directory
no Navigator/private tools folder is installed
real analysis succeeds with those paths
```

Navigator does not copy or bundle its own FFmpeg and does not silently fall back to PATH. Core stays host-independent because it receives explicit executable paths.

The no-redecode proof temporarily hides the **disposable CI host's bundled** `ffmpeg.exe`, changes Profile switches/sensitivity, verifies re-query and then restores the executable. This never touches a user's installation。

### Previous verified baseline and failure history

- `35126507934`: pre-locator baseline, 24 required assertions PASS. It used a temporary Plugin-private test backend and is superseded for backend ownership/path claims, while its other W1/W2 evidence remains historical corroboration。
- `35129433744`: failed before product build because the workflow used PowerShell's reserved `$Host` variable. Fixed by renaming it; no product claim was inferred。
- `35129534277`: the YMM4-bundled FFmpeg was correctly found, but the test tried to encode an H.264 fixture with unavailable `libx264`. This was a fixture assumption, not a Navigator decode failure. The fixture was changed to deterministic FFV1/PCM MKV generated by the same bundled backend。
- `35129626271`: bundled locator/product integration PASS。

Earlier Tool integration failures/fixes remain relevant: runs `35123554196` / `35124382029` did not activate the registered Tool; run `35124984453` exposed a read-only Progress binding issue; `35125240537` passed the corrected baseline before later hardening。

### UI evidence boundary

The real host creates the actual compiled Tool view. Separately, its 360x480 control layout is measured and important control rectangles must lie completely inside the surface. An opaque control-only PNG is retained for visual review. This is not a full desktop screenshot, physical mouse/keyboard test, every theme/DPI test or user-usability acceptance。

## Lab adoption

Recording Archive timing facts remain pinned to `0b69aed70a3f8a84cc2ede539d794cb4e8108677`. Navigation-context source `3b52a3acff544ed0ec759d657f48364a415c485c`, run `35120206452`, passed16 required native assertions; evidence wording was corrected in `5666810491cc7b6c82c60260538f41efa2b7d325` so callback receipt is not misreported as visible UI proof。

YMM4 bundled FFmpeg evidence is adopted from Lab merge `c1acd43297f9a1c2dd5053e7c9667fb84fa237b5`: source `21090626faeb7985f964a26c4a57b8f301255a88`, run `35123682432`, job `104887398283`, artifact `10458179237` / SHA256 `c3b8acfc064388731ad33a6493ed07bf53f43abed5ec1b03d97116b54b9559cb`。Lab proved the public locator and bundled sibling; product native runs separately prove Navigator uses them。

Recording Archive product/probe code is not a Navigator dependency and was not modified by these product changes。詳細は [LAB_REFERENCES.md](LAB_REFERENCES.md)。

## Remaining work / do not infer

1. **W3-A:** non-destructive batch Corpus intake、dedupe、Positive memberships、durable registration transaction。
2. **W3-B:** raw-video-free Corpus reload/replay。
3. **W3-C:** per-clip Transition candidate extraction、before/transition/after windows。
4. **W5:** Transition Filter Candidate generation、multiple Pattern OR、Covered/Hard Positive replay、Runtime registration、Sensitivity接続。
5. **W6:** `これは違う` Explicit Negative、Contrast refinement、Positive regression、candidate-density gate、revision/rollback。
6. Persisted Review Presets / Filter configuration and complete project/scene/reload/Undo lifecycle tests。
7. GPU/software fallback validation、realistic long-recording memory/query/ingest benchmarks、other codecs/VFR、decoded preview-frame correspondence。
8. Stable `.ymme` installer layout、install/upgrade proof、supported YMM4 update compatibility、hands-on Recall/candidate-density acceptance。

Current code does **not** implement Learning Corpus intake, Transition Filter authoring, Explicit Negative feedback or input-video deletion. Do not describe v0.4.1 decisions as implemented behavior。

## Resume

Start from current main and [IMPLEMENTATION_KICKOFF.md](IMPLEMENTATION_KICKOFF.md). Reuse the existing Core, adapter, YMM4 backend locator, no-redecode runtime and regression fixtures. First implementation target is **W3-A → W3-B → W3-C**. Pure Corpus/Transition work does not need Windows native runs unless it changes a host/product integration boundary. Generated source media and runtime binaries stay out of repository history。
