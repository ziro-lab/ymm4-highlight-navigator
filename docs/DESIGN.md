# YMM4見どころナビ v0.4.2
## Transition Filter Learning / Edit-While-Review Rebinding / Highlight Memo Shelf

**Entry:** v0.4.1 Transition Filter Learning / Recall-First Reviewを継承し、2026-09-19に確定した「編集しながら候補巡回を継続する」「使えそうな開始地点を別Sceneへメモとして確保する」方針をMaterial Deltaとして統合する。  
**Product name:** **YMM4見どころナビ**。X4は第一Profile Group / 初期検証対象であり、製品Scopeそのものではない。  
**Target:** YMM4上で長尺録画を軽量Featureへ変換し、過去の短尺教材から作ったTransition Filterを含む複数Filterを高速適用して、編集で確認すべき場面切り替わり候補を前/次で巡回するTool Plugin。

# 1. GOAL / PRIORITY

Primary Goal:

> **3時間級の録画を頭から見ず、編集で確認したい「場面の切り替わり」を高速に拾い、見逃しを減らす。**

Learning Goal:

> **人間が残した短尺動画を教材として投入し、そこに共通する時間方向の変化傾向からFilterを作る。現Filterで拾えない教材だけを次の重点学習対象にし、実際の誤検出だけをExplicit Negativeとして改善へ使う。**

Priority:

1. Recall / 見逃し低減
2. 場面切り替わり候補の高速Review
3. 過去動画からFilterを簡単に作れること
4. Filter強度をすぐ変えて候補量を調整できること
5. 元教材動画をNavigatorの長期正本にしないこと
6. Filter更新後も保存済みCorpusで再評価できること
7. 複数Filterを同時ONにして候補をUnionできること
8. 安いFeatureで成立させ、Heavy AIをFirst Value必須にしないこと
9. YMM4本体・元動画・ymmpを不必要に変更しないこと
10. 分割・Trim・Move・Undo/Redo後も、元Sourceが有効なら解析済みFeatureを再利用してReviewを継続できること
11. 使えそうな候補を本編から切断せず、開始地点が分かる短いメモClipとして別Sceneへ即時確保できること

本製品は「面白さをAIが判定する」ものではない。主役は**状態分類ではなくTransition detection**。

# 2. ACTIVE DECISIONS

**D-01 — Product scope**  
YMM4向け汎用Navigator。X4は最初の強いProfile Groupだがhard-codeしない。

**D-02 — Archive is optional convenience**  
YMM4録画アーカイブPluginは教材動画の切り出しを楽にする別製品。Navigatorの前提・依存ではない。普通の動画ファイル/Folderを同じ教材として扱える。Archive manifest等があればprovenance補助に使ってよいが必須にしない。

**D-03 — Human Positive authority**  
`X4/戦闘` 等へ人間が入れた教材は、そのFilter/ProfileへのPositive membership。別Folderに入っていないことはHard Negativeを意味しない。

**D-04 — Transition-first**  
学習対象はClip全体の平均より、`before → transition → after` の時間方向変化を優先する。Archive等で前後余白があることは不利ではなく、before/after比較材料として利用できる。

**D-05 — Clip center is not truth**  
教材1本に狙うTransitionが1つとは限らず、中央固定もしない。Feature時系列からMaterialなTransition候補を抽出し、必要なら複数候補を持つ。

**D-06 — Multiple patterns per Filter**  
1 Filter / Profileは1つの平均patternへ固定しない。同じ「戦闘開始」でも異なる入り方を `Pattern A OR Pattern B OR ...` で保持できる。Hard Positiveを無理に既存patternへ平均してRecallを落とさない。

**D-07 — Recall-first learning loop**  
Positive教材へ現Filterを再適用し、拾えるものを `Covered Positive`、拾えないものを `Hard Positive` とする。次の特徴探索はHard Positiveを優先する。Covered Positiveは重点学習から外してよいがRegressionには毎回参加する。

