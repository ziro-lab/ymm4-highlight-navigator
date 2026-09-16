# W1 Lab Question Set

W1でLabへ送るQuestionは、Navigator実装全体ではなく以下のhost fact単位に分ける。

## Q1 — Target VideoItem snapshot

Current YMM4 Tool Pluginから、ユーザーが対象として指定したVideoItemについて次を安全に取得する最小surfaceは何か。

- stable-enough item identity
- FilePath / source identity
- ItemStartFrame
- ContentOffset
- Length
- PlaybackRate
- Timeline FPS

Public APIを優先し、足りない場合は必要最小限のprivate/reflection boundaryを特定する。

## Q2 — Source time projection semantics

trim / nonzero ItemStartFrame / nonzero ContentOffset / positive constant PlaybackRateを含むVideoItemについて、Timeline frameとSource timeを相互変換するcurrent semanticsとroundingは何か。

最低ケース:

- 100%
- 50%
- 200%
- same source multiple occurrences
- overlapping source ranges

## Q3 — Timeline seek / jump

Tool Pluginから、Source Episodeを投影したTimeline occurrenceへYMM4 preview/timelineを移動する最小安全surfaceは何か。

Programmatic seekと物理UI操作を混同せず、何を実際に証明したかを分離する。

## Q4 — Explicit Target Set stability

Target Set snapshot後にYMM4 selectionが変わっても、NavigatorのTarget Setを黙って差し替えない実装境界を作れるか。Host側identity lifetimeに注意する。

## Reuse first

Lab commit `84e43c86fac4735225c69a93a001f94ea00a4bda` のrecording-archive probesに同じsurfaceのEvidenceがある場合はそれを先に読む。直接証明していないClaimだけ新規Probeする。

## Expected Lab output

各Questionについて:

```text
PASS / PARTIAL / BLOCKED
exact YMM4 version
public/private surface
observed semantics
minimal reproducible probe
known limits
reopen condition
```

を残し、Navigator側は `docs/LAB_REFERENCES.md` からpinする。
