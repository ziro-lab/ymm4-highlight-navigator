# YMM4見どころナビ

YukkuriMovieMaker4（YMM4）の長尺録画から軽量な映像・音声特徴で候補を探し、複数プロファイルを同時に使ってタイムラインを巡回するTool Plugin。

**現在: W1/W2の最初の実装段階。完成品・一般配布版ではありません。** 機能別の実装範囲と検証結果は [最新チェックポイント](docs/IMPLEMENTATION_STATUS.md) を正本にします。v0.4.0は設計全体の版であり、W1〜W7すべてが完成したことを意味しません。

## 実装済みの基盤

- 選択VideoItemを明示的に対象化。選択を変えても対象を勝手に変更せず、位置・速度・素材が変わったら古い対象へのJumpを拒否。
- 同一録画の重複範囲をまとめてCPUで背景解析。YMM4の生オブジェクトはUIスレッド側に隔離。
- 映像・音声の汎用特徴、軽量Packの保存・再読込、破損／欠損／スキーマ不一致の検出。
- 複数プロファイルの独立評価、OR統合、重複区間の整理、帰属情報を残した時系列候補。
- プロファイルON/OFFと感度変更時は動画を再Decodeせず再検索。
- 日本語の対象指定・解析・中止・前／次・候補一覧。

現在の初期プロファイルは **映像の急変／音の強い場面／明るい場面** という汎用条件です。「戦闘」「ステーション」を学習済みと偽って表示しません。人間分類フォルダからのCorpus登録、逆分類、改善候補の反映は次の工程です。

## 目指す運用

録画アーカイブなどで保存した短尺素材を、人間が `X4/戦闘`、`X4/ステーション` などの普通のフォルダに分けます。Navigatorはそこから軽量な学習用特徴を保持し、明示的にプロファイルを改善します。学習後の長尺検索では複数プロファイルを同時ONにし、候補を足して巡回します。

30件と10件の検出ならヒット計40件。同じ時間帯が重なればレビュー候補は例えば36件です。同じ録画をタイムラインで二度使っている場合は、別の使用箇所として保持します。

X4は最初の検証対象・Profile Groupであり、製品の対応範囲をX4専用にはしません。

## 開発と検証

[設計](docs/DESIGN.md) / [実装状況・証拠](docs/IMPLEMENTATION_STATUS.md) / [Roadmap](docs/ROADMAP.md) / [次の着手点](docs/IMPLEMENTATION_KICKOFF.md) / [特徴形式](docs/FEATURE_FORMAT.md)

- Core: .NET 10、YMM4非依存。`dotnet run --project tests/Ymm4HighlightNavigator.Core.Tests -c Release -- --out out/core-tests` でpureケースを実行。実FFmpegケースの実行方法は `.github/workflows/core.yml` を参照。証拠出力先は毎回空の専用ディレクトリを使う。
- Plugin: YMM4 Lite 4.56.1.0を対象に、`YMM4DirPath`を渡してビルド。起動や回帰テストは `.github/workflows/native.yml`。実行条件・結果はチェックポイントを参照。
- FFmpegの通常配布・ライセンス／更新方針と `.ymme` は未完了。Actions成果物のDLLだけを完成済みインストーラーとして扱わない。テストは一時hostへbackendを配置するが、そのbinaryは成果物に含めない。
- この実装段階には入力動画の削除機能を入れていない。Pack保存成功だけで削除を許可しない。

## Repository境界

YMM4本体の未確認挙動は [chat-native-work-lab-001](https://github.com/ziro-lab/chat-native-work-lab-001) で最小検証し、[採用Evidence](docs/LAB_REFERENCES.md) をpinします。Navigator側は製品の機能・UI統合を検証します。Lab PASS、製品native PASS、配布・実利用の合格は別です。

録画アーカイブとは独立したrepo・コード・workflow・一時作業場を使います。分類UIや学習責務をアーカイブへ追加せず、その実験コードをNavigator用に変更しません。知識は共有し、製品コードは密結合させません。

詳細は [AGENTS.md](AGENTS.md)、[開発運用](docs/DEVELOPMENT.md)、[Native検証方針](docs/NATIVE_VALIDATION.md)。