**D-08 — Explicit Negative only**  
Negativeは実際の長尺ReviewでNavigatorが誤検出し、人間が「これは違う」と指定した候補を原則Authorityとする。他Profile corpusやFolder absenceを自動Hard Negative化しない。

**D-09 — Contrast, not blind subtraction**  
Explicit NegativeはPositiveから単純に値を引くのではなく、「PositiveにもNegativeにもあるFeatureは弱い」「Positive側に残る差を優先」というContrast材料として使う。Negative回避のために既存Positiveを落とす更新は採用しない。

**D-10 — Sensitivity is a primary runtime control**  
Filterの適用強度 / 検出感度はメインReviewで変えやすい位置に置く。高感度 = 広く拾う / 候補多め、低感度 = 厳しく拾う / 候補少なめ。変更時に再Decode・再学習しない。Global Sensitivityを常時表示し、Filter別補正は必要なら詳細へ置く。

**D-11 — Candidate density is the practical precision guard**  
Recallを守るためPrecision最大化は主目的にしない。ただし候補が多すぎてReview Reductionを失うFilterは実用不可。停止/採用判断ではPositive coverageに加え、長尺での候補密度/Review量を確認する。

**D-12 — Persistent primitive Corpus**  
学習動画はFeature Pack化し、Profile再評価に必要なsemantic前primitiveを残す。元動画はCorpus Authorityにしない。schema/extractor versionを保持し、不足Featureを0扱いしない。

**D-13 — No silent self-training**  
Filter/Profile改善はCorpus replay・Preview・明示Apply・revision保存を通す。人間ラベルを自動変更しない。前revisionへ戻せる。

**D-14 — Multi-profile runtime**  
Filter/Profileは非排他的。同時ONし、HitをOR / Unionする。重複EpisodeはReview上まとめてもProfile attributionは失わない。

**D-15 — Safe host compatibility**  
YMM4のversion番号だけではPluginを拒否しない。必要surfaceが使える限り動かし、依存変更時は該当機能だけfail closedする。通常の機能失敗をYMM4 processへ未処理例外として逃がさない。

**D-16 — Source-time authority for edit-while-review**  
解析済みSessionの正本は「最初のVideoItem object」ではなく、Source identity / analyzed source range / Feature Index / Candidate source time。Split・Trim・Move・Undo/RedoでTimeline Item構成が変わっても、source fileと必要source coverageが有効ならFeature Indexを捨てない。Jump時にcurrent Timelineへ再bindする。

**D-17 — Candidate AnchorSourceTime**  
CandidateはReview用の広いTimeRangeに加えて、実際のhit瞬間を表す `AnchorSourceTime` を持つ。Transition Filterはmatched Transition centerをAnchorとする。Prev/Nextの安定順序はcapture時Target順 + AnchorSourceTimeをAuthorityにし、編集後のcurrent Timeline Frameで候補順を勝手に並べ替えない。current FrameはJump/display用projection。

**D-18 — Occurrence lineage / ambiguity guard**  
同一Source・同一SourceRangeはcopy/pasteで複数Timeline occurrenceになり得る。Source+timeだけで任意のcopyへJumpしない。最初にReview対象へ固定したoccurrenceのlineageを追い、known referenceが残るeditはそれを優先する。Splitのようにreferenceが置換される場合は直前bindingのsource/timeline partitionからunambiguousなreplacementだけをlineageへ採用し、曖昧ならfail closedする。

**D-19 — Highlight memo shelf**  
`見どころを確保` は完成尺の自動切り抜きではなく、使えそうな開始地点を後で確認できる軽量memo。専用Scene `見どころメモ` をcreate/reuseし、全memo ClipをTimeline **Frame 0**へ置き、**1 memo = 1 Layer**で縦に並べる。Review中のactive Sceneは切り替えず、元の長尺ItemをSplit/Trim/Moveしない。

**D-20 — Memo capture semantics**  
memo ClipはCandidate `AnchorSourceTime` から開始し、前余白を付けない。初期default durationは30秒、ユーザーが秒数を変更できる。初版は100% playbackの参照Clipとし、source終端を越える場合だけ短縮する。Layerはmemo Scene内で既存Itemがない最小のpositive Layerを使う。Remarkは `見どころナビ｜<hit Filter names>` を基本形にし、同じsource+anchorのNavigator memoは重複追加しない。

