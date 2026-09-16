# Implementation continuation

W1/W2のコードは既にあります。初期のゼロからの実装指示を繰り返さず、[実装状況](IMPLEMENTATION_STATUS.md) と現在のbranch/PRを最初に確認してください。詳細の要件は [DESIGN.md](DESIGN.md)。

## 次の主工程 — W3 Learning Corpus Intake

現在の `FeaturePack` / `PackStore` / `FfmpegBackend` を再利用し、分類済みフォルダから学習用Packとサンプル登録を一括で作る流れを追加します。

1. Folder -> Group/Profileをpreviewできるbatch入力。分類は人間が行い、録画アーカイブ側にUIを追加しない。
2. Packと分類membership・provenanceを結び、同一素材の再取込／複数ラベルを二重学習しない。
3. Pack書込・reload検証・sample登録のcommitを区別。元動画を外した状態でCorpusを再読込できることをテスト。
4. 最初に非破壊Importを成立させる。消費削除を追加する場合だけ、明示された専用Inboxの所有権・部分失敗・中止・復旧・他project参照への影響を別テストする。Pack生成成功だけを削除Authorityにしない。
5. その後W5のfrozen-profile評価とW6の改善候補／replay／明示反映へ進む。

## 残す境界

- CoreはYMM4を参照しない。hostの生オブジェクト、reflection、DispatcherはPluginのAdapter側。
- `PlaybackRate2` / `PlaybackRateMap` の採用済み定速意味を再発見しない。
- 初期プロファイルは汎用条件。実データなしにX4分類精度を主張しない。
- 他フォルダへの所属はHard Negativeではない。逆分類結果を勝手に正解ラベルにしない。
- runtime候補統合は同一Sourceの異なるItem occurrenceを消さない。
- 純粋なCorpus/学習ロジックの変更はLinux側tests。Nativeはhost/UI/実行境界を変えるcheckpointで必要な範囲だけ。
- 通常配布・GPU・長尺負荷・実X4のRecallは未検証項目として明示して管理する。

## 最初に実行する確認

現在のCore regressionを実行し、変更箇所のnegative testsを追加する。W1を触る場合は記録済みの製品native checkpointを確認してから必要なテストだけ更新する。証拠のsource headと実際のcheckoutを混同しない。
