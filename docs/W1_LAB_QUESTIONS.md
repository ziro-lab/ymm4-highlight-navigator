# W1 Lab Question Set

W1でLabへ送るQuestionは、Navigator実装全体ではなく**まだ未確認のhost factだけ**に絞る。

Recording Archive側のLab検証が進み、YMM4 v4.56.1.0について `PlaybackRate2` / `PlaybackRateMap` / constant positive 50・100・200% source-time behaviorは既にNavigatorで採用できるEvidenceになった。詳細は `docs/LAB_REFERENCES.md` をAuthority索引とする。

## Already adopted — do not re-probe as discovery

以下はW1の新規Questionではない。

- current rate surfaceは `PlaybackRate2`。legacy `BaseItem.PlaybackRate` は使わない;
- source-time authorityはYMM4 native `PlaybackRateMap`;
- constant positive rateで:

  ```text
  sourceTime = ContentOffset + itemTime * rate / 100
  ```

- `ContentOffset`へrateを掛けない;
- inverse lookupはtested caseでhalf-open `[0, Length)`;
- `ContentLength`はSource Range consumed lengthとして使わない;
- 50 / 100 / 200% `PlaybackRate2` + non-zero `ContentOffset` はnative save/reloadで保持される。

この範囲を確認するためだけの重複Actionは回さない。Product側では採用したbehaviorをintegration regressionとして再確認してよいが、Lab discoveryとproduct acceptanceを混同しない。

## Q1 — Target VideoItem snapshot / identity

Current YMM4 Tool Pluginから、ユーザーが対象として指定したVideoItemを**Target Setとして固定**するための最小surfaceは何か。

既に再探索しなくてよいsurface:

- FilePath
- ContentOffset
- Frame
- Length
- PlaybackRate2

今回確認するもの:

- stable-enough item identity / occurrence identity;
- Timeline identity;
- Timeline FPSをどこからAuthorityとして取得するか;
- selectionから明示Target Setへsnapshotする最小public surface;
- project / scene / selection lifecycleでidentityがどこまで有効か。

Public APIを優先し、足りない場合はTarget Adapter内へ必要最小限のprivate/reflection boundaryを特定する。

## Q2 — Timeline frame -> item-local time / rounding

`PlaybackRateMap`による **item-local time <-> source time** は採用済み。

残っているQuestionは、YMM4 Timelineのabsolute frameからitem-local timeへ落とす境界とrounding。

最低ケース:

- nonzero `Item.Frame` / ItemStartFrame;
- nonzero `ContentOffset`;
- 100 / 50 / 200% constant positive `PlaybackRate2`;
- item start / interior / final valid frame;
- same source multiple occurrences;
- overlapping source ranges。

確認したいこと:

```text
Timeline absolute frame
→ item-local frame/time
→ PlaybackRateMap.GetSourceTime(...)
```

とinverse projectionのexact boundary / rounding。

`ContentLength`からusage durationを逆算しない。

## Q3 — Timeline seek / jump

Tool Pluginから、Source Episodeを投影したTimeline occurrenceへYMM4 preview/timelineを移動する最小安全surfaceは何か。

確認するもの:

- target Timelineを選択 / activateする必要があるか;
- current frame / playheadを設定するpublic surface;
- scene切替を伴う場合の最小sequence;
- programmatic seek後に期待frameへ到達したことをmachine-checkする方法。

Programmatic seekと物理UI操作を混同せず、何を実際に証明したかを分離する。

## Q4 — Explicit Target Set stability

Target Set snapshot後にYMM4 selectionが変わっても、NavigatorのTarget Setを黙って差し替えない実装境界を作れるか。

確認するもの:

- selection changeとTarget snapshot lifetimeを分離できるか;
- scene switch / project reloadでstale identityを検出できるか;
- stale Targetを別Itemへ誤投影しないための最小signature / invalidation条件。

Host側identity lifetimeに注意する。

## Reuse first

Lab evidenceの優先順:

1. `0b69aed70a3f8a84cc2ede539d794cb4e8108677` — Recording Archive native timing / integrated spine
2. `84e43c86fac4735225c69a93a001f94ea00a4bda` — initial host-surface discovery

Q1〜Q4のうち、既存experimentが直接証明している部分は再Probeしない。**Navigator固有で不足するClaimだけ**新規Probeする。

## Expected Lab output

各未解決Questionについて:

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

## CURRENT W1 Lab budget target

新規Lab Actionの主対象は次の3系統へ絞る。

```text
Target identity / snapshot lifetime
Timeline absolute frame <-> item-local time / rounding
Timeline seek / jump
```

PlaybackRate2 / PlaybackRateMap / 50・100・200% constant positive source-time mappingの再発見にはActionを使わない。
