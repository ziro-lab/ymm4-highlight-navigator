# Implementation continuation — v0.4.2 edit-while-review + memo shelf

最初に `AGENTS.md`、`docs/DESIGN.md`、`docs/IMPLEMENTATION_STATUS.md`、`docs/ROADMAP.md`、`docs/LAB_REFERENCES.md` と現在のmain/PRを確認する。

現mainの再開点はW3/W5初期学習機能まで実装済み。**W3/W5を作り直さない。次は W1-R → W4-M → W6-A の順。**

## Current reusable spine

```text
普通の教材動画 / 直下フォルダー
→ LearningModel / CorpusStore
→ FeaturePack / catalog / positive membership
→ TransitionIndex
→ FilterAuthor / TransitionMatcher
→ FilterDraft
→ trial or FilterStore.Apply
→ NavigatorModel learned evaluator
→ Episode Union / Target projection / Prev/Next
```

既存のYMM4 bundled FFmpeg locator、Feature extraction、Corpus、FilterStore、Sensitivity no-redecode、Learning UIは再実装しない。

## Adopted Lab facts for the next slices

### Edit-time lifecycle

- Split: 元VideoItemは消え、左右とも新object。source rangeはpartitionされる。
- Head/tail Trim: same object referenceのままFrame/Length/ContentOffset等が変わる。
- Move: same object referenceのままFrameが変わり、source rangeは維持。
- Undo/Redo: semantic stateが戻り、observed cycleでは過去referenceも再出現した。
- Copy/Paste:別objectなのに同じFilePath + ContentOffset + Length + rateのoccurrenceを複数作れる。

Evidence exact pinsは `docs/LAB_REFERENCES.md`。製品実装はこれを再調査せず、product integrationだけをnativeで証明する。

### Memo Scene shelf

- `MainModel.Scenes / CreateNewScene / SelectScene / Timeline.TryAddItems` がtested hostで使用可能。
- Mainをactiveのままnon-active memo TimelineへVideoItemを追加できる。
- 全memoをFrame0へ置きLayerだけ1/2/3と分けられる。
- 30s/30s/12sの可変長、ContentOffset、Remark、FilePath、memo Timeline Guidがsave/reload後も維持。
- memo追加でMain source itemは変更されなかった。

## Slice 1 — W1-R Edit-time rebinding

### Goal

一度解析したSource Feature Indexを、Timeline-only editのたびに捨てず、そのまま次の候補へ進める。

### Required model delta

現行 `Candidate(TargetId, SourceRange, Frame, ...)` はFrameをAuthorityにしすぎている。次の形へ寄せる。

```text
Candidate
├ TargetId / ReviewTargetId
├ SourceKey
├ SourceRange          // review context
├ AnchorSourceTime     // actual hit/start authority
├ StableOrderKey       // capture-time target order + anchor
├ Filter/Profile attribution
├ current projection  // nullable / refreshable
└ visited / memoed UI state
```

Transition Filterは `TransitionMatch.CenterSeconds` をAnchorへ通す。review rangeのPreRoll startをAnchorにしない。

Generic seed Profileは最初の実hit pointをAnchorにできるよう、ProfileHit/Episode pathへoptional anchor metadataを通す。anchorを失うためだけにReview Range unionを壊さない。

### Review session / occurrence lineage

`TargetAdapter` は次の責務へ分割する。

```text
ReviewSourceSession
  immutable:
    review target id
    source key
    analyzed source range
    capture-time order
    source file fingerprint/stamp
  mutable host binding:
    known lineage references
    last resolved item(s)
    last source/timeline ranges
```

Rebind policy:

