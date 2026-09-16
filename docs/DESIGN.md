# YMM4見どころナビ v0.4.0
## Learning Corpus / Multi-Profile Union / Reverse Classification / Compact Handoff Candidate

**Entry:** REDESIGN_RECOVERY。`X4見どころナビ v0.3.1` をBaselineとし、2026-09-16の運用方針変更だけをMaterial Deltaとして更新する。  
**Product name:** **YMM4見どころナビ**。X4は第一対象・初期検証ゲームであり、製品Scopeそのものではない。  
**Target:** YMM4上で長尺ゲーム録画を軽量Featureへ変換し、複数の見どころProfileを同時適用して候補区間を高速巡回するTool Plugin。さらに、分類済み短尺素材からProfileを育てるPersistent Learning Corpusを持つ。

# 1. IDENTITY / GOAL / PRIORITY

Current revision: **v0.4.0 Learning Corpus / Multi-Profile Candidate**

Problem Form: **RETAIN_CURRENT_FORM / EXPAND LEARNING LOOP**

> **長尺録画を再利用可能な軽量Feature Indexへ変換し、ONにした複数ProfileのHitを高速Unionして、YMM4 Timeline上の見るべき候補へ正しくJumpする。**

加えて:

> **人間がざっくり分類した短尺録画からSemantic Profileの特徴を抽出し、元動画を保持せず軽量Learning Feature Packだけを長期保存する。既存Profileで説明できない分類済みサンプルを優先してProfile改善へ使う。**

First Value — Review:

> **3時間の録画を頭から確認せず、`戦闘`、`ステーション`、`MAP`等の必要Profileを複数ONにして、Hit候補を前/次で高速巡回できる。**

First Value — Learning:

> **人間が `X4/戦闘` 等へまとめた短尺録画フォルダを一度解析すると、動画を保持せず再評価可能な軽量Corpusへ変換し、その分類に共通するFeatureからProfile改善候補を作れる。**

Priority:

1. 複数ProfileをON/OFFして候補を高速に増減できること
2. Recall / 見逃し低減
3. Review時間削減
4. 分類済み素材を少ない手間でProfile改善へ使えること
5. 元学習動画を見どころナビ側で長期保持しないこと
6. Profile更新後も保存済みCorpusを再評価できること
7. 解析中もYMM4を使えること
8. 安いFeatureの組み合わせで成立させること
9. 小さい構造 / Heavy AI非必須

本製品は「面白さをAIが判定する」製品ではない。Profileは**特定の場面・状態へ近いFeature patternを検出するためのSemantic Detector**として扱う。

# 2. REDESIGN RECOVERY / MATERIAL USER DECISIONS

## 2.1 Baseline

Baselineは `X4見どころナビ v0.3.1 Research-Prepared / Zero-Prep Bootstrap Candidate`。

Preserveする主な基盤:

- YMM4 Tool Plugin / 日本語UI
- `Feature Index → Condition → Query Episode → Timeline Projection → Review`
- Source timeとTimeline occurrence分離
- 同一Source重複rangeは一度だけ解析
- FFmpeg child process + GPU decode/resize + software fallback
- cheap visual/audio feature中心
- source-adaptive normalization
- no-redecode query
- Background / Progress / Cancel
- Commonality / ContrastによるFeature候補抽出
- Profile変更は黙って永続反映せず、明示的なcommit境界を持つ
- 元動画 / ymmp非破壊の通常Review

## 2.2 Trigger Delta

2026-09-16に以下をCurrentへ昇格する。

1. 製品名とScopeを `X4見どころナビ` から **`YMM4見どころナビ`** へ変更
2. 録画アーカイブで切り出した短尺素材を、人間がフォルダ等で大分類して教材化
3. 分類済み動画を解析後、**Persistent Learning Feature Pack**へ変換し、見どころナビは元動画を長期保持しない
4. 既存Profileで分類済みサンプルを逆判定し、**期待Profileで説明できないサンプルほど学習価値が高い**ものとしてProfile改善へ利用
5. 複数Profileを同時ONにし、各ProfileのHitを基本OR / UnionでReview候補へ加える
6. 同一時間帯の重複HitはReview Queue上で統合し、Profile attributionは失わない

## 2.3 Invalidated Basis

以下はv0.3.1のままでは不足する。

- Feature Indexを原則Session onlyとするだけでは、元教材動画削除後にProfileを再評価できない
- `General → Genre → Game → User Preset` の一方向階層だけでは、独立Profileを複数同時ONにするRuntime modelが曖昧
- Profile authoringを通常Review feedback中心に限定すると、アーカイブ由来の分類済みCorpusを活かしきれない
- X4固有Product identityは現在の意図より狭い

