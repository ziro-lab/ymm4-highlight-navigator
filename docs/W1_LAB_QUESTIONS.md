# W1 question closure / remaining boundaries

W1開始前の質問セットは、Recording Archive evidenceと追加navigation-target-context experimentで基本経路が解消した。**同じQ1〜Q4を未調査として再送しない。** 採用元は [LAB_REFERENCES.md](LAB_REFERENCES.md)、製品統合の結果は [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md)。

## Q1 — Target snapshot / identity

ADOPTED: public TimelineToolInfo.Timeline、Timeline.VideoInfo.FPS、Timeline.SelectedItems。実装はsession object referenceにNavigatorのsnapshot Guidを対応させ、同じ素材でも別Item occurrenceを維持する。

選択変更で対象が入れ替わらないこととempty capture時のatomicityはproduct proofに含む。永続的なItem identityやreload/Undo後の同一性は証明していない。別Timelineを受けたらinvalidateし、現在のmembership/parametersをJump前に検査する。

## Q2 — Time projection / rounding

ADOPTED: PlaybackRate2、native PlaybackRateMap、50/100/200%、offsetを倍率に含めない定速式、inverseのexclusive-end境界。ContentLengthはmedia durationでありusage durationではない。

絶対Timeline frameはItem.Frameを加えて得る。fractional item-local timeには最初の表現可能frameへ切り上げる **product policy** を適用し、native mapで候補内に残るか検査する。Labは一般的なhost roundingを証明したわけではない。

nonzero start/offset、200%と50%の同Source別occurrence、subframe候補、終端を含まない挙動はCore/nativeの該当テストを再利用する。

## Q3 — Seek / Tool context

ADOPTED: public Timeline.CurrentFrameのinteger set/readback。product proofでは登録済みの実Toolを表示し、そのmodelの候補Jumpが期待frameへ到達することを確認。

callback receiptだけから可視View作成やmenu activationを推定しない。物理マウス/キーボードやdecoderが表示したframeとの照合は別の未証明Claim。現在は受け取ったTimelineだけを対象にし、横断scene巡回を勝手に追加しない。

## Q4 — Snapshot lifetime

selectionとは独立して保持し、Item変更時のstale拒否、adapter.detach/Disposeによる解放を検証。実装はtimeline instance差替えでinvalidateする。

残る広いacceptance: 実Project reload、scene switch、削除/recreate、Undo/Redo、背景解析中の切替・終了が重なる操作。現在の狭いPASSをこれらの合格へ拡大しない。変更する製品経路に応じて必要なfixtureだけ追加する。

## Reopen / budget

新host版、未知のhost挙動、既存Evidenceとの矛盾のみLabの新規Questionにする。通常のNavigator実装不具合は製品regression側で直す。variable/reverse再生はCURRENTへ昇格するまで探索を増やさない。既存Archive Probeは変更しない。
