# YMM4見どころナビ

YukkuriMovieMaker4（YMM4）の長尺録画から軽量な映像・音声特徴で候補を探し、複数フィルターを同時に使ってタイムラインを巡回するTool Plugin。

**現在: W1/W2/W4の基盤実装済み、Transition Filter学習はこれから。完成品・一般配布版ではありません。** 機能別の実装範囲と検証結果は [最新チェックポイント](docs/IMPLEMENTATION_STATUS.md) を正本にします。v0.4.1は設計全体の版であり、W1〜W7すべてが完成したことを意味しません。

## 実装済みの基盤

- 選択VideoItemを明示的に対象化。選択を変えても対象を勝手に変更せず、位置・速度・素材が変わったら古い対象へのJumpを拒否。
- 同一録画の重複範囲をまとめてCPUで背景解析。YMM4の生オブジェクトはUIスレッド側に隔離。
- 映像・音声の汎用特徴、軽量Packの保存・再読込、破損／欠損／スキーマ不一致の検出。
- 複数Profileの独立評価、OR統合、重複区間の整理、帰属情報を残した時系列候補。
- Profile ON/OFFとGlobal Sensitivity変更時は動画を再Decodeせず再検索。
- 日本語の対象指定・解析・中止・前／次・候補一覧。
- 解析backendはPluginへFFmpegを同梱せず、YMM4自身の同梱 `ffmpeg.exe` / `ffprobe.exe` をhost capability経由で利用する。
- YMM4のversion番号だけではPluginを拒否しない。依存関係が変わった場合は該当機能をfail closedする方針。

現在の初期Profileは **映像の急変／音の強い場面／明るい場面** という汎用条件です。「戦闘」「ステーション」を学習済みとは扱いません。

## 次に作るもの

主軸は、短尺教材から**場面の切り替わり方**を覚えるTransition Filterです。

```text
短尺教材
→ Feature Pack
→ before / transition / after の変化候補
→ Filter Candidate
→ Positive教材へ再適用
   ├ Covered Positive
   └ Hard Positive
→ 長尺録画へ適用
→ Sensitivityで候補量を調整
→ 前 / 次で高速Review
```

Primary Goalは **Recall / 見逃し低減**。Precision最大化より、見逃しを増やさず候補量を実用域へ下げることを優先します。

実運用で誤検出した候補は将来 `これは違う` と指定し、その前後FeatureをExplicit NegativeとしてFilter改善へ使います。他Folderや他Profileへの未所属は自動Hard Negativeにしません。

同じFilterでも場面の入り方が複数ある場合は、1つの平均patternへ潰さず `Pattern A OR Pattern B ...` を許します。

## 教材の準備

普通の動画/Folderをそのまま教材として使えることが前提です。

録画アーカイブPluginは、過去編集から見どころ短尺素材を楽に作れるため相性が良いですが、Navigatorの依存先ではありません。自分で切り出した動画や既存素材も同じ入口で扱います。

X4は最初の検証対象・Profile Groupであり、製品の対応範囲をX4専用にはしません。

## Runtime Review

メイン画面では常用操作を絞ります。

```text
Filter / Profile ON/OFF
Global Sensitivity
candidate / hit count
Prev / Next / List
```

Sensitivityは高いほど広く拾い、低いほど候補を絞ります。変更で再Decode・再学習しない設計を維持します。

30件と10件のHitならraw hit totalは40。同じ時間帯が重なればReview候補は例えば36件です。同じ録画をタイムラインで二度使っている場合は別の使用箇所として保持します。

## 開発と検証

[設計](docs/DESIGN.md) / [実装状況・証拠](docs/IMPLEMENTATION_STATUS.md) / [Roadmap](docs/ROADMAP.md) / [次の着手点](docs/IMPLEMENTATION_KICKOFF.md) / [特徴形式](docs/FEATURE_FORMAT.md)

- Core: .NET 10、YMM4非依存。`dotnet run --project tests/Ymm4HighlightNavigator.Core.Tests -c Release -- --out out/core-tests` でpureケースを実行。実FFmpegケースの実行方法は `.github/workflows/core.yml` を参照。
- Plugin: YMM4 Lite 4.56.1.0でnative regression済み。起動や回帰テストは `.github/workflows/native.yml`。検証Evidenceはexact host versionへpinするが、runtime互換はversion番号ではなく必要capabilityで判断する。
- YMM4 Lite 4.56.1.0では `Resources\bin\x64\ffmpeg\` の同梱backend利用をLabと製品native testの双方で確認済み。Navigator独自のFFmpeg copy、PATH探索、別インストールは不要。
- `.ymme` の通常導入、upgrade、実利用acceptanceは未完了。Actions成果物のDLLを完成済みインストーラーとして扱わない。
- 現在の実装にはLearning Corpus intake、Transition Filter authoring、Explicit Negative feedback、入力動画削除機能はまだ入っていない。

## Repository境界

YMM4本体の未確認挙動は [chat-native-work-lab-001](https://github.com/ziro-lab/chat-native-work-lab-001) で最小検証し、[採用Evidence](docs/LAB_REFERENCES.md) をpinします。Navigator側は製品の機能・UI統合を検証します。Lab PASS、製品native PASS、配布・実利用の合格は別です。

録画アーカイブとは独立したrepo・コード・workflowを維持します。Archiveは教材準備を楽にする別Pluginであり、Navigatorから分類/学習責務を押し込みません。

詳細は [AGENTS.md](AGENTS.md)、[開発運用](docs/DEVELOPMENT.md)、[Native検証方針](docs/NATIVE_VALIDATION.md)。
