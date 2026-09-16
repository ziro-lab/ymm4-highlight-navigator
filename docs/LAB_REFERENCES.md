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

### Recording Archive host-surface probes — RELATED

- Lab commit: `84e43c86fac4735225c69a93a001f94ea00a4bda`
- Date: 2026-09-16
- Scope in Lab: VideoItem archive surface / Scene archive surface / Project archive save surfaceのnative probes。
- Relevance: Navigator W1のVideoItem snapshotやproject/scene境界と近い領域があるため、同じhost surfaceを再探索する前に確認する。
- Status: **RELATED**。NavigatorのTarget selection / seek / exact source-time projection Claimは、このcommitだけでは採用済みとみなさない。
- Reopen/next step: W1で必要なexact Claimを棚卸しし、既存experimentが直接証明している場合だけADOPTEDへ昇格。不足分だけLab Probeを追加する。

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

## Initial W1 evidence checklist

W1開始前に、以下をこの表へADOPTEDとしてpinできるか確認する。不足しているものだけLabへ送る。

- Tool Pluginから対象VideoItemをsnapshotするcurrent surface;
- Item identity / FilePath / ContentOffset / Length / PlaybackRate / ItemStartFrame / Timeline FPS semantics;
- source time <-> timeline mapping / rounding for trim and positive constant PlaybackRate;
- Timeline seek/jump surface;
- selection changeとexplicit Target Setの境界;
- public APIで足りない場合の最小reflection boundary。

## Product precedent (not Lab authority)

`ziro-lab/ymm4-plugin-garage` のPreview Speed Extensionは、product-side native proofの現行patternとして参考にできる。

2026-09-16のworkflowではYMM4 `4.56.1.0 Lite`をSHA256 pinして一時hostへPluginをinstallし、実YMM4をlaunchして製品claimを検証している。Navigatorではその**workflow pattern**を再利用してよいが、Preview Speed固有のbehavior resultをNavigator host factとして流用しない。