## 2.4 Active Decisions

**D-01 — Product scope**  
製品はYMM4向けの汎用見どころナビ。X4は最初に育てるProfile Group / 検証対象であり、実装をX4固有hard-codeへ寄せない。

**D-02 — Archive separation**  
YMM4録画アーカイブPluginへ分類・学習責務を追加しない。Archiveは「使用録画範囲の保存・再リンク」を主責務のまま維持する。教材分類は見どころナビ外で人間が手作業で行ってよい。

**D-03 — Human label authority**  
`X4/戦闘` 等の人間分類を、そのProfileへのPositive membershipのAuthorityとする。別フォルダに入っていないことをHard Negativeとはみなさない。

**D-04 — Persistent learning representation**  
分類済み動画はFeature抽出後、再Profile評価に必要なSemantic前の軽量Feature Packへ変換して長期保存できる。動画そのものをCorpusの正本にしない。

**D-05 — Reverse classification for learning value**  
新サンプルはProfile更新へ混ぜる前に、現在のfrozen Profile群で逆分類する。期待Profileの一致が弱い、または競合Profileの方が強いサンプルをHard Example候補として優先する。

**D-06 — Multi-profile runtime**  
Profileは排他的分類ではない。Runtimeでは複数Profileを同時ONにし、HitをUnionしてReviewする。同一Episodeが複数ProfileへHitしてよい。

**D-07 — No silent self-training**  
逆分類結果だけで人間ラベルを書き換えない。Profile更新案はCorpus replayで回帰確認し、明示的な反映操作で新Profile revisionへcommitする。

# 3. CURRENT / FUTURE / OUT

## CURRENT

### Runtime Review

- YMM4 Tool Plugin / 日本語UI
- 1個以上のVideo ItemをTarget Set化
- Source range union / 重複Decode回避
- Session Feature Index
- audio dense features / visual low-mid-high
- source-adaptive normalization
- Profile 1-click ON/OFF
- 複数ProfileのHit Union
- Profile内部ConditionのANY / N-of-M / ALL
- global review sensitivity（高いほど広く拾う）
- Timeline順Review
- overlapping Profile hitsのEpisode merge / attribution保持
- query-independent Session Review History
- Background / Progress / Cancel
- FFmpeg GPU decode/resize + software fallback

### Learning Corpus

- 人間が分類したフォルダ / 動画群をProfile教材としてImport
- folder/profile labelをPositive membershipとして保持
- Persistent Learning Feature Pack生成
- sample provenance / feature schema version保持
- input動画をCorpus正本として要求しない
- user-owned教材Inboxでは、Pack検証成功後に入力動画を消費削除できる
- same sampleの重複Import検知
- Commonality extraction
- cross-profile Contrast suggestion
- Reverse Classification / Profile similarity
- Hard Example / learning-value candidate ranking
- profile update candidate作成
- existing Corpus replayによる回帰確認
- explicit commitでProfile revision更新
- old Profile revision / rollback可能な最小履歴

### Initial Profile Group

- `X4`
- 初期候補Profile: `戦闘` / `ステーション` / `MAP・UI` / `静→動` 等
- これらはX4固有hard-coded detectorではなく、Corpusまたはseed conditionから育てるProfile

## LOW-COST PREFERRED

安く成立する場合のみCURRENT。

- Progressive Review
- 解析負荷 `軽め / 標準 / 最速`
- density変更時のnested sample再利用
- Folder treeから `Group / Profile` を自動推定し、Import Previewで修正可能
-同一Feature Packへ複数Positive labelを付与して動画重複を避ける

## FUTURE

- Action / FPS-TPS / Strategy-RTS / Racing / Horror等の共有Seed Profile
- 複数ゲームの同名ProfileからGeneral Profile候補を抽出
- Profile同士の自動cluster / related-profile suggestion
- Motion Tracking / Optical Flow
- OCR / Object Detection / 高度Audio分類
- Advanced Feature Provider
- Session跨ぎRuntime Feature Cache
- 学習Corpus向け超低解像度Proxy保持（Feature Packだけでは将来の再抽出が不足すると判明した場合）

## OUT

- 録画アーカイブPluginへ分類UIを追加
- Folder名だけを根拠にHard Negativeを自動生成
- Reverse Classificationによる自動ラベル変更
- Profile同士を排他的classとして扱うこと
- 「一致度」を確率・正答率として表示すること
- auto cut / edit / effect / render
- AIによる「面白い」判定
- 重いML学習・大量教師データをFirst Value必須化
- Runtime Review時の元動画 / ymmp破壊