1. current Timelineにknown lineage referenceが存在しAnchorを含むならそれを使う。
2. Split等でknown current refが消えた場合だけ、直前bindingと同Sourceのreplacement candidatesを調べる。
3. replacementがsource/timeline partitionとして一意に説明できる場合だけlineageへ採用する。
4. Copy/Pasteされた同一source occurrenceは、known lineageが残る限り無視する。
5. replacementが複数解釈できる場合は任意選択せず、そのCandidateを `曖昧` / unavailableとしてfail closedする。
6. Trim/DeleteでAnchorを含むlineage pieceが無ければそのCandidateだけskipする。
7. Moveはknown referenceが残るのでcurrent Frameだけ再投影する。
8. Undo/Redoはknown historical refsが再出現すれば再利用し、出なければ同じrebind ruleを使う。

Source fileが消えた/内容stampが変わった場合はFeature Index staleとして従来どおり再解析要求。Timeline editとsource mutationを混同しない。

### Navigation order

Prev/Next順は**capture時Target順 + AnchorSourceTime**で安定させる。編集後のTimeline Frameでqueueを並べ替えない。

current Timeline FrameはJump直前に再projectする。List表示のcurrent positionが必要ならprojection refreshで更新するが、順序Authorityにはしない。

### Expected code touch

- `Core/Intervals.cs`: ProfileHit / ReviewEpisodeにoptional anchor metadataを通す。
- `Core/Learning/TransitionFilters.cs`: learned match centerをanchorとして保持。
- `Plugin/NavigatorModel.cs`: Candidate model、stable order、visited identity、projection refresh。
- `Plugin/TargetAdapter.cs`: strict snapshot staleからReviewSourceSession + rebindへ。
- Tests: Core queue/anchor pure tests + Plugin adapter/model tests。

Host-private surfaceをCoreへ漏らさない。

### Pure acceptance first

- transition review rangeがpre-rollを持ってもAnchorはtransition center。
- overlap/union後もFilter attributionと合理的Anchorが残る。
- stable orderはcurrent Frame moveで変わらない。
- Trimで1候補だけunavailableになり他候補は残る。
- duplicate source occurrenceがあってもknown lineageが選ばれる。
- ambiguous replacementはfail closed。
- source stamp変更はsession stale。

### Product native checkpoint

Generated mediaで:

```text
解析
→ CandidateへJump
→ real Timeline split
→ Next
→ head/tail trim
→ Next
→ split piece move
→ Next
→ Undo/Redo semantic cycle
→ Next
```

を再Decodeなしで通す。同一source copy/pasteを作り、copy側へ誤Jumpしないこともassertする。

Native PASSはphysical mouse/keyboard gestureの証明へ拡張しない。Labで既に確定したmutation semanticsを製品が正しく使うことだけを証明する。

## Slice 2 — W4-M 「見どころを確保」

### User-visible behavior

Main Reviewへ小さい操作を追加:

```text
確保時間 [30] 秒
[ ★ 見どころを確保 ]
```

selected Candidateがある時だけ有効。押すとactive Sceneを変えず、`見どころメモ` Sceneへreference clipを追加する。

### Memo semantics

- start = `AnchorSourceTime`。前余白なし。
- default duration = 30秒。
- durationはユーザー指定可能。
- 初版memo playback = 100%。durationはsource/reference clip秒数として扱う。
- source終端まで指定秒数が無い場合は残りだけに短縮。
- Frame = 0。
- Layer = memo Sceneで**どのItemにも使われていない最小positive Layer**。1 memo = 1 Layer。
- Remark = `見どころナビ｜<hit Filter names>`。
- source fileをcopy/exportしない。FFmpeg cut/re-encodeなし。
- Review target ItemをSplit/Trim/Moveしない。

### Scene resolution

初版は安全優先:

1. Scene名 `見どころメモ` が0件 -> create。
2. 1件 -> reuse。
3. 2件以上 -> silentに選ばずfriendly failure。

Memo Scene creationが一時的にactive Sceneを変えるhost behaviorへ備え、create前のReview Timelineを保持し、作成後に元へ戻す。

MainModel取得などnon-public host accessは新しい狭い `MemoSceneAdapter` / `ProjectSceneAdapter` に隔離し、host capability change時はmemo機能だけfail closedする。

### Duplicate memo guard

同じNavigator memoを連打しない。初版はmemo Scene内のNavigator-owned itemについて:

```text
normalized SourceKey
+ Anchor ContentOffset (small tolerance)
```

が一致すればcaptured済みとみなし、追加しない。RemarkのFilter名差だけで同じ開始点を重複させない。

### UI state

Candidateにはsession内 `Memoed` stateを持たせてよい。再読込後のAuthorityはmemo Scene scan。UIの★は補助表示であり独自DBを正本にしない。

### Expected code touch

- new `Plugin/MemoSceneAdapter.cs`。
- `NavigatorModel.cs`: CaptureDurationSeconds / CaptureMemoCommand / memoed refresh。
- `NavigatorView.xaml`: capture duration + button。
- CandidateにSourceKey/Anchor/Filter attributionを保持。
- Native product proofをexisting workflowへ追加または小さい専用laneで追加。

### Product native checkpoint

- 0 memo Scene -> create。
- existing single memo Scene -> reuse。
- Main activeのままLayer1/2/3へFrame0追加。
- 30秒と別指定秒数。
- source終端shortening。
- Japanese Filter Remark。
- same source+anchor duplicate no-op。
- 同名memo Sceneが複数ならfriendly fail closed。
- Main source semantic signature unchanged。
- save/reload後もScene/Layer/Remark/offset/length保持。
- normal feature failureがYMM4 processへ未処理で漏れない。

## Combined first-use checkpoint

W1-RとW4-Mが終わった時点で、実際の編集フローを一度通す。

```text
長尺を一度解析
→ Nextで候補確認
→ 使えそうなら「見どころを確保」
→ 必要ならMain側でSplit/Trim/Move
→ Next
→ さらにmemo確保
→ 最後に「見どころメモ」Sceneを開く
→ Layer ON/OFFで開始地点を確認
```

このcheckpointで重要なのは完成尺自動抽出ではない。**Reviewを止めず、開始地点を取りこぼさず、手作業のSplit/Moveを減らせること**。

## Then W6-A false-positive feedback

W1-R/W4-M後に既存計画へ戻る。

1. Runtime candidateがどのFilter/revision/Transition center/source windowから来たかを保持する。
2. `これは違う` で対象FilterだけへExplicit Negativeを登録する。
3. Feature windowを保存しraw video再Decodeを要求しない。
4. Positive regressionを優先し、Negative回避だけでKnown Positiveを落とす提案はrejectする。
5. existing FilterStore Preview/Apply/revision/rollbackへ接続し第二の保存正本を作らない。

## Regression to preserve

- Baseline Core27。
- Learning35 x 2 generated media conditions。
- Existing product native47。
- version番号だけでYMM4を拒否しない。
- host bundled FFmpeg only; private copy/PATH fallbackなし。
- Slider/Filter toggleで再Decodeしない。
- Learning Filter trial/save/raw-free reuse。
- Candidate attribution / same-source separate occurrence baseline。

W1-Rで旧strict stale testは意味が変わるため、単に削除せず `source mutation stale` と `Timeline edit rebind` に分解して置換する。

## Execution discipline

- 実装branchは小さく分ける。推奨:
  - `feature/w1r-edit-time-rebinding`
  - `feature/w4m-highlight-memo-capture`
  - その後 `feature/w6-explicit-negative`
- Core model/pure logicを先にcheap testsで固める。
- host behaviorはLabで再調査しない。製品integrationだけnative。
- doc-only変更ではnativeを回さない。
- Archive Pluginへ依存や変更を追加しない。
- private recording / Corpus / YMM4 binariesをcommitしない。

## Stop / reopen conditions

- current host factと製品integrationが矛盾する -> その狭いfactだけLabへ戻す。
- split replacement lineageがunambiguousに決められないケース -> arbitrary guessを追加せずfail closed。
- memo Sceneが複数存在する -> silent merge/selectしない。
- source path/contentが変わる -> edit-time rebindで誤魔化さずstale/reanalysis。
- memo captureが完成尺推定やmedia exportへ膨らみ始める -> current scopeへ戻す。