**D-21 — Portable user-data authority**  
Navigator自身が永続化するユーザーデータは、loaded Plugin assemblyのstable rootを基準にした `<plugin>/Data` を正本とする。Learning Corpus / Feature Pack / Filter revisionsは `Data/Learning`、確認セット・表示aliasは `Data/Review`。旧 `%LOCALAPPDATA%\Ymm4HighlightNavigator` は初回移行元としてのみ扱い、Portable側が存在した後は旧側へsilent fallbackしない。移行成功後も旧データは削除せずbackupとして残す。配布packageはuser `Data/` を含めない。

**D-22 — Filter sharing is explicit export/import, not internal-file copying**  
自作Filterの共有は将来許容するが、内部のrevision file / corpus directoryを公開交換形式として固定しない。共有時はversionedな明示Exportを用意し、Runtime適用に必要なFilter pattern・algorithm/schema互換情報・表示metadataだけを持たせる。教材動画、Learning Corpus、ローカルrevision履歴、確認セット、不要なprovenanceは共有packageへ自動同梱しない。import時は既存Filter identity/nameとの衝突を明示処理し、互換しないalgorithm/schemaを0値補完して受理しない。

# 3. PRODUCT FLOWS

## 3.1 Runtime Review

```text
YMM4 Target Set
  ↓
Source Range Planner
  ↓
YMM4 bundled FFmpeg backend
  ↓
Session Feature Index
  ↓
Source Normalization / Salience
  ↓
Enabled Filters / Profiles
  ├─ 戦闘開始 Pattern A OR B
  ├─ MAP切替
  └─ ステーション移行
  ↓ independent match
Hit Sets
  ↓ UNION / Merge / Attribution
Timeline Projection
  ↓
Prev / Next / List
```

Filter ON/OFFとSensitivity変更は既存Feature Indexへ再Queryするだけで、動画を再Decodeしない。

## 3.2 Learning / Filter Authoring

```text
Human-selected clips/folders
  ↓
Batch Import Preview
  ↓
Feature Extraction / Persistent Pack
  ↓
Transition candidate extraction
  ↓
before / transition / after window
  ↓
Cross-clip alignment / common temporal tendencies
  ↓
Transition Filter Candidate
  ↓
Replay on all Positive material
  ├─ Covered Positive
  └─ Hard Positive
  ↓
Long recording trial
  ↓
Explicit Negative from false positives
  ↓
Contrast + Hard Positive refinement
  ↓
Corpus Replay / Candidate-density check
  ↓ explicit Apply
New Filter Revision
```

## 3.3 Edit-while-review / Highlight Memo

```text
Long recording / Target Set
  ↓ one-time source Feature analysis
Candidate
  ├─ Review Range
  ├─ AnchorSourceTime
  └─ Filter attribution
  ↓
Prev / Next
  ↓
current Timeline occurrence rebind
  ↓
Jump
  ↓
user edits YMM4
  ├─ Split
  ├─ Trim
  ├─ Move
  └─ Undo / Redo
  ↓
next navigation rescans/rebinds current occurrence lineage
  ↓
Feature Index reuse; no re-decode for Timeline-only edits
```

候補が現在のlineage内source rangeからTrim/Deleteされて消えた場合は、そのCandidateだけUnavailableとしてskipする。Session全体をstaleにしない。source file自体の変更や必要Feature coverageの欠落は別のstale条件。

使えそうな候補では:

```text
Candidate AnchorSourceTime
  ↓ [見どころを確保]
resolve/create 「見どころメモ」 Scene
  ↓
Frame 0 / next free Layer
  ↓
configured duration (default 30s)
  ↓
Remark = hit Filter names
```

これはmedia export/cutではなく、元Sourceを参照する短いVideoItemを別Sceneへ追加する操作。FilterのReview Rangeにあるpre-rollはmemo開始位置へ持ち込まない。