# 4. SETTLED DESIGN — OVERVIEW

## 4.1 Two independent flows

### A. Runtime Review Flow

```text
YMM4 Target Set
  ↓
Source Range Planner
  ↓
FFmpeg Analysis Backend
  ↓
Session Feature Index
  ↓
Source Normalization / Salience
  ↓
Enabled Profiles
  ├─ Profile: 戦闘
  ├─ Profile: ステーション
  └─ Profile: MAP
  ↓ independent match
Profile Hit Sets
  ↓ UNION
Episode Merge / Attribution
  ↓
Item Projection
  ↓
Review Navigator
```

### B. Learning Corpus Flow

```text
Human-classified clips/folders
  ↓
Corpus Intake Preview
  ↓
Feature Extraction
  ↓
Persistent Learning Feature Pack
  ↓
Reverse Classification against CURRENT frozen Profiles
  ↓
Learning Value / Hard Example ranking
  ↓
Commonality + Contrast analysis
  ↓
Profile Update Candidate
  ↓
Corpus Replay / Regression Check
  ↓ explicit Apply
New Profile Revision
```

Runtime ReviewとLearning CorpusはFeature primitiveを共有するが、Lifecycle / Storage / UXを分ける。

# 5. PROFILE MODEL

## 5.1 Profile

Profileは「場面の意味」を直接理解するAIモデルではなく、Feature primitiveとtemporal patternを組み合わせたSemantic Detector。

例:

```text
Profile: X4 / 戦闘
  inputs:
    audio burst
    visual delta
    brightness/extreme jump
    grid transition
    temporal burst/stability pattern
  composition:
    ANY / N-of-M / ALL / weighted condition
  tuning:
    source-adaptive threshold
    duration / merge / pre-post context
```

Exact internal representationは実装時に最小十分へ委任する。Heavy ML modelを前提にしない。

## 5.2 Profile Group

Profile Groupは主にゲーム / 用途単位のnamespace。

```text
X4
├─ 戦闘
├─ ステーション
├─ MAP・UI
└─ 静→動
```

別ゲームは別Groupへ追加できる。Groupは排他的classifierではない。

## 5.3 Review Preset

Review PresetはProfileそのものではなく、**よく使うProfile ON/OFF集合 + review settings**。

例:

```text
Preset: 編集候補ざっと確認
  [✓] 戦闘
  [✓] ステーション
  [ ] MAP
  sensitivity: 100%
```

これにより、Profile authoringとRuntime selectionを混同しない。

# 6. MULTI-PROFILE QUERY SEMANTICS

## 6.1 Independent detection

各Profileは同じSession Feature Indexへ独立適用する。

例:

```text
戦闘          30 hits
ステーション  10 hits
```

両方ONならraw hit totalは**40**。

## 6.2 Review Union

同じ時間帯へ複数ProfileがHitした場合、Review Queueでは一度だけ見せる。

```text
12:30.0 - 12:48.0
  hit: 戦闘
  hit: ステーション
```

したがってUIは必要に応じて、

- `Profile hit total`
- `Unique review candidates`

を区別する。

例:

```text
戦闘 30 / ステーション 10
候補 36件  |  ヒット計 40
```

40をUnique件数として誤認させない。

## 6.3 Merge

Profile Hit SetをUnionした後、近接Episodeは既存merge-gap rulesで統合してよい。統合後も、元HitのProfile attribution / time rangeを保持する。

## 6.4 Profile internal match

`ANY / N-of-M / ALL` はProfile間ではなく、原則**Profile内部Conditionの組み合わせ**に使う。

Profile同士は基本OR / Union。

# 7. HUMAN-CLASSIFIED LEARNING CORPUS

## 7.1 Preparation model

録画アーカイブPluginは分類しない。ユーザーはArchiveで短くなった録画等を、普通のFolder操作で大分類してよい。

推奨例:

```text
教材/
└─ X4/
   ├─ 戦闘/
   ├─ ステーション/
   ├─ MAP/
   └─ 探索/
```

分類粒度は「Featureを抜きたい特定場面」程度でよい。細かいタグ入力をFirst Valueへ要求しない。

## 7.2 Label semantics

`X4/戦闘` へ入れた動画は、**戦闘ProfileのPositive candidate**。

重要:

- `戦闘`に入っている ≠ 他Profileではない
- `ステーション`に入っている ≠ 戦闘Negative
- Folder未所属 ≠ Negative

X4では「ステーション周辺の戦闘」のようなmulti-label sceneが自然に存在し得るため、absenceを排他的Negativeにしない。

他Profile群はContrast sourceとして利用できるが、Hard Negative扱いはしない。

## 7.3 Intake

CURRENTの最小UX:

```text
[教材フォルダを追加]

推定:
  グループ     X4
  プロファイル 戦闘
  動画         24本

[✓] 解析・検証成功後、この取込フォルダの動画を削除

[解析開始]
```

Folder pathから推定できる場合は自動補完し、誤っていればImport Previewで修正できる。毎動画の個別入力は要求しない。

# 8. PERSISTENT LEARNING FEATURE PACK

## 8.1 Purpose

元教材動画を消した後でも、Profileの式・threshold・compositionを変更して再評価できること。

保存するのは最終Semantic scoreだけではなく、できるだけ一段手前の汎用Feature representation。

## 8.2 Candidate content

```text
LearningSample
├─ sample id / source fingerprint
├─ positive labels
├─ source duration / basic metadata
├─ extractor / feature schema version
├─ visual scalar time series
│  ├─ luma / contrast / chroma proxy
│  ├─ frame delta / histogram distance
│  ├─ edge density / extreme ratio
│  └─ grid summaries
├─ compact histogram / descriptor series
├─ audio time series
│  ├─ RMS / peak
│  └─ relative / context change
├─ temporal derived summaries
└─ provenance / import time / original display name
```

Exact binary formatは委任。Profile compatibilityを判定できるSchema versionは必須。

## 8.3 Storage rule

- **Session Feature Index:** 長尺Runtime Review用。原則Session lifecycle。
- **Learning Feature Pack:** 分類済み教材の長期Corpus。Persistent。
- **Profile / Condition:** Corpusから育てるSemantic knowledge。Persistent。

この3つを混同しない。

## 8.4 Raw video ownership

見どころナビは教材元動画を内部正本として保持しない。通常運用として、**教材Inboxへ置いた動画をFeature Pack化し、検証成功後に消費削除する**経路を持つ。

- Navigatorが内部staging copyを作る場合、Feature Pack確定後は保持しない
- `解析後に削除` はFolder単位の明示設定とし、毎ファイル確認を要求しない
- Pack書込 + reload検証 + sample登録commitが完了したファイルだけ削除対象にする
- Partial / Error / Cancel / schema validation failureでは該当sourceを削除しない
- Archive Project等が参照する外部ファイルを「Navigatorが不要になった」という理由だけで破壊しない
- 任意の外部Folderを勝手にConsumptive Inboxとみなさず、削除Authorityを明示した取込Folderだけを対象にする

これにより「解析後は動画を持たない」反復運用と、他ProjectのAuthority保護を両立する。

## 8.5 Future feature limitation

元動画削除後に、既存Packへ存在しない新Feature familyを後から抽出することはできない。

そのため:

- Current PackはSemantic scoreよりprimitive寄りに保存
- extractor/schema versionを保持
- 必須Feature不足のPackを黙って0/negative扱いしない
- Materialな新Featureが必要になった場合は、新しい教材からCorpusを更新するか、FUTUREのtiny proxy保持を検討

# 9. REVERSE CLASSIFICATION / LEARNING VALUE

## 9.1 Purpose

Reverse Classificationの主用途は「ラベルを自動決定すること」ではなく、**現在のProfileがまだ説明できない分類済み教材を見つけること**。

UI語は必要に応じて `近いプロファイル` / `分類チェック` とし、内部用語を強制しない。

## 9.2 Frozen-profile evaluation

新サンプルはProfile更新に混ぜる**前**に、CURRENT frozen Profile revisionで評価する。

```text
Human label: X4 / 戦闘

Current profile matches:
  戦闘          high
  ステーション  low
  MAP           low
→ 既知pattern寄り
```

または:

```text
Human label: X4 / 戦闘

Current profile matches:
  戦闘          low
  ステーション  medium-high
  MAP           low
→ 戦闘Profileでは説明不足
→ Hard Example候補
```

表示値は`一致度`であり、校正済み確率として扱わない。

## 9.3 Learning-value candidate

単純な「珍しさ」だけでは決めない。

第一候補の構成要素:

```text
Learning Value
  = expected-profile gap
  + competing-profile confusion
  + novelty vs existing positive corpus
  - invalid / low-information penalty
```

