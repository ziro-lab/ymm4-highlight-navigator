# Active development lanes

現在の初期並列lane:

```text
feature/w1-projection-spine
  YMM4 host integration / Target / Projection
  unknown host facts -> chat-native-work-lab-001

feature/w2-feature-engine
  YMM4-independent Shared Feature Engine
  deterministic fixtures / schema / pack groundwork
```

W1とW2は並行可能。W1でYMM4 native factを待っている間も、W2のpure workを止めない。

W3以降のbranchは、そのWayPointへ実際に着手するときに作る。空branchを大量に先行作成しない。
