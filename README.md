# YMM4見どころナビ

YukkuriMovieMaker4（YMM4）で、長尺ゲーム録画から「見るべき候補」を軽量Featureの組み合わせで探し、タイムライン上を高速に巡回するTool Pluginです。

**現在の状態:** v0.4.0 設計確定・実装準備。Plugin本体はまだ未実装です。

## 目指す使い方

通常のレビューでは、複数のProfileを同時にONにします。

```text
[✓ 戦闘]          30 hit
[✓ ステーション]  10 hit
[ ] MAP            18 hit

候補 36件 / ヒット計 40
[ ◀ 前 ] [ 次 ▶ ] [ 一覧 ]
```

Profileは排他的な分類器ではありません。同じ場面が「戦闘」と「ステーション」の両方にHitしてよく、Review Queueでは時間重複だけを統合します。

Profileは、人間がざっくり分類した短尺素材から育てます。

```text
録画アーカイブ等で短くなった素材
  ↓
人間がフォルダで大分類
  X4/戦闘
  X4/ステーション
  X4/MAP
  ↓
YMM4見どころナビでFeature解析
  ↓
Persistent Learning Feature Pack
  ↓
元教材動画はNavigatorの正本にしない
  ↓
既存Profileで説明できないHard Exampleを優先
  ↓
Profile改善
```

X4 Foundationsは最初に強く育てるProfile Groupですが、製品ScopeはX4専用ではありません。

## Repositoryの役割

このRepositoryは**製品本体**の正本です。

```text
ziro-lab/chat-native-work-lab-001
  YMM4本体の挙動・API・version差分をProbeして知見を蓄積
                ↓ verified behavior
ziro-lab/ymm4-highlight-navigator
  製品設計・実装・pure test・製品としてのnative regression
                ↓
  YMM4 Pluginとして配布・実機確認
```

YMM4自体について未知の挙動が出た場合、このRepositoryで推測や重複実験をせず、まずLabで最小Probeを行います。こちらのGitHub Actionsは、Labで得た事実を使った**Plugin製品としての実機確認**へ絞ります。

録画アーカイブPluginとは密結合しません。アーカイブは録画の保存・再リンクを担当し、教材の分類・学習は本Plugin側の責務です。

## Current authority

- [設計正本 `docs/DESIGN.md`](docs/DESIGN.md) — v0.4.0 Learning Corpus / Multi-Profile / Reverse Classification
- [実装開始点 `docs/IMPLEMENTATION_KICKOFF.md`](docs/IMPLEMENTATION_KICKOFF.md)
- [実装Roadmap `docs/ROADMAP.md`](docs/ROADMAP.md)
- [開発運用 `docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
- [Native検証方針 `docs/NATIVE_VALIDATION.md`](docs/NATIVE_VALIDATION.md)
- [Lab evidence索引 `docs/LAB_REFERENCES.md`](docs/LAB_REFERENCES.md)
- [W1 Lab Question Set `docs/W1_LAB_QUESTIONS.md`](docs/W1_LAB_QUESTIONS.md)
- [W2 Feature Engine Kickoff `docs/W2_KICKOFF.md`](docs/W2_KICKOFF.md)
- [初期並列lane `docs/BRANCHES.md`](docs/BRANCHES.md)

実装時の優先順位は、ユーザーのCurrent Goal / Material Decision → `docs/DESIGN.md` → LabのCurrent Evidence → 実装都合、の順です。

初期laneは `feature/w1-projection-spine` と `feature/w2-feature-engine`。W1はYMM4 host integration、W2はYMM4非依存Feature Engineとして並行できます。

## Development posture

- cheap Feature first。Heavy ML / OCR / Object DetectionをFirst Valueの必須にしない。
- Session Feature Index / Persistent Learning Feature Pack / Profileを別lifecycleとして扱う。
- 人間ラベルをAuthorityにし、逆分類だけで自動relabellingしない。
- Profile更新はCorpus replay + Preview + explicit Apply。
- 元動画・ymmpを通常Reviewで破壊変更しない。
- 教材動画を消費削除する場合は、明示したInboxだけを対象にし、Feature Pack確定・reload検証・sample登録commit後だけ削除する。
- YMM4 host factの調査はLabへ寄せ、Product Actionを事実探索に浪費しない。
- W1 integration codeが存在しない現在はnative workflowを置かない。製品claimを証明できる段階でのみNavigator側Actionsを追加する。