Exact formulaはProbeで決める。

High-value candidate例:

- Human labelのProfile matchが低い
- 別Profileの方が強く出る
- 既存positive corpusとFeature patternがMaterialに違う
- ただし破損 / 極端に短い / ほぼ無情報等ではない

## 9.4 Human authority

Reverse ClassificationはHuman labelを自動修正しない。

`戦闘folder`の動画が`ステーション`に近くても、まずは

> 「現在の戦闘Profileがこの戦闘を十分説明できていない可能性」

として扱う。

必要ならユーザーが分類ミスを訂正できるが、自動relabellingはしない。

# 10. PROFILE REFINEMENT

## 10.1 Commonality

同一ProfileのPositive Pack群から:

- common feature prevalence
- robust median / spread
- temporal pattern
- grid / descriptor similarity
- duration / burst structure

をrankingする。

## 10.2 Contrast

他Profile corpusとの差を見る。ただし他ProfileはHard Negativeではない。

- median difference
- robust spread difference
- prevalence gap
- profile-specific temporal pattern

などを「識別に役立つ候補」として使う。

## 10.3 Hard-example-guided refinement

Profile更新Candidateは、既知positiveをただ多数回学ぶより、Reverse Classificationで説明不足だったHard Exampleのcoverageを上げることを優先する。

ただしHard Exampleだけへoverfitしない。

## 10.4 Regression gate

Update Candidate適用前にPersistent Corpusを再生し、最低限:

- target Profileの既存positive coverageがMaterialに落ちない
- new hard example coverageが改善する
- 他Profile corpusへのhit explosionが起きない
- query costがMaterialに悪化しない

を確認する。

他Profile未所属をHard Negativeとしていないため、cross-profile hitの増加だけで即FAILにはしない。multi-label semanticsを維持する。

## 10.5 Commit

```text
改善候補があります
  戦闘: Hard Example 6件中 5件を追加coverage
  既存Positive: 大きな回帰なし

[Profileへ反映]
```

明示操作で新revisionへcommit。必要なら直前revisionへ戻せる。

# 11. FEATURE / BACKEND — PRESERVED BASELINE

## 11.1 Analysis backend

第一候補:

> **Plugin → FFmpeg child process → tiny frame / PCM → managed Feature extraction**

- supported NVIDIA: NVDEC/CUDA decode + GPU resizeをProbe
- failure / incompatible / slower: software decode fallback
- userへFFmpeg install / PATH設定を要求しない
- backend failure / cancelをchild process boundaryで扱う
- tiny Feature計算まで無理にGPU化しない
- exact FFmpeg build / license / versionは実装時pin

## 11.2 Initial visual probe defaults

v0.3.1のProbe値をAuthorityではなく初期候補として継承。

- Low / Medium / High: 1 / 2 / 4 fps
- working tiny frame: 64×36 candidate
- persistent luma descriptor: 32×18 candidate
- grid: 4×4 candidate
- histogram: 16 bins candidate

Core:

- luma / contrast / chroma-saturation proxy
- coarse histogram / occupancy
- previous-sample delta / histogram distance
- edge density / black-white extreme ratio
- grid luma / chroma / delta
- tiny descriptor similarity

Index derived:

- robust deviation
- stillness / duration
- stable→sudden / change start-end
- delta acceleration

Audio:

- hop 50ms candidate
- RMS / Peak 100ms candidate
- context 400ms + 3s candidate
- RMS / Peak / relative change / short-vs-context

Initial Coreから外す:

- Optical Flow
- OCR
- Object Detection
- dense embedding
- heavy pretrained semantic model

## 11.3 Source-adaptive salience

```text
raw feature
→ local/source robust deviation
→ empirical percentile / salience
→ Profile/Condition threshold
```

median + MAD第一候補、必要ならIQR fallback。

Sensitivityは全Profileで **高 = 広く拾う** を維持する。

# 12. YMM4 MAPPING — PRESERVED

Snapshot最低限:

- Item identity
- FilePath / Source identity
- ItemStartFrame / ContentOffset / Length
- PlaybackRate / Timeline FPS

CURRENTは正の一定PlaybackRate。exact semantics / roundingは実機ProbeをAuthorityとする。

同じSource時刻が複数ItemにあればFeature解析は1回、Candidateは各occurrenceへ別投影。

Target AdapterにYMM4 identity / timing / seek依存を隔離し、private/reflectionは必要最小限にする。

# 13. UX SPECIALIST REVIEW — APPLIED

UX SkillはPrimary Designを上書きせず、主要Taskの摩擦除去として以下を反映する。