# 4. FILTER / PROFILE MODEL

## 4.1 Transition Filter is primary

Profile内部にはState conditionも持てるが、初期の主役はTransition Filter。

例:

```text
Filter: X4 / 戦闘開始

Pattern A
  Before:
    visual delta: low
  Transition:
    delta rise: strong
    histogram change: high
    grid change: broad
    audio burst: optional-medium
  After:
    activity: sustained

OR Pattern B
  Before:
    stable UI
  Transition:
    target/UI + visual delta rise
  After:
    repeated motion/burst
```

Exact formulaやPattern clustering方法は実装Probeへ委任する。

## 4.2 Feature candidates

Visual:

- luma / contrast / chroma proxy
- frame delta / histogram distance
- edge density / extreme ratio
- 4x4等のgrid summaries
- compact descriptor similarity

Temporal:

- stable → sudden
- change start/end
- delta acceleration
- before/after distribution difference
- peak / rise / duration / sustained activity

Audio:

- RMS / Peak
- short-vs-context relative change
- burst timing

Heavy ML / OCR / Object DetectionはCURRENT必須にしない。

## 4.3 Sensitivity

SensitivityはFilter再学習とは分離する。

```text
高感度
→ threshold / match許容を広げる
→ 見逃しにくい
→ 候補増

低感度
→ threshold / match許容を狭める
→ 候補減
→ 強い一致中心
```

UIでは「確率」と誤解させず、`検出感度` / `拾いやすさ`として扱う。

# 5. LEARNING CORPUS

## 5.1 Intake

普通の動画FolderをbatchでImportできることが基本。Archive出力はその一例にすぎない。

```text
教材/
└─ X4/
   ├─ 戦闘開始/
   ├─ ステーション移行/
   └─ MAP切替/
```

Folder pathからGroup/Filter名を推定できる場合はPreviewで補助する。毎動画に細かいmetadata入力を要求しない。

## 5.2 Persistent sample

最低限:

```text
LearningSample
├─ sample id / fingerprint
├─ positive memberships
├─ duration/basic metadata
├─ extractor/schema version
├─ visual scalar series
├─ histogram/grid/descriptor series
├─ audio series
├─ temporal summaries
├─ transition candidates / windows
└─ provenance / display name / import time
```

同一素材に複数Positive membershipがある場合はPack重複よりrelation追加を優先する。

## 5.3 Raw video ownership

最初は**非破壊Import**を成立させる。将来、明示的にuser-owned consumptive Inboxを設定した場合だけ削除可能にする。

削除条件:

- Pack write成功
- reload検証成功
- sample registration commit成功
- 対象Folderが明示的にconsumptive

Partial / Error / Cancel / schema failureでは削除しない。Archive Project等が参照する外部素材をNavigator都合で削除しない。

# 6. COVERAGE / HARD POSITIVE / NEGATIVE

## 6.1 Positive replay

Filter生成/更新後はPositive教材すべてへ再適用する。

```text
Covered Positive
  = 現Filterで拾える
  = 次の特徴探索では低優先
  = Regressionでは毎回確認

Hard Positive
  = 現Filterで拾えない
  = 最優先改善対象
```

「拾えたので学習不要」は**特徴探索の優先度**の意味であり、Regression Corpusから消す意味ではない。

## 6.2 Explicit Negative

Runtime候補にユーザーが`これは違う`を指定したら、その候補の前後Feature WindowをExplicit Negativeとして保存できる。動画再切り出しを必須にしない。

## 6.3 Learning priority

初期優先度:

1. Hard Positive
2. Explicit Negative
3. Covered Positive

他Profileへの一致やnoveltyは補助情報であり、First Valueの中心ではない。

# 7. FILTER REFINEMENT / REGRESSION

改善Candidateは最低限:

- Hard Positive coverage改善
- Covered Positiveを落とさない
- Explicit Negativeとの差を増やす
- 長尺で候補密度がMaterialに悪化しない
- query costがMaterialに悪化しない

を確認する。

他Profile corpusへのhit増加だけでは即FAILにしない。multi-label semanticsを維持する。

