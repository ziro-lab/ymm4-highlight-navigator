# Lab Evidence References

Canonical Lab: [ziro-lab/chat-native-work-lab-001](https://github.com/ziro-lab/chat-native-work-lab-001)。履歴の丸ごとコピーではなく、Navigatorが実際に採用するhost Claimの索引。

Claimごとに実験、tested source/host、採用範囲、未証明部分、再確認条件を分ける。`ADOPTED` はその狭い事実の採用であり、Navigatorの製品・UX・配布の合格を意味しない。

## Recording Archive timing — ADOPTED

- Lab evidence commit: `0b69aed70a3f8a84cc2ede539d794cb4e8108677`
- Exact host: YMM4 Lite **4.56.1.0**
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- Timing source head: `7450eb3d9b92cbb67c40f2ceac10f80b0ab6c5bd`
- [PlaybackRateMap experiment](https://github.com/ziro-lab/chat-native-work-lab-001/blob/0b69aed70a3f8a84cc2ede539d794cb4e8108677/experiments/ymm4/playbackratemap-source-time/README.md)
- Timing run: `35113196298`; artifact `10452663798`; ZIP SHA256 `56e7c20f619dbba448c8272a1997862aae94b1491c9077b783e29308fc6f51de`

採用する内容:

1. current速度surfaceは `PlaybackRate2`。legacy `BaseItem.PlaybackRate`は使わない。
2. native source-time authorityは `BaseItem.PlaybackRateMap`。non-public getterだけをversion-pinned `TargetAdapter`内へ隔離。
3. tested constant positive 50/100/200%では `sourceTime = ContentOffset + itemTime * rate / 100`。ContentOffset自体は倍率を掛けない。
4. inverse lookupはItem終端を除外。Lengthがframesなら、item-local secondsの区間は `[0, Length/FPS)`。単位を混同しない。
5. `ContentLength` / `OriginalContentLength`はtested fixtureではmedia durationのまま。使用Source durationをそこから推定しない。native mapへmedia-duration引数として渡すこととは区別する。
6. constant50/100/200%とnonzero ContentOffsetの保存・再読込保持。

Source時間計算の未知探索を再実行せず、製品での使用箇所をintegration regressionする。

### Related archive evidence, not a Navigator save requirement

同commitのintegrated archive run `35113196291` / artifact `10453342033` はdetached Projectのrelink/save/reloadと非破壊を確認している。Navigatorの通常Reviewにはこの保存機能を追加しない。初期surface commit `84e43c86fac4735225c69a93a001f94ea00a4bda` は履歴参照として残すが、timingについては後続の直接検証を優先する。

## Navigation target context — ADOPTED for basic W1

- [Experiment and corrected boundaries](https://github.com/ziro-lab/chat-native-work-lab-001/blob/5666810491cc7b6c82c60260538f41efa2b7d325/experiments/ymm4/navigation-target-context/README.md)
- Tested source/checkout: `3b52a3acff544ed0ec759d657f48364a415c485c`
- Source tree: `19d3b10569103addb79c63653ed0c8e49f85b1d4`
- Evidence text correction: `5666810491cc7b6c82c60260538f41efa2b7d325`
- Lab PR: #12
- Run: `35120206452`; job `104875765200`
- Artifact: `10457134077`; ZIP SHA256 `78b2b65c8ea6e7438e0f79775388d2d5a6d8159df25a69f0038ec5650931d2ab`
- Exact host: YMM4 Lite4.56.1.0, observed60fps; **16 required assertions PASS**.

採用する内容:

- host-created Tool modelへの `SetTimelineToolInfo` callback receipt;
- public `TimelineToolInfo.Timeline`;
- public `Timeline.VideoInfo.FPS`;
- public `Timeline.SelectedItems`と選択通知;
- 同じSourceの異なるVideoItemは別object reference。選択をクリアしてもsnapshotした参照とTimeline membershipが残る;
- public integer `Timeline.CurrentFrame`のset/readback;
- fractional source-time逆変換がitem-local0.5173秒を保持する。

identityは **session object reference + Navigator側snapshot Guid**。永続IDではない。timeline変更で捨て、参照が現Timelineに残っていること、位置/長さ/速度/参照先/素材情報がsnapshotに一致することをJump前に検査する。

`ceil(itemLocalSeconds * FPS)`で最初の表現可能frameを選ぶのは **Navigatorのrounding policy**。hostの一般的roundingとして主張しない。選んだframeが元候補とhalf-open Item範囲に入ることをnative mapで再確認する。

### Important corrected claim

元のLab proseにあった「Tool menu invocationを確認」は、`Info != null`だけのassertionでは支持されなかったため修正済み。callback受信と可視UIの作成は別Claim。ToolAreaViewModelの表示と製品Viewの実体はNavigator native checkpointで別途確認する。Labの古い強い表現を実装の根拠としてコピーしない。

## W1 status after implementation

| Claim | Status / boundary |
|---|---|
| FilePath/Frame/Length/ContentOffset/PlaybackRate2 | ADOPTED; TargetAdapterで使用 |
| Timeline FPS / selected VideoItems / integer playhead | ADOPTED on4.56.1.0 / observed60fps |
| Native constant-positive source mapping | ADOPTED; raw mathだけをhost証明の代用にしない |
| Explicit Target Set | implemented; selection-independent snapshot / stale rejection product tests |
| Fractional rounding | product ceiling policy + native map validation |
| Visible product Tool / compiled XAML | product native checkpoint; Lab callbackのみから推定しない |
| Reload/Undo/scene-switch完全lifecycle | broader acceptance still open; referencesを永続化しない |
| Physical input / decoded preview-frame correspondence | NOT PROVEN |
| Variable/reverse playback, other host versions | OUT OF VERIFIED SCOPE |

再確認条件はhost版変更、map/surface変更、対応速度範囲拡張、product regressionとの矛盾。Q1〜Q4をまた一式新規Labへ投げない。詳細は [W1_LAB_QUESTIONS.md](W1_LAB_QUESTIONS.md)、製品証拠は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Validation practice — ADOPTED

Lab commit `46530ac8de801d22683ebd4587cccea6c175cc28`:
- host事実 / product機能 / UIUX / package-install / user acceptanceを分ける;
- IsVisibleだけでなくviewport内の実寸・座標を確認する;
- packageの内部rootと実際に検証したDLL hashを確認する;
- producerのPASS自己申告だけでなく独立consumerとnegative testsを使う。

[Native policy](NATIVE_VALIDATION.md)へ継承する。GarageのPreview Speed Extensionはworkflowの参考であり、その機能PASSをNavigatorの証明に流用しない。