## 13.1 Archiveへ分類を足さない — PRESERVE

分類をArchive UIへ統合すると、保存というPrimary Taskへ別Decisionを追加する。分類は後からFolderで行えるため、Archiveは単目的のまま維持する。

## 13.2 Folder Importはbatch first — ACCELERATE

毎動画へProfile名を入力させない。Folder単位でまとめてImportし、Group/Profileはpathから推定しPreviewで修正する。

## 13.3 Reverse Classificationを確率として見せない — PREVENT

内部scoreを`92%の確率で戦闘`のように見せない。`一致度` / `近いProfile`として扱い、Human labelをAuthorityとして保持する。

## 13.4 Multi-profile countを二重解釈させない — TRUTHFUL STATE

Profile別Hit合計とUnique Review Candidate数を分ける。

```text
戦闘 30  ステーション 10
候補 36件 / ヒット計 40
```

## 13.5 Main Reviewをauthoring UIで汚さない — REHIERARCHIZE

通常ReviewのPrimary controlsは:

```text
Profile ON/OFF
Global Sensitivity
前 / 次 / 一覧
```

Profile内部Conditionや学習解析は別の`Profile / 教材` surfaceへ分離する。

## 13.6 Profile更新をsampleごとの確認にしない — BATCH / PREVIEW

Hard Exampleごとに確認を要求せず、解析後にProfile update candidateをbatchで提示し、一度のPreview / Applyで反映できる。

# 14. END-USER SURFACES

## 14.1 Main Review

```text
グループ [ X4 ▼ ]

プロファイル
[✓ 戦闘 30] [✓ ステーション 10] [ ] MAP 18] [ ] 静→動 12]

検出感度  狭く ─────●───── 広く

候補 36件 / ヒット計 40
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]
```

同一候補が複数Hit:

```text
12:30.0  [戦闘] [ステーション]
```

Profile ON/OFF / sensitivity変更で再Decodeしない。

## 14.2 Learning / Profile

```text
X4 / 戦闘

教材 128 samples
Hard Example候補 9
Profile revision 7

[教材フォルダを追加]
[分析結果を見る]
[改善候補を確認]
```

教材Folder登録時に、必要なら `解析後に動画を削除` を一度選ぶ。以後は同じInboxへ置く反復作業で毎回確認させない。ただし削除対象・成功件数・失敗残存件数は処理結果で確認できる。

Feature名やthresholdは通常利用者へ常時露出しない。必要な場合のみ詳細へ展開。

## 14.3 Background ingest

長時間の教材解析はBackground実行。

表示するもの:

- current folder / current file
- 完了件数 / 総件数
- Feature Pack確定件数
- Error / skipped件数
- Cancel

Cancel / failure時に未確定sampleを成功扱いしない。

# 15. STORAGE / LIFECYCLE

## 15.1 Persistent

- Profile Group metadata
- Profile revisions
- Review Presets
- Learning Feature Packs
- sample labels / provenance
- Reverse Classification result cache（再計算可能なら最小限）
- Hard Example state / update candidate metadata

## 15.2 Session

- Runtime Feature Index
- Query Episodes
- Runtime Review History
- temporary descriptors / FFmpeg temp

## 15.3 Duplicate / relabel

同一教材が複数ProfileへPositive membershipを持つ場合、可能ならFeature Packを重複保存せずlabel relationだけ増やす。

Folder移動後もPack identityが壊れないよう、恒久identityをpathだけに依存しない。

## 15.4 Profile compatibility

Profile revisionが要求するFeature schemaをPackが満たさない場合:

- compatible subsetだけで黙って別意味のscoreを出さない
- incompatible / partial compatibilityを明示
- 必要ならそのPackを特定評価から除外

# 16. ACCEPTANCE

## 16.1 Runtime Review

READMEなしで:

```text
Target追加
→ Profileを複数ON
→ 解析
→ 前/次
→ YMM4 Jump
```

- 戦闘30 + ステーション10のようにProfile別Hitが得られる
- 両Profile ONでHit SetをUnion
- overlapping hitはReview上1候補へ統合
- merged candidateに全Hit Profile attributionが残る
- Profile ON/OFF / sensitivity変更で再Decodeしない
- default巡回Timeline順
- trim / ItemStart非0 / 正PlaybackRateでJump一致
- Selection変更でTargetが黙って変わらない

Initial query targetはv0.3.1を継承:

> 3h Medium scalar query warm p95 **300ms未満目標、最低1秒未満**。