Applyは明示操作。Filter revisionを保存しrollback可能にする。

# 8. RUNTIME MULTI-PROFILE SEMANTICS

各Filter/Profileは同じSession Feature Indexへ独立適用。

```text
戦闘開始       30 hits
MAP切替        10 hits
```

両方ONならraw hit total 40。時間帯が重なればReview候補は40未満でよい。

UIは:

- Profile hit total
- Unique review candidates

を区別する。

`ANY / N-of-M / ALL` は原則Profile内部Condition composition。Profile間はOR / Union。

# 9. YMM4 / BACKEND BOUNDARY

## 9.1 Mapping

Target AdapterへYMM4 identity / timing / seek依存を隔離する。

- PlaybackRate2をcurrent surfaceとして扱う
- native PlaybackRateMapでsource timeを確認
- ContentLengthをsource usage length推定へ使わない
- 同じSourceの別Item occurrenceは別Candidate projection
- current Frame/Length/ContentOffsetの完全一致を編集後projectionのAuthorityにしない
- split/trim/move/undo後はReview occurrence lineageからcurrent Itemを再bindする
- source+timeだけでduplicate occurrenceを一意選択しない
- CandidateはReview RangeとAnchorSourceTimeを分ける
- CURRENTは正の一定PlaybackRate

採用済みhost事実はLab evidenceをAuthorityとし、未知事項だけLabへ戻す。

## 9.2 FFmpeg

Plugin runtimeはYMM4同梱FFmpegをhost public locator capability経由で解決する。Navigator独自FFmpeg copy/PATH fallbackを持たない。

YMM4更新でlocator/type/pathが変わった場合は解析機能をfail closedし、YMM4本体を不必要に巻き込まない。

CoreのFfmpegBackendはexplicit path inputのままYMM4非依存。

# 10. MAIN UI

Primary controls:

```text
グループ [ X4 ▼ ]

フィルター
[✓ 戦闘開始 30] [✓ MAP切替 10] [ ] ステーション移行 8]

検出感度
少なく拾う ─────●───── 多く拾う

候補 36件 / ヒット計 40
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]

確保時間 [ 30 ] 秒
[ ★ 見どころを確保 ]
```

常時見せるもの:

- Filter/Profile ON/OFF
- Global Sensitivity
- candidate / hit count
- Prev / Next / List
- 見どころを確保 / capture duration

Filter内部threshold、Pattern詳細、Feature名は通常Reviewへ常時露出しない。

Filter別Sensitivity補正は必要になった場合のみ詳細surfaceへ追加する。

「見どころを確保」はselected Candidateがcurrent review sourceへ解決できる時だけ有効。成功時はactive review Sceneを変えず、memo Layer番号を短く通知する。Candidateはcurrent sessionでは確保済み状態を示して二重操作を避ける。

# 11. FILTER AUTHORING UI

ユーザー向け入口はCorpus内部用語より、次を優先する。

```text
[動画からフィルターを作る]

教材: X4 / 戦闘開始  12本

共通する場面切り替わりを解析
  Covered Positive  9
  拾えなかった教材 3

[長尺動画で試す]
[フィルターを改善]
[保存]
```

Runtime候補には将来:

```text
[これは違う]
```

を追加し、Explicit Negativeへ登録できる。

# 12. STORAGE / LIFECYCLE

Navigator-owned portable root:

```text
<YMM4>/user/plugin/Ymm4HighlightNavigator/Data/
├─ Learning/
│  ├─ corpus.json
│  ├─ packs/*.navfp
│  ├─ filters.json
│  └─ filters/<filter-key>/<revision>.json
└─ Review/
   └─ review-settings.json
```

Pathはhard-codedなYMM4 version/pathではなくloaded Navigator assembly locationから解決する。旧 `%LOCALAPPDATA%\Ymm4HighlightNavigator\Learning|Review` はPortable rootが未作成の場合だけ検証付きで移行する。Portableが存在すれば常にそちらがAuthorityで、破損時もlegacyへ黙って戻らない。旧データは移行後も削除しない。

