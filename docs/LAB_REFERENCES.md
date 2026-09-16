# Lab Evidence References

Canonical Lab: [`ziro-lab/chat-native-work-lab-001`](https://github.com/ziro-lab/chat-native-work-lab-001)

この文書はLabの履歴コピーではなく、**Navigatorが実際に依存するYMM4 host Claimへの索引**にする。

## Rule

新しいLab resultを採用するとき、次を記録する。

```text
Claim
Lab commit / experiment
Tested host/version/conditions
Navigator dependency
Status: ADOPTED / RELATED / SUPERSEDED
Reopen condition
```

`RELATED` は「読む価値がある」だけで、NavigatorのClaim Authorityとして採用済みという意味ではない。

## Current references

### Recording Archive initial host-surface probes — SUPERSEDED / RELATED

- Lab commit: `84e43c86fac4735225c69a93a001f94ea00a4bda`
- Date: 2026-09-16
- Scope in Lab: VideoItem archive surface / Scene archive surface / Project archive save surfaceの初期native probes。
- Status: **SUPERSEDED for timing claims / RELATED for surface discovery**。
- Superseded by: `0b69aed70a3f8a84cc2ede539d794cb4e8108677` のrecording-archive evidence chain。
- Navigator dependency: W1で同じVideoItem/timing surfaceを再探索しないための履歴参照。

### Recording Archive native timing evidence — ADOPTED FOR W1

- Lab commit: `0b69aed70a3f8a84cc2ede539d794cb4e8108677`
- Date: 2026-09-17 JST
- Exact host: **YMM4 Lite v4.56.1.0**
- Integrated proof source head: `7450eb3d9b92cbb67c40f2ceac10f80b0ab6c5bd`
- Integrated workflow run: `35113196291`
- Scope in Lab: VideoItem timing surface、`PlaybackRate2`、`PlaybackRateMap`、50/100/200% constant positive rate、ContentOffset、ContentLength falsification、scene dependency、detached serialization、integrated archive spine。

Navigatorが採用するClaim:

1. **`PlaybackRate2` がcurrent rate surface**。legacy `BaseItem.PlaybackRate` はobsoleteとして扱う。
2. Native video playback pathのsource-time authorityは **`BaseItem.PlaybackRateMap`**。getterはnon-publicなので、必要ならversion-pinned Target Adapter内の狭いreflection boundaryで取得する。
3. constant positive 50 / 100 / 200%について、host mapのforward mappingは:

   ```text
   sourceTime = ContentOffset + itemTime * rate / 100
   ```

   `ContentOffset` 自体へrateを掛けない。
4. `PlaybackRateMap.FindFirstTimeForSourceTime()` のinverse lookupは、tested constant-rate caseでitem終端を含まない **half-open `[0, Length)`** として扱う。
5. `ContentLength` / `OriginalContentLength` はtested fixtureでmedia durationのまま。**Source Range consumed lengthの推定には使わない**。
6. `PlaybackRate2` とnon-zero `ContentOffset` は50 / 100 / 200%のnative project save/reload roundtripで保持される。
7. Recording Archive integrated proofでは200% / 50%のVideoItemを含むdetached project relink/save/reloadが成立し、live project / source `.ymmp` は非破壊だった。これはNavigatorの通常Reviewで直接必要な保存Claimではないが、採用timing surfaceの統合実績としてRELATED。

Status: **ADOPTED** for W1 source-time core semantics on the exact tested host and constant positive rates。

Not adopted / still open:

- YMM4 Timeline absolute frame -> item-local timeへ落とすexact rounding / boundary semantics;
- Navigatorのexplicit Target Set用stable-enough item identity;
- Tool PluginからTimelineへseek/jumpする最小安全surface;
- selection change後もTarget Setを固定するlifecycle boundary;
- animated / variable / reverse playback。

Reopen condition:

- supported YMM4 versionをv4.56.1.0から上げる;
- `PlaybackRateMap` surfaceまたはtiming behaviorが変わる;
- animated / non-monotonic / reverse rateをCURRENTへ昇格する;
- product native regressionがLab Claimと矛盾する。

### YMM4 downstream evidence policy — ADOPTED AS VALIDATION PRACTICE

- Lab commit: `46530ac8de801d22683ebd4587cccea6c175cc28`
- Date: 2026-09-16
- Adopted practice:
  - functional integrationとUI/UX acceptanceを別Claimにする;
  - logical visibilityだけでなく重要Controlのviewport containmentを確認する;
  - plugin loadとpackage/install/upgradeを別Claimにする;
  - release evidence producerの自己申告PASSだけに依存せず、独立consumer + negative fixturesで検証する;
  - host/API -> product functional -> UI/UX -> package/install -> human acceptanceのClaim層を分ける。
- Navigator dependency: `docs/NATIVE_VALIDATION.md` のrelease/native policy。
- Status: **ADOPTED**。
- Reopen condition: Labのevidence policyが後続のverified feedbackで置換された場合。

## W1 evidence status after Recording Archive feedback

実装前の棚卸し結果:

| W1 Claim | Status | Next action |
|---|---|---|
| VideoItem `FilePath / ContentOffset / Frame / Length / PlaybackRate2` surface | **PARTIAL / reuse first** | surface再探索はしない。Navigator Target snapshotとして不足するidentity/FPSだけ確認 |
| item-local time <-> source time, positive constant 50/100/200% | **ADOPTED** | `PlaybackRateMap`をAuthorityとしてTarget Adapterへ隔離 |
| `ContentLength`をSource Rangeへ使えるか | **REJECTED** | planner inputに使わない |
| Timeline absolute frame -> item-local time / rounding | **OPEN** | Labで不足ClaimだけProbe |
| Timeline seek/jump surface | **OPEN** | Labで最小Probe |
| selection change / explicit Target Set stability | **OPEN** | Navigator固有identity/lifecycle Probe |
| variable / reverse playback | **OUT OF CURRENT** | CURRENTへ昇格するまでProbe不要 |

W1では、既にADOPTEDになった50/100/200% source-time behaviorをNavigator側で再発見するためのLab Actionを回さない。

## Product precedent (not Lab authority)

`ziro-lab/ymm4-plugin-garage` のPreview Speed Extensionは、product-side native proofの現行patternとして参考にできる。

2026-09-16のworkflowではYMM4 `4.56.1.0 Lite`をSHA256 pinして一時hostへPluginをinstallし、実YMM4をlaunchして製品claimを検証している。Navigatorではその**workflow pattern**を再利用してよいが、Preview Speed固有のbehavior resultをNavigator host factとして流用しない。