Probe targetであり固定User Requirementではない。

## 16.2 Learning Intake

- Folder単位で複数動画を一括Importできる
- user-owned Inboxでは `解析・検証成功後に入力動画を削除` をFolder単位で選べる
- 削除ONでもPartial / Error / Cancel sampleは残る
- Group / Profileは推定可能なら自動補完しPreview修正可能
- 毎動画の細かいmetadata入力を要求しない
- Feature Pack確定前にsourceを「処理済み」と扱わない
- same sampleの重複Importを検知または安全にdedupe
- Human folder labelがPositive membershipとして保持される

## 16.3 Raw-video-free Corpus

- 元教材動画を外した状態でもProfile revisionを再評価できる
- Profile threshold / composition変更後に旧PackをCorpus replayできる
- Feature schema不足は明示される
- final semantic scoreだけでなくProfile再計算に必要なprimitive representationが残る

## 16.4 Reverse Classification

新しいHuman-labeled sampleについて:

- Profile更新前revisionでmatchを計算
- Expected Profile gapを検出可能
- competing Profile matchを保持
- scoreをcalibrated probabilityとして表示しない
- Human labelを自動変更しない
- Hard Example candidateを優先度付きで抽出できる

## 16.5 Profile Refinement

- Hard Exampleを使ったCandidate updateが作れる
- Existing positive corpusへReplayできる
- obvious regression / hit explosionを検出できる
- PreviewなしにProfileを黙って永続変更しない
- Apply後は新revisionとして保存
- previous revisionへ戻せる

## 16.6 Background / Backend

- 解析中YMM4 UIを持続blockしない
- Progress / Cancel
- supported環境でGPU path、失敗時software fallback
- backend failureでYMM4全体を不必要に巻き込まない

# 17. EXECUTION WAYPOINTS

## W1 — YMM4 Target / Projection Spine

v0.3.1継承。

**TARGET:** Target Set + dummy CandidateでSource time↔Timeline mapping。  
**EXIT:** Item identity / timing / seek / public-private boundaryを実機確定。

W1成立前にYMM4依存Scanner統合を大規模化しない。

## W2 — Shared Feature Engine

Runtime recordingとLearning clipの両方から同じprimitive feature schemaを生成できることを確認。

**EXIT:** Medium Feature Index / Learning Feature Packが同じProfile evaluatorへ入力できる。

## W3 — Learning Corpus Intake

Folder batch import → Feature Pack persist → source動画なしでreload / replay。

**EXIT:** X4の少数分類教材をPack化し、同一Profile evaluationを再実行できる。

## W4 — Multi-Profile Runtime Review

Profile別Hit Set → Union → overlap merge → attribution → Timeline review。

**EXIT:** 戦闘 + ステーション等を同時ONし、候補数が期待通り増減、重複Reviewしない。

## W5 — Reverse Classification / Hard Example

新Sampleをfrozen Profilesで評価し、expected-profile gap / confusion / noveltyからHard Example候補を出す。

**EXIT:** 人間が戦闘と分類したが現戦闘Profileで拾えないサンプルを再現可能に抽出。

## W6 — Profile Refinement / Regression

Commonality + Contrast + Hard ExampleからUpdate Candidate生成、Corpus replay、Preview / Apply / rollback。

**EXIT:** Hard Example coverage改善と既存CorpusのMaterial regressionなしを同時確認。

## W7 — First Value / Distribution

- X4 Profile Groupで複数Profile高速巡回
- 学習Corpusをraw-video-freeで維持
- background usability
- normal YMM4 distribution

Goal到達後、他Genre / heavy detectorへ自動拡張しない。

# 18. REQUIRED INPUTS / VERIFICATION

## Implementation Required

**R1 — Actual YMM4 target environment**  
Tool host / selection / timing / seek / UI responsiveness確認。

**R2 — Representative raw game recording**  
Runtime backend / query / projection smoke用。第一対象はX4。

**R3 — Small human-classified learning folders**  
例:

```text
X4/戦闘      several clips
X4/ステーション several clips
X4/MAP       several clips
```

大量CorpusはImplementation開始blockerにしない。W3/W5の成立確認に必要な最小数だけでよい。

## Agent-generated

- same-source multi-item fixture
- deterministic FFmpeg feature fixture
- synthetic near-duplicate / incompatible schema packs
- profile regression fixture

## Later validation

実運用Archiveから分類済みCorpusを増やし、Profile coverage / Hard Example収束 / review reductionを評価する。

# 19. TECHNICAL REALITY / VALIDITY