UIから実保存先を確認してフォルダーを開けること。将来の `.ymme` package/installでは `Data/` をpackage payloadへ含めず、updateでuser dataを上書きしないことをrelease acceptanceへ含める。

Persistent:

- Filter/Profile Group metadata
- Filter/Profile revisions
- Review Presets
- Learning Feature Packs
- Positive memberships / provenance
- Transition candidates/windows
- Covered/Hard Positive state
- Explicit Negative Feature windows
- update candidate metadata
- `見どころメモ` Scene / memo VideoItemsはYMM4 Project側に永続化される。Navigator独自DBへ同じClipを二重保存しない

Session:

- Runtime Feature Index
- Query Episodes
- Runtime Review History
- Review occurrence lineage / current bindings
- Candidate AnchorSourceTime / current projection state
- temporary descriptors / backend temp

Profile revisionがPack schemaを満たさない場合、黙って0/negative扱いしない。

内部保存形式はバックアップ/移行のAuthorityであり、共有契約ではない。自作Filterの共有を実装する場合は別のversioned Export/Import境界を追加し、Corpusやraw動画を必要としないRuntime Filter packageとして扱う。

# 13. ACCEPTANCE

## 13.1 First transition-learning value

少数の短尺動画から:

```text
教材Import
→ Feature Pack化
→ Transition候補抽出
→ Filter Candidate生成
→ 全PositiveへReplay
→ Covered / Hard Positive分類
→ 長尺録画へ適用
→ Sensitivityを変えながら候補巡回
```

まで一気通貫で成立する。

## 13.2 Recall-first behavior

- Positive教材をFilterへ再適用できる
- 拾えないPositiveをHard Positiveとして抽出できる
- Covered PositiveはRegressionへ残る
- Filter更新で既存Covered PositiveをMaterialに落とさない
- Sensitivity変更で再Decodeしない

## 13.3 Explicit Negative behavior

- Runtime誤検出候補を人間がNegative登録できる
- 前後Feature Windowを保持できる
- 他Profile absenceをHard Negative化しない
- Negative対応でPositive coverageを落とすCandidateをrejectできる

## 13.4 Runtime Review

READMEなしで:

```text
Target追加
→ Filterを複数ON
→ 解析
→ Sensitivity調整
→ 前/次
→ YMM4 Jump
```

- overlap hitはReview上1候補へまとめてもattribution保持
- selection変更でTargetが黙って変わらない
- stale Targetを拒否
- background analysis中もYMM4 UIを持続blockしない

## 13.5 Corpus

- Folder単位batch Import
- dedupe / multiple positive membership
- raw-video-free replay
- schema incompatibility明示
- 最初は非破壊

## 13.6 Revision

- Previewなしのsilent update禁止
- Applyで新revision
- previous revisionへrollback
- Corpus replayと候補密度を確認

## 13.7 Edit-while-review rebinding

- 解析後にReview対象をSplitしても、元Sourceが同じなら再Decodeせず次Candidateへ進める
- head/tail Trim後、Anchorが残るCandidateはcurrent Itemへ再投影できる
- Trim/DeleteでAnchorが消えたCandidateだけskipできる
- split pieceをMoveしてもcurrent Frameを再計算してJumpできる
- Undo/Redo後もcurrent Timeline stateからrebindできる
- copy/pasteで同一source rangeが複数ある時、任意のduplicateへ誤Jumpしない
- source file自体が変更された場合はFeature staleを明示する

## 13.8 Highlight memo shelf

READMEなしで:

```text
候補へJump
→ 使えそうなら「見どころを確保」
→ Review Sceneはそのまま
→ 「見どころメモ」SceneにFrame0 / 1Layer1本で追加
```

- memo開始 = AnchorSourceTime。review pre-rollを付けない
- default30秒、ユーザー指定尺が反映される
- source終端では安全に短縮する
- Remarkにhit Filter名を残す
- 既存memoはLayer ON/OFFで判別しやすいよう時間方向へずらさない
- 同一source+anchorの二重memoを防ぐ
- Main review Itemを変更しない
- Project save/reload後もmemo Scene/Layer/Remark/source位置が残る

