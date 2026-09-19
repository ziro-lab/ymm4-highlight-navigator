# Development workflow

このRepositoryは、`chat-native-work-lab-001` でYMM4自体の挙動を確認し、その確定事実を製品実装へ持ち込む運用を前提にする。

## 1. Task開始時

最低限、次を確認する。

```text
AGENTS.md
  ↓
docs/DESIGN.md      Current requirement / architecture / acceptance
  ↓
docs/ROADMAP.md     current waypoint / dependency
  ↓
docs/LAB_REFERENCES.md
  ↓
docs/NATIVE_VALIDATION.md
```

既にLabで確定済みのFactを推測し直さない。逆に、未確認のYMM4挙動を製品コード内のreflection試行錯誤で事実化しない。

## 2. Unknown YMM4 factが出たとき

製品側で必要なQuestionを小さくする。

例:

```text
NG: 「Navigatorを動かす方法を調べる」
OK: 「current YMM4で選択VideoItemのidentity / ContentOffset / Length / PlaybackRateを
     Tool Pluginから取得する最小public surfaceは何か」
```

そのQuestionだけを `ziro-lab/chat-native-work-lab-001` へProbeとして送り、再利用可能なEvidenceを残す。

Lab結果を採用するときは `docs/LAB_REFERENCES.md` に:

- Lab commit / experiment
- exact adopted claim
- tested YMM4 version / condition
- Navigatorで使う箇所
- invalidation/reopen condition

を追加する。

## 3. Implementation order

原則はRoadmapのW1-W7。

ただし完全直列ではない。

```text
W1 Target/Projection baseline ─┐
                              ├─ W2 Shared Feature Engine
W3 Learning Corpus/Transition ┘
               ↓
W4 Multi-Filter Runtime Review
               ↓
W5 Initial Transition Filter authoring / coverage
               ↓
W1-R Edit-time rebinding
               ↓
W4-M Highlight memo capture
               ↓
W6 Explicit Negative / refinement
               ↓
W7 Real use / performance / distribution
```

W1/W2/W3/W4/W5のbaselineは既にある。現在はLab確定済みのedit/memo host factsを再調査せず、W1-R/W4-Mのpure model → product integrationの順で進める。

## 4. Branch / PR posture

推奨branch:

- `feature/w1-projection-spine`
- `feature/w2-feature-engine`
- `feature/w3-learning-corpus`
- `feature/w4-multi-profile-review`
- `feature/w5-transition-filter-authoring`
- `feature/w1r-edit-time-rebinding`
- `feature/w4m-highlight-memo-capture`
- `feature/w6-profile-refinement`
- `feature/w7-distribution`

PRには最低限:

- affected waypoint / DESIGN decisions;
- user-visible change;
- pure tests;
- reused Lab evidence;
- native proofが必要か、不要ならなぜか;
- remaining OPEN / REOPEN condition;

を短く残す。

## 5. Test pyramid

### Pure / deterministic first

YMM4を起動せず確認できるものはここで閉じる。

- source range / timing math after host semantics are fixed;
- Candidate AnchorSourceTime / stable review order;
- occurrence lineage replacement selection / ambiguity rejection;
- Feature primitive calculations;
- Feature Pack serialization/schema compatibility;
- fingerprint/dedupe behavior;
- Profile evaluator;
- Multi-Profile hit union / episode merge / attribution;
- Transition matcher / coverage replay;
- Explicit Negative / contrast logic when W6 resumes;
- Corpus replay / Profile regression gate;
- cancellation/idempotency of internal jobs where host-independent.

### Lab native probe

YMM4 host factそのもの。

### Product native smoke

Lab factを使ったNavigator Pluginとしてのintegration。

### Release proof

UI/UX、package/install/upgrade、evidence consumerまで含める。

## 6. Fixtures

実録画をrepoへ置かない。

- FFmpegで決定論的な短いtest video/audioを生成する。
- source time/frameを画面・音で判別可能にできるfixtureを優先する。
- Learning Corpusの大半はFeature Pack synthetic fixtureで済ませる。
- incompatible/old schema、near-duplicate、corrupt pack、cross-profile overlap等のnegative fixtureを作る。

実際のX4録画はhands-on / private validation入力であり、公開repoのtest assetにしない。

## 7. Documentation update rule

Current stateを変えたら正本を更新する。

- material design change -> `docs/DESIGN.md`
- waypoint / dependency / exit change -> `docs/ROADMAP.md`
- host Evidence adoption -> `docs/LAB_REFERENCES.md`
- native proof contract -> `docs/NATIVE_VALIDATION.md`

同じSemantic Roleの新しいhandoff文書を増殖させない。