## Preserved evidence

v0.3.1で確認済みの以下は今回のDeltaで否定されないため再利用する。

- YMM4 Tool Pluginのruntime integrationは実機Probeが必要
- FFmpeg child process + GPU decode/resize + software fallbackは第一候補として現実的
- cheap multimodal / temporal feature compositionはProfile primitiveとして利用可能
- semantic heavy AIをFirst Value必須にしなくてよい

今回追加したLearning Corpus / Reverse Classification / Multi-Profile Unionは主に内部Data / Query / UX layerの設計であり、新しい外部Provider成立性を前提にしない。

## OPEN / Probe required

- Feature Pack容量 / sampleあたり目標サイズ
- Primitive保存量と再Profile可能性の最適点
- reverse classification score normalization
- learning-value formula
- Profile update search / optimization方法
- cross-profile hit explosionのmaterial threshold
- same-sample dedupe fingerprint
- profile schema migration

これらはGoal / User workflowを変えない限り実装Probeへ委任する。

# 20. RISK / REOPEN

Main Risk:

- **Corpus information loss:** 元動画削除後に新Featureを再抽出できない
  - primitive寄りPack + schema version
  - 不足時は新Corpusで補う
- **Self-reinforcing profile drift:** 現Profileの判定を正解label化してしまう
  - Human label authority
  - frozen-profile evaluation before update
  - explicit commit
- **False exclusivity:** 別Profile教材をHard Negative扱いしてmulti-label sceneを壊す
  - membership positive / absence unknown
- **Hard-example overfit:** 難例だけに合わせて既知patternを落とす
  - Corpus replay / regression gate
- **UI count confusion:** 30+10=40とUnique candidate数を混同
  - hit totalとcandidate count分離
- **Archive scope creep:** Archiveへ分類・学習UIを追加しPrimary Taskを重くする
  - Plugin責務を分離
- **Consumptive intake deletion:** Pack確定前や外部Authorityの動画を消してしまう
  - Folder-level explicit ownership + verified-commit-before-delete + partial failure preservation

REOPEN:

- Learning Feature Packが大きすぎて元動画削除メリットをMaterialに損なう
- 元動画なしではProfile改善に必要な情報が頻繁に不足する
- cheap FeatureだけではHuman分類Profileのcoverageが閉じない
- Multi-profile Unionで候補密度が高すぎReview Reductionを失う
- Reverse Classification scoreが学習価値のrankingへ使えない
- Corpus replayでProfile更新の回帰を十分検出できない
- YMM4 integrationがTarget Adapter隔離で成立しない

# 21. AUTHORITY / PRECEDENCE / HANDOFF

Authority / Precedence:

1. ユーザーのCurrent Goal / D-01〜D-07 / Priority / Scope
2. 本v0.4.0 Current Design
3. v0.3.1のPRESERVE対象
4. Technical Evidence / Probe結果
5. 実装Convenience

Delegation:

- exact Pack binary format
- exact fingerprint / dedupe algorithm
- exact learning-value formula
- exact profile optimization method
- exact feature sample rates / descriptor sizes
- exact cache / queue details
- exact FFmpeg build / packaging

再確認が必要:

- Human classification workflowをMaterialに増やす
- Archive Pluginへ分類 / learning責務を移す
- 元動画を保持必須へ戻す
- Profileを排他的classificationへ変更
- Heavy ML / OCR / Object DetectionをCURRENT必須化
- Profile updateをsilent auto-learningへ変更
- Runtime Reviewで元動画 / ymmpを破壊変更

# 22. FINAL INTENT

> **人間は「これは戦闘」「これはステーション」程度の大分類だけ行う。見どころナビはその素材から軽量Featureを残し、元動画なしでProfileを何度でも再評価・改善する。既存Profileで説明できない分類済み素材ほど優先して学び、Runtimeでは必要なProfileを複数ONにしてHitを足し合わせ、重複だけまとめて高速巡回する。**

Operational image:

```text
YMM4編集
  ↓
録画アーカイブで使用部分を短く保存
  ↓
人間がFolderでざっくり分類
  ↓
YMM4見どころナビへ教材Import
  ↓
Learning Feature Pack化
  ↓
元教材動画はNavigatorの正本ではない
  ↓
Reverse ClassificationでHard Example発見
  ↓
Profile改善
  ↓
新しい長尺録画へ複数Profileを同時適用
  ↓
候補をTimeline順に高速Review
```

**X4は最初の強いProfile Groupであり、YMM4見どころナビそのものの上限ではない。**