# 14. EXECUTION WAYPOINTS

## W1 — YMM4 Target / Projection Spine — IMPLEMENTED BASELINE

Target snapshot / source-time mapping / Timeline Jump / separate occurrenceのbaselineは実装済み。現行のstrict stale snapshotはedit-while-reviewに対して過剰なので、下のW1-Rで置換する。

## W1-R — Edit-time Rebinding Extension — NEXT FIRST

**Purpose:** 一度解析したSource Featureを、Split / Trim / Move / Undo-Redo後も再利用しながら候補巡回を続ける。

Scope:

- Candidate AnchorSourceTime
- immutable ReviewSourceSession / stable review order
- occurrence lineage / current binding
- split replacement adoption
- trim containment / per-candidate unavailable
- move/current Frame reprojection
- duplicate ambiguity fail-closed
- source-file staleは維持

**Exit:** `解析 → 候補へJump → Split/Trim/Move → 次候補` が再Decodeなしで動き、duplicate source occurrenceへ誤Jumpしない。

## W2 — Shared Feature Engine — IMPLEMENTED BASELINE

RuntimeとLearningで共有するprimitive Feature、FeaturePack、PackStore、FFmpeg backend。

## W3 — Learning Corpus + Transition Extraction — IMPLEMENTED CHECKPOINT

Non-destructive batch intake / persistent Pack / raw-video-free replay / local Transition proposalsまで実装済み。作り直さない。

## W4 — Multi-Profile Runtime Review — IMPLEMENTED CHECKPOINT

既存Profile evaluator / learned Filter / OR Union / merge / attribution / Prev-Next-List / no-redecode sensitivityまで接続済み。

## W4-M — Highlight Memo Capture Extension — NEXT SECOND

**Purpose:** Reviewで使えそうなCandidate開始地点を、元Timelineを編集せず別Sceneへ即時確保する。

Scope:

- `見どころメモ` Scene create/reuse
- active Review Scene保持
- Frame0 fixed
- one memo per Layer / smallest free positive Layer
- AnchorSourceTime start / no pre-roll
- configurable duration, default30s
- 100% reference playback
- source-end clamp
- Remark attribution
- same source+anchor dedupe
- friendly fail-closed on duplicate memo Scene or host dependency change

**Exit:** `候補選択 → 見どころを確保` でMainを変えずmemo shelfへ追加でき、save/reload後もそのままLayer ON/OFF確認できる。

## W5 — Transition Filter Authoring + Coverage Loop — IMPLEMENTED CHECKPOINT

教材動画 → Filter生成 → Positive replay → 長尺試用 → 明示保存 → Sensitivity変更 → Prev/Nextまで実装済み。実X4精度合格ではない。

## W6 — Hard Positive / Explicit Negative / Revision — AFTER W1-R / W4-M

**Purpose:** 見逃しと実際の誤検出からFilterを改善する。

Scope:

- Hard Positive優先
- `これは違う` → Explicit Negative Feature Window
- Contrast refinement
- full Positive regression
- candidate-density guard
- existing Preview / Apply / revision / rollbackへ接続

**Exit:** Hard Positive coverageを改善しつつ既存Positiveを維持し、誤検出由来Negativeへの一致を減らせる。

## W7 — Real Recording / Performance / Distribution

- X4実長尺でRecall / candidate density / review reduction評価
- long-recording memory / ingest / query latency
- edit-while-review hands-on
- memo shelf hands-on / many-layer usability
- GPU path + software fallback
- `.ymme` install / upgrade
- supported YMM4 update compatibility
- hands-on usability acceptance

Goal到達後にGeneral Profile自動clusterやHeavy Detectorへ自動拡張しない。

# 15. RISKS / REOPEN

Main risks:

- **Average-pattern collapse:** 複数の入り方を1平均に潰して見逃す
  - multiple Pattern OR
- **Hard-positive overfit:** 難例だけへ合わせ既知Positiveを落とす
  - full Positive replay
