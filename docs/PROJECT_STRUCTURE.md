# Planned project structure

W1 / W2を並行しても衝突しにくいよう、初期の物理境界だけ先に決める。実装開始前の名前であり、Goalを変えない範囲の局所調整は可。

```text
src/
├─ Ymm4HighlightNavigator.Core/
│  └─ YMM4非依存: Feature model / Pack / Profile / Corpus / Episode logic
└─ Ymm4HighlightNavigator.Plugin/
   └─ YMM4依存: Tool Plugin / Target Adapter / UI / Timeline projection

tests/
├─ Ymm4HighlightNavigator.Core.Tests/
│  └─ pure deterministic tests
└─ Ymm4HighlightNavigator.Plugin.Tests/
   └─ product-side integration helpers / host-independent adapter tests where possible

tools/
└─ fixtures/
   └─ deterministic FFmpeg fixture generation / validation helpers

docs/
└─ design / roadmap / evidence / native acceptance
```

## Dependency direction

```text
Plugin -> Core
Core -X-> Plugin
Core -X-> YMM4 assemblies
```

YMM4 private/reflection dependencyをCoreへ漏らさない。

## W1 ownership

`feature/w1-projection-spine` は主に:

- `src/Ymm4HighlightNavigator.Plugin/`
- Plugin-side adapter tests
- W1 native smoke（integration成立後）

を扱う。

## W2 ownership

`feature/w2-feature-engine` は主に:

- `src/Ymm4HighlightNavigator.Core/`
- `tests/Ymm4HighlightNavigator.Core.Tests/`
- `tools/fixtures/`

を扱う。

これにより、W1がLabのYMM4 fact待ちでもW2を独立して進められる。

## Do not pre-split further

Feature extraction / Corpus / Profile / Queryを最初から別assemblyへ細分化しない。CoreがMaterialに大きくなり、独立versioning/test boundaryが必要と実測で分かるまで1つのCoreに保つ。
