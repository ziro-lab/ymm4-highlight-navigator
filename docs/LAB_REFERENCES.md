# Lab Evidence References

Canonical Lab: [ziro-lab/chat-native-work-lab-001](https://github.com/ziro-lab/chat-native-work-lab-001)。履歴の丸ごとコピーではなく、Navigatorが実際に採用するhost Claimの索引。

Claimごとに実験、tested source/host、採用範囲、未証明部分、再確認条件を分ける。`ADOPTED` はその狭い事実の採用であり、Navigatorの製品・UX・配布の合格を意味しない。

## Recording Archive timing — ADOPTED

- Lab evidence commit: `0b69aed70a3f8a84cc2ede539d794cb4e8108677`
- Exact host: YMM4 Lite **4.56.1.0**
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- Timing source head: `7450eb3d9b92cbb67c40f2ceac10f80b0ab6c5bd`
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

- Tested source/checkout: `3b52a3acff544ed0ec759d657f48364a415c485c`
- Source tree: `19d3b10569103addb79c63653ed0c8e49f85b1d4`
- Evidence text correction: `5666810491cc7b6c82c60260538f41efa2b7d325`
- Lab PR: #12
- Run: `35120206452`; job `104875765200`
- Artifact: `10457134077`; ZIP SHA256 `78b2b65c8ea6e7438e0f79775388d2d5a6d8159df25a69f0038ec5650931d2ab`
- Exact host: YMM4 Lite4.56.1.0, observed60fps; **16 required assertions PASS**。

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

## YMM4 bundled FFmpeg surface — ADOPTED

- Lab merge commit: `c1acd43297f9a1c2dd5053e7c9667fb84fa237b5`
- Tested source head: `21090626faeb7985f964a26c4a57b8f301255a88`
- Exact host: YMM4 Lite **4.56.1.0** / x64
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- Run: `35123682432`; job `104887398283`
- Artifact: `10458179237`; ZIP SHA256 `c3b8acfc064388731ad33a6493ed07bf53f43abed5ec1b03d97116b54b9559cb`

Labが確認した物理配置:

```text
Resources\bin\x64\ffmpeg\ffmpeg.exe
Resources\bin\x64\ffmpeg\ffprobe.exe
```

public `YukkuriMovieMaker.Plugin.FileSource.FFmpeg.FFmpegResourceLocator` には `GetFFmpegDirectory()` / `GetFFmpegExePath()` があり、**実YMM4 process内**で既存のbundled pathを返した。public `GetFFprobeExePath()` は確認されていないが、`GetFFmpegDirectory()` のsibling `ffprobe.exe` が存在することをnativeで検証済み。

Navigatorの採用ルール:

1. Plugin runtimeは `FFmpegResourceLocator.GetFFmpegExePath()` と `GetFFmpegDirectory()` をAuthorityにする。
2. `ffprobe.exe` は検証済みFFmpeg directoryのsiblingとして解決し、存在しなければfail closedする。
3. 外部PATHを探索しない。Navigator独自のFFmpeg binaryをPlugin folderへcopy/bundleしない。
4. Coreの `FfmpegBackend(ffmpegPath, ffprobePath)` はhost非依存のまま維持し、YMM4 path解決だけPlugin層で行う。
5. supported YMM4 versionを4.56.1.0から上げるとき、locatorとsibling配置を再検証する。

製品側ではsource `61126ef4be5b630118ae574823c9ba26d7c07f51`、run `35129626271` でlocator由来のexact paths、private backend copy不在、実解析を含む**27 required assertions PASS**を別途確認した。Labのhost factだけで製品統合PASSを代用していない。

補足: YMM4 4.56.1.0同梱FFmpegはnative fixture作成時に `libx264` encoderを提供しなかったため、product regression fixtureは同梱backendが生成可能なFFV1/PCM MKVへ変更した。これは解析backendのdecode/read capability不足を意味しない。Navigatorのfeature extractionはencoderを要求しない。

## VideoItem split lifecycle — ADOPTED for edit-time rebinding

- Lab merge commit: `6ec7e54ef4b398322b6eabeee0daa2e97841306f`
- Experiment: `experiments/ymm4/videoitem-split-lifecycle`
- Tested source head: `1041e9300ce134c23359dcfa3d4aba8512881aa6`
- Exact host: YMM4 Lite **4.56.1.0**
- Host ZIP SHA256: `49c0ed689f545737b7ce939971bfc625962e00791c57883dc8e6f058aa336c5a`
- Run: `35359881285`; job `105648639203`
- Artifact: `10554237891`; ZIP SHA256 `2d77175964a67675b1b53fc83274b93f9317678b84194cb1c4efa423b41ecd3f`
- **23 required assertions PASS**

採用する内容:

1. `Timeline.CanSplitSelectedAndGroupedItems(int)` / `SplitSelectedAndGroupedItems(int, ItemSplitSelectionMode)` がtested hostのpublic split surface。
2. VideoItem splitは元objectを残さず、左右とも新しいVideoItemへ置換する。
3. 左右は元Timeline区間をgap/overlapなしでpartitionし、FilePathとconstant positive PlaybackRate2を保持する。
4. 左は元ContentOffsetを保持し、右はcutまでに消費したsource timeだけContentOffsetが進む。
5. 50/100/200%で同じsource-time則を確認。100%の二回目splitでは対象pieceだけが再置換され、非対象pieceは残る。

Navigatorでは**object referenceをcandidate authorityにしない**。split後はsource identity + source timeから現在のpieceへrebindする。これはFeature Indexの再Decode要求を意味しない。

未証明: physical split gesture、save/restart identity、variable/reverse rate、Navigator製品実装。

## VideoItem trim / move / duplicate / Undo-Redo lifecycle — ADOPTED for edit-time rebinding

- Lab merge commit: `5748cc3f691ca4029e701c5fef0d145ff0d4ee2f`
- Experiment: `experiments/ymm4/videoitem-edit-rebinding`
- Tested source/runner contract: `df92cf66d64b3beea3b7e4fc4324539a6f373d27`
- Exact host: YMM4 Lite **4.56.1.0**
- Run: `35424106866`; job `105846989339`
- Artifact: `10578391490`; ZIP SHA256 `97e51bbed4ba6d3a04e31462710ed8dd40a241a9f54e54096cd7a84018d057e7`
- **25 required assertions PASS**, build Warning0 / Error0

採用する内容:

1. head trimはsame VideoItem referenceのままFrame/Length/ContentOffsetを変更する。
2. tail trimはsame referenceのままLengthを短縮し、source startは保持する。
3. split後pieceのmoveはsame referenceのままFrameを変更し、FilePath/Length/ContentOffset/rateは保持する。
4. real copy/pasteで**別objectなのにFilePath + ContentOffset + Length + rateが同一**のoccurrenceを複数作れる。したがってsource identity + source timeだけではTimeline occurrenceを一意に決められない。
5. splitがemitした実UndoRedo commandでは、Undoで元semantic itemへ戻り、observed cycleでは元referenceも復帰。Redoでsplit semanticへ戻り、observed cycleでは以前の左右referenceが再利用された。

Navigator採用ルール:

- current Timeline状態を都度再bindする。frozen Frame/Length/Offset snapshot一致を編集後Jumpの必須条件にしない。
- trimでcandidate anchorが現在のsource rangeから外れた場合は、そのcandidateだけUnavailable/skipにし、Session全体を捨てない。
- move後はcached Timeline Frameを使わず再投影する。
- copy/paste ambiguityへ備え、Review targetごとのoccurrence lineage/discriminatorを持つ。source+timeだけで任意のcopyへjumpしない。
- source file自体が変わった場合は別問題であり、Feature Index stale判定を維持する。

未証明: physical trim/move、physical Ctrl+Z/Ctrl+Y route、全edit種別のUndoRedo、project restart、variable/reverse rate、duplicate lineageの製品選択policy。

## Highlight memo Scene shelf — ADOPTED for 「見どころを確保」

- Lab merge commit: `3cf8b65a554a9f1d5e1b58086e7bfbd1004ec7ed`
- Experiment: `experiments/ymm4/highlight-memo-scene`
- Behavior source head: `aa934561ff20f38f2e20c79763be110b7ee1b87e`
- Documentation/revalidation head: `3662efddebe4c578f5602d5581a7b5000964c17d`
- Exact host: YMM4 Lite **4.56.1.0**
- Behavior run: `35430972362`; job `105865350826`; **37 required assertions PASS**
- Artifact: `10580383042`; ZIP SHA256 `441c596b35cda229043a5abb68c9488f3b2e13324be08be6b8526a96f01cd535`
- Documentation revalidation run: `35431074898`; artifact `10580313712`; ZIP SHA256 `ea25c71276efdf4f049e49909e1c2055d848c7883d24a612f833924120c9a5b5`

採用する内容:

1. tested hostのpublic scene routeは `MainModel.Scenes` / `CreateNewScene()` / `SelectScene(Timeline)` と `Timeline.TryAddItems(...)`。
2. `見どころメモ` Sceneを1つ作り、後のensureで同じTimelineを再利用できる。
3. Mainをactiveのまま、**non-active memo Timelineへ直接VideoItemを追加できる**。追加してもMain active stateと元review VideoItemのproperties/object referenceは変わらない。
4. 複数VideoItemをすべて**Frame 0**に置き、Layer 1/2/3のようにLayerだけ分けて共存できる。
5. 30s/30s/12sを混在させて保存再読込できるため、memo durationはhost固定ではなく製品設定にできる。
6. FilePath / ContentOffset / Length / PlaybackRate2 / Japanese Remark / Layer / Frameがnative save/load roundtripで維持される。
7. memo Timeline Guidもroundtripで維持された。

Navigator採用ルール:

- 「見どころを確保」はmedia fileを切らない。Candidateのsource anchorから短いVideoItem referenceを作る。
- memo clipはpre-rollなし、**AnchorSourceTimeから開始**する。
- 初期default capture durationは30秒、ユーザー変更可。source終端を越える場合は安全に短縮する。
- memo Timelineでは全clipをFrame0へ置き、1 memo = 1 Layer。既存itemと衝突しない最小の空きpositive Layerを初期allocation policyとする。
- Remarkは `見どころナビ｜<hit Filter names>` を基本形にする。
- memo Scene作成/取得で0件なら作成、1件なら再利用、同名複数ならsilentに選ばずfail closedする。
- Product側でMainModel取得に必要なhost-private accessは狭いMemo/Project adapterへ隔離する。

未証明: physical Layer ON/OFF、source relocation/deletion、Navigator buttonのatomic UndoRedo、最終UI/duplicate memo policy、future host version。

## W1 status after implementation

| Claim | Status / boundary |
|---|---|
| FilePath/Frame/Length/ContentOffset/PlaybackRate2 | ADOPTED; TargetAdapterで使用 |
| Timeline FPS / selected VideoItems / integer playhead | ADOPTED on4.56.1.0 / observed60fps |
| Native constant-positive source mapping | ADOPTED; raw mathだけをhost証明の代用にしない |
| Explicit Target Set | implemented; selection-independent snapshot / stale rejection product tests |
| Fractional rounding | product ceiling policy + native map validation |
| Visible product Tool / compiled XAML | product native checkpoint; Lab callbackのみから推定しない |
| YMM4 bundled FFmpeg / ffprobe locator | ADOPTED + product native integration PASS |
| Reload/Undo/scene-switch完全lifecycle | broader acceptance still open; referencesを永続化しない |
| Physical input / decoded preview-frame correspondence | NOT PROVEN |
| Variable/reverse playback, other host versions | OUT OF VERIFIED SCOPE |

再確認条件はhost版変更、map/locator surface変更、対応速度範囲拡張、product regressionとの矛盾。既存の確定Claimをまた一式新規Labへ投げない。製品証拠は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Validation practice — ADOPTED

Lab commit `46530ac8de801d22683ebd4587cccea6c175cc28`:
- host事実 / product機能 / UIUX / package-install / user acceptanceを分ける;
- IsVisibleだけでなくviewport内の実寸・座標を確認する;
- packageの内部rootと実際に検証したDLL hashを確認する;
- producerのPASS自己申告だけでなく独立consumerとnegative testsを使う。

[Native policy](NATIVE_VALIDATION.md)へ継承する。GarageのPreview Speed Extensionはworkflowの参考であり、その機能PASSをNavigatorの証明に流用しない。