- **Negative overfit:** 誤検出回避でRecallを落とす
  - Positive regressionを優先
- **Candidate explosion:** Recall100%でもReview量が多すぎる
  - candidate density / Sensitivity / merge
- **Corpus information loss:** 元動画削除後に新Featureを取れない
  - primitive寄りPack + schema version
- **False exclusivity:** 他ProfileをHard Negative化する
  - Positive membership / absence unknown
- **Archive coupling:** Archiveを必須前提にする
  - ordinary video importがAuthority
- **Host update failure:** YMM4更新で依存変更しPluginが本体を巻き込む
  - capability guard / fail closed
- **Frozen-item invalidation:** Split/Trim/MoveだけでFeature Indexを捨ててReviewが止まる
  - source-time authority / current Timeline rebind
- **Duplicate occurrence jump:** copy/pasteされた同一source rangeへ誤Jumpする
  - occurrence lineage / ambiguity fail-closed
- **Memo shelf drift:** memoを時間方向へ並べて使用Layerが即座に分からなくなる
  - Frame0固定 / 1 memo 1 Layer
- **Memo overreach:** memo機能が完成尺推定・media exportへ肥大化する
  - Anchor開始 + user指定固定尺のreference clipに限定

REOPEN:

- cheap FeatureだけではTransitionを十分拾えない
- candidate densityが実用域へ下がらない
- multiple Pattern数が増え続けFilterが管理不能になる
- Feature Packが大きすぎる
- raw-video-freeでは改善に必要な情報が頻繁に不足する
- SensitivityがFilterごとに不整合な挙動になる

# 16. AUTHORITY / HANDOFF

Authority:

1. Current user decisions / D-01〜D-20 / Priority
2. 本v0.4.2 Design
3. v0.4.0の非衝突部分
4. Lab / Product evidence
5. Implementation convenience

Delegation:

- exact transition peak extraction algorithm
- exact alignment method
- exact multiple-pattern split criterion
- exact Filter formula / optimizer
- exact sensitivity mapping
- exact fingerprint / storage format
- exact candidate-density threshold
- exact feature sample rates / descriptor sizes

再確認が必要:

- ArchiveをNavigatorの必須依存へ変更
- Human分類を増やし毎動画細かい入力を要求
- 他ProfileをHard Negativeへ変更
- RecallよりPrecisionをPrimary Goalへ変更
- Heavy MLをFirst Value必須化
- silent auto-trainingへ変更
- Runtime Reviewで元動画 / ymmpを破壊変更
- `見どころを確保`を完成尺の自動抽出/自動編集へ拡大
- memo Clipを時間方向へ自動でずらす配置へ変更
- duplicate occurrenceをSource+timeだけで任意選択

# 17. FINAL INTENT

> **人間が過去に残した短尺動画から、日常→戦闘などの「場面が切り替わる傾向」をFilterとして育てる。長尺Reviewでは一度解析したSource Featureを編集後も再利用し、候補を前/次で高速確認する。使えそうな開始地点は本編を切らず「見どころメモ」SceneへFrame0・1Layer1本で確保する。現Filterで拾えない教材と実際の誤検出だけを重点改善し、見逃しを増やさず候補量を減らす。**

Operational image:

```text
過去の短尺動画
  ├─ 自分で切り出した動画
  └─ 録画アーカイブで楽に切り出した動画
        ↓
Folderで大分類
        ↓
NavigatorへImport
        ↓
Transition Feature / Pack
        ↓
Filter生成
        ↓
Positive replay
  ├─ Covered
  └─ Hard Positive → 改善
        ↓
新しい長尺録画へ適用
        ↓
Sensitivityを調整し高速Review
        ↓
使えそうなら「見どころを確保」
  └─ Frame0 / 1Layer1本 / Anchor開始
        ↓
編集してもSource-time rebindでReview継続
        ↓
誤検出だけExplicit Negative
        ↓
Filter revision改善
```

**Archiveは教材準備を楽にする相性の良い別Pluginであり、Navigatorの前提ではない。**
