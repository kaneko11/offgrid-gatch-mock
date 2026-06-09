# v2 Shims Experimental Design

## 1. 目的

このドキュメントは、MiniMockito.Net v2 本体では扱わない以下の領域を、将来 `MiniMockito.Shims.Experimental` として分離して検証するための設計調査です。

- direct `new` interception
- static method mocking
- sealed class mocking
- non-virtual method mocking
- constructor interception
- runtime IL rewrite
- profiler API based shim

この Phase では実装しません。MiniMockito 本体にも runtime rewrite / profiler / direct `new` / static / sealed / non-virtual の差し替え機能を入れません。

### なぜ proxy では扱えないのか

v1 の interface proxy は `DispatchProxy` を使い、呼び出し先を interface proxy instance に置き換えます。利用者コードが interface 経由で呼び出す場合だけ、呼び出しは proxy の `Invoke` に集約されます。

v2 の class proxy は対象 class を継承した generated class を作り、public virtual method を override します。利用者コードがその proxy instance を呼び出し、かつ呼び出し対象が virtual dispatch される場合だけ interception できます。

そのため、以下は interface proxy / class proxy の範囲外です。

- direct `new`: 既存コード内の `new SomeClass()` は concrete type を直接生成するため、外側から proxy instance に差し替えられない
- static method: static dispatch は instance proxy を通らず、override もできない
- sealed class: class proxy は継承が前提なので、sealed class から派生できない
- non-virtual method: virtual dispatch が発生しないため、派生 class の override で差し替えられない
- private method: 外部 API から呼び出せず、override もできない
- constructor: constructor 呼び出しそのものは通常の virtual method dispatch ではない

Microsoft Fakes Shim 相当の機能は、proxy instance を使うのではなく、実行時またはビルド時に call site や method body の解決先を書き換える領域です。これは通常の mock よりも実行環境、並列実行、デバッグ、保守性への影響が大きいため、MiniMockito 本体から分離します。

## 2. 方式比較

### 2.1 runtime IL rewrite

概要:

- 実行中または test setup 時に assembly / method body / call site の IL を書き換え、`new`, `call`, `callvirt` などの解決先を shim dispatcher へ向ける方式
- static method、constructor call、non-virtual method call を理論上差し替えられる

できること:

- source code を変更せずに direct `new` や static call を差し替える可能性がある
- 対象を call site 単位に絞れる設計なら、特定 test scope だけの差し替えを表現できる
- BCL 以外の user assembly であれば、比較的現実的な検証対象にできる

できないこと:

- すでに JIT 済みの method、ReadyToRun、AOT、trimming 環境では制約が大きい
- .NET runtime の version や loader の挙動に強く依存する
- private implementation detail に寄りやすく、長期保守が難しい
- すべての call site を安全に発見して書き換えることは難しい

Visual Studio 2022 + MSTest との相性:

- 通常の test runner から実行できる可能性はあるが、JIT timing と test discovery / execution ordering に影響されやすい
- デバッグ中に書き換え後の IL と source の対応がずれ、ステップ実行が分かりにくくなる

CI での扱いやすさ:

- 特別な agent 権限が不要な設計なら CI で動かしやすい
- runtime version 差、ReadyToRun 有無、parallel test 設定で結果が変わるリスクがある

並列テスト時のリスク:

- 書き換えが process-wide になると、別 test の呼び出しも差し替わる
- test scope ごとの isolation を実現するには、thread-local / async-local dispatcher や process isolation が必要になる

デバッグ容易性:

- 低い。失敗時に元 IL、書き換え後 IL、dispatcher のどこで壊れたかを追う必要がある

実装難易度:

- 高い。IL 解析、metadata token、generic method、exception handler、async state machine への理解が必要

保守性:

- 低い。runtime 更新、target framework 更新、JIT optimization の影響を受けやすい

セキュリティ / 実行環境制約:

- 環境によって dynamic code generation や metadata 書き換えが制限される可能性がある
- signed assembly や hardened CI 環境では制約が増える

### 2.2 CLR Profiling API

概要:

- CLR Profiling API を使い、module load、JIT compilation、ReJIT などのタイミングで IL や method body の差し替えを行う方式
- Microsoft Fakes Shim 相当の強力な interception に近い方向性

できること:

- source code を変更せずに method call の差し替えを狙える
- JIT 前の IL 書き換えや ReJIT を使えば、runtime rewrite より制御点が明確になる
- static / constructor / non-virtual call の差し替えを検証しやすい

できないこと:

- profiler attach は process 起動時の environment variable や native component が必要になることが多い
- managed-only library として完結しにくい
- Visual Studio Test Platform、coverage collector、diagnostics profiler と競合する可能性がある
- multi-target / x86 / x64 / ARM64 などへの対応が重い

Visual Studio 2022 + MSTest との相性:

- MSTest runner の起動環境に profiler を注入する必要があり、通常の unit test 体験から外れやすい
- Visual Studio のコードカバレッジや診断ツールとの同時利用に注意が必要

CI での扱いやすさ:

- Windows runner では検証しやすいが、native profiler の配置、環境変数、bitness、権限を管理する必要がある
- GitHub Actions などで再現性を保つには専用 test job に分離するのが望ましい

並列テスト時のリスク:

- profiler は process-wide に作用するため、parallel test との相性は悪い
- shim scope を test method ごとに切っても、同時実行中の別 test が同じ method を呼べば影響を受ける

デバッグ容易性:

- 低い。native profiler、CLR event、managed dispatcher の境界で問題が起きる

実装難易度:

- 非常に高い。native component、CLR hosting、ReJIT、metadata rewrite の知識が必要

保守性:

- 低い。runtime version、OS、architecture、test runner の更新に追随する必要がある

セキュリティ / 実行環境制約:

- native binary を読み込むため、企業 CI や sandboxed 環境でブロックされる可能性がある
- profiler injection は明示的な opt-in と強い警告が必要

### 2.3 source rewriting

概要:

- compile 前に source code を解析し、`new UserRepository()` を factory call に置き換えるなど、testable な source に変換する方式
- Roslyn workspace や MSBuild task で変換する可能性がある

できること:

- direct `new` や static call を、明示的な adapter / factory 経由に変換できる
- 変換結果を source として確認しやすい
- runtime rewrite よりデバッグしやすい

できないこと:

- source を持たない third-party assembly や BCL の内部 call は対象外
- source を直接変更する方式は利用者の作業ツリーに影響する
- 変換ルールが広すぎると、意図しない semantic change を起こす

Visual Studio 2022 + MSTest との相性:

- 生成後の source を通常の MSTest で実行できるため相性は良い
- IDE 上での差分確認、code fix、preview と組み合わせやすい

CI での扱いやすさ:

- deterministic な変換にできれば扱いやすい
- CI では変換済み source の差分検出や snapshot test が必要

並列テスト時のリスク:

- runtime global state を使わないため低い
- ただし、source rewrite の出力先を共有すると concurrent build で競合する可能性がある

デバッグ容易性:

- 高い。最終的な source を追える

実装難易度:

- 中から高。C# syntax と semantic model を正しく扱う必要がある

保守性:

- 中。C# language version 更新に追随する必要はあるが、runtime patching より安定しやすい

セキュリティ / 実行環境制約:

- 低リスク。native component や profiler injection を必要としない

### 2.4 build-time weaving

概要:

- compile 後、test 実行前に assembly IL を書き換え、method call や constructor call を shim dispatcher へ向ける方式
- source rewriting と runtime rewrite の中間に位置する

できること:

- source code を変更せずに user assembly の call site を差し替えられる可能性がある
- runtime 中の JIT timing に左右されにくい
- CI で weave 済み assembly を検査しやすい

できないこと:

- signed assembly、strong-name、PDB、SourceLink、coverage への影響を慎重に扱う必要がある
- third-party / BCL assembly を書き換えるのは現実的でない
- generic、async、iterator、expression tree、reflection call は個別検証が必要

Visual Studio 2022 + MSTest との相性:

- weave 済み test output を MSTest が実行する形なら相性は比較的良い
- ただし、IDE の build pipeline に custom step を入れる必要がある

CI での扱いやすさ:

- dedicated MSBuild target として deterministic に実行できれば扱いやすい
- restore/build/test の間に weaving step が入るため、失敗時の診断を整える必要がある

並列テスト時のリスク:

- weave 済み assembly ごとに出力先を分ければ低い
- 差し替え dispatcher が process-wide state を持つ場合は parallel test の干渉が残る

デバッグ容易性:

- 中。PDB 更新を正しく行えば追跡しやすいが、source と IL の対応がずれる場合がある

実装難易度:

- 高い。IL rewrite、PDB、MSBuild integration が必要

保守性:

- 中から低。runtime patching より安定しやすいが、build pipeline の複雑さが増える

セキュリティ / 実行環境制約:

- native component は不要にできる
- assembly 書き換えを許可しない環境や signed artifact では制約がある

### 2.5 detour / method patching

概要:

- JIT 後の method entry point や native code の jump target を差し替え、呼び出しを shim method へ誘導する方式
- native detour、method table patch、function prolog patch などが該当する

できること:

- static / sealed / non-virtual method も、method body entry を差し替える方向で扱える可能性がある
- source や assembly output を変更せずに動作できる可能性がある

できないこと:

- runtime 実装 detail に強く依存する
- inlining、tiered compilation、ReadyToRun、AOT、generic sharing の影響を受ける
- 安全な restore と scope 管理が難しい

Visual Studio 2022 + MSTest との相性:

- test runner 上で動く可能性はあるが、debugger、coverage、JIT optimization と干渉しやすい
- 失敗時に access violation や process crash になりやすく、通常の unit test として扱いにくい

CI での扱いやすさ:

- 低い。OS、architecture、runtime version ごとの安定性確認が必要
- sandboxed runner や hardened environment では制限されやすい

並列テスト時のリスク:

- 非常に高い。patch は process-wide になりやすく、同じ method を別 test が呼ぶと干渉する
- lock で直列化しても、async continuation や background thread が patch scope 外で動く可能性がある

デバッグ容易性:

- 非常に低い。process crash、native stack、JIT code の調査が必要になる

実装難易度:

- 非常に高い。unsafe / native interop / runtime internals への依存が大きい

保守性:

- 低い。runtime update で破損しやすい

セキュリティ / 実行環境制約:

- 高リスク。unsafe code、memory protection 変更、native call が必要になる可能性がある
- MiniMockito 本体には入れない

### 2.6 Roslyn analyzer / source generator による seam 提案

概要:

- analyzer で `new`, static call, `DateTime.Now`, concrete dependency creation などの hard-to-mock pattern を検出する
- code fix で factory / adapter / injectable clock / interface extraction などの移行案を提示する
- source generator は wrapper や adapter scaffold を生成する用途に限定し、既存 source を暗黙に書き換えない

できること:

- direct `new` や static call を含むコードに、より testable な設計への移行案を出せる
- Visual Studio 2022 の light bulb / code fix と相性が良い
- runtime interception を使わず、MiniMockito 本体の安全性を保てる
- CI で analyzer warning として運用できる

できないこと:

- 実行時に既存 call を透過的に差し替えることはできない
- generator は既存 method body を直接変更できない
- すぐに legacy code 全体を shim できるわけではない

Visual Studio 2022 + MSTest との相性:

- 非常に良い。IDE 上で修正候補を出し、通常の MSTest で検証できる
- テストコード作成時の migration helper として自然に使える

CI での扱いやすさ:

- 非常に良い。analyzer package として導入し、warning / error level を制御できる
- native component や special runner が不要

並列テスト時のリスク:

- 低い。runtime global state を変更しない

デバッグ容易性:

- 高い。通常の source / generated source として追える

実装難易度:

- 中。Roslyn semantic model と code fix の実装は必要だが、runtime patching より安全

保守性:

- 高い。C# language update への追随は必要だが、runtime internals への依存がない

セキュリティ / 実行環境制約:

- 低リスク。通常の managed analyzer として運用できる

### 2.7 adapter / factory migration helper

概要:

- shim ではなく設計移行支援として、static / direct `new` の利用箇所に adapter、factory、clock、provider を導入する補助機能
- analyzer / code fix と組み合わせる

できること:

- `new UserRepository()` を `IUserRepositoryFactory.Create()` に寄せる
- `DateTime.Now` を `IClock.Now` や `TimeProvider` に寄せる
- static utility を injectable service に寄せる
- MiniMockito 本体の interface mock / class proxy でテスト可能な形へ移行できる

できないこと:

- 既存 production code の call を実行時に透過差し替えすることはできない
- 大規模 legacy code では migration cost が残る

Visual Studio 2022 + MSTest との相性:

- 良い。IDE code fix と通常の MSTest の流れに乗る

CI での扱いやすさ:

- 良い。analyzer rule として enforce できる

並列テスト時のリスク:

- 低い。global patching を使わない

デバッグ容易性:

- 高い。変更後の dependency boundary が source に明示される

実装難易度:

- 低から中。汎用的すぎる自動変換は避け、提案と scaffold に絞るのが現実的

保守性:

- 高い。runtime internals への依存がない

セキュリティ / 実行環境制約:

- 低リスク

## 3. experimental package 案

候補構成:

```text
src/
  MiniMockito.Shims.Experimental/
tests/
  MiniMockito.Shims.Experimental.Tests/
```

候補 namespace:

```csharp
MiniMockito.Shims.Experimental
```

候補 API:

```csharp
using (ShimContext.Create())
{
    Shim.Static(() => DateTime.Now).Returns(fixedTime);
    Shim.New<UserRepository>().Returns(fakeRepo);
}
```

この API は確定しません。現時点では、利用者が明示的に experimental package を参照し、明示的に `ShimContext` scope を作る方向性だけを候補とします。

設計上の注意:

- `ShimContext` は process-wide patch と test-local scope の差を明確にする
- parallel test で安全でない方式は API 名や documentation で明示する
- production code の main package から experimental package へ依存しない
- experimental package は SemVer 上も breaking change があり得ることを明示する
- Visual Studio 2022 + MSTest の通常フローで使えるものを優先し、profiler / native detour は別 lane に分ける

## 4. 本体との境界

MiniMockito 本体に残すもの:

- interface mock / interface spy
- DispatchProxy based interface proxy
- class proxy based public virtual method mocking
- class spy / partial mock
- stubbing / verification / InOrder / argument matcher / captor
- `InvocationRecord`, `MockState`, `StubRule`, `VerificationEngine` などの high-level model
- proxy-based mocking の diagnostics

MiniMockito.ClassProxy に置くもの:

- class proxy validation
- Reflection.Emit based generated subclass
- public non-sealed class + public virtual method の interception
- CallBase / class spy の base implementation 呼び出し
- class proxy 固有 exception と supported / unsupported method diagnostics

MiniMockito.Shims.Experimental に分けるもの:

- direct `new` interception
- static method mocking
- sealed class mocking
- non-virtual method mocking
- constructor interception
- runtime IL rewrite
- profiler API based rewrite
- build-time weaving
- detour / method patching
- shim scope / shim dispatcher / low-level patch lifecycle
- analyzer / code fix / source generator based migration helpers

共有できる Core model:

- invocation 表現のうち、method / arguments / return / exception / thread / timestamp / sequence number などの概念
- matcher / captor の一部
- verification count model の一部
- error message formatting の方針

共有してはいけない低レベル実装:

- class proxy の generated subclass builder を shim 側に流用しない
- profiler / detour / IL rewrite の低レベル state を本体に持ち込まない
- shim dispatcher の process-wide mutable state を本体の `MockRepository` に混ぜない
- experimental package から本体の internal implementation に過度に依存しない

境界ルール:

- `MiniMockito` は `MiniMockito.Shims.Experimental` を参照しない
- `MiniMockito.Shims.Experimental` は必要に応じて `MiniMockito` の public API または小さな shared abstractions だけを使う
- experimental の failure は本体の interface mock / class proxy の安定性に影響させない
- shim 機能は opt-in、別 package、別 namespace、別 docs、別 test job にする

## 5. Visual Studio 2022 + MSTest との相性

相性が良い順:

1. analyzer / code fix / source generator
2. adapter / factory migration helper
3. source rewriting
4. build-time weaving
5. runtime IL rewrite
6. CLR Profiling API
7. detour / method patching

Visual Studio 2022 では、analyzer / code fix が最も自然です。利用者が IDE 上で hard-to-mock pattern を見つけ、factory や adapter を導入し、通常の MSTest で検証できます。

build-time weaving は MSBuild target として構成できれば許容できますが、PDB、coverage、debug step のずれが問題になります。

profiler や detour は test runner 起動方法、bitness、coverage collector、debugger との干渉が大きく、通常の unit test experience から外れやすいです。実験する場合も dedicated test project と dedicated CI job に限定します。

## 6. CI での扱いやすさ

扱いやすい方式:

- analyzer / code fix
- source generator
- adapter / factory migration helper
- source rewriting

注意が必要な方式:

- build-time weaving
  - deterministic output
  - PDB 更新
  - weave 済み assembly の検査
  - test output directory の分離

扱いにくい方式:

- runtime IL rewrite
- CLR Profiling API
- detour / method patching

CI 方針:

- MiniMockito 本体の CI は shim experimental に依存しない
- shim experimental は dedicated matrix を持つ
- unsafe / profiler / detour の job は default CI から分け、失敗時に本体 release を止めない設計を検討する
- parallel test を disabled にする必要がある方式は、その理由を README と test runsettings に明記する

## 7. 並列テスト時のリスク

direct `new` / static / sealed / non-virtual の差し替えは、対象が process-wide になりやすい点が最大リスクです。

主なリスク:

- 同じ static method を複数 test が別々に shim し、結果が非決定的になる
- patch scope を `using` で閉じても、async continuation や background thread が scope 外で対象 method を呼ぶ
- test framework の parallelization により、別 test class の production code 呼び出しが shim の影響を受ける
- method body / native code patch の restore 漏れで後続 test が汚染される
- test retry や fail-fast 時に cleanup が実行されない

最低限必要な対策:

- shim experimental の test は既定で parallel disabled を推奨する
- process-wide patch を使う方式は test collection / assembly level で直列化する
- `ShimContext` は dispose で確実に restore し、restore failure を明示的に報告する
- async-local scope を使う場合も、process-wide patch との差を documentation に明記する
- CI では shim experimental job を本体 job から分離する

## 8. 最初に実験すべき PoC

最初の PoC は、Roslyn analyzer / code fix による seam 提案を推奨します。

理由:

- MiniMockito 本体の安定性を壊さない
- Visual Studio 2022 と MSTest の通常フローに乗る
- CI で扱いやすい
- 並列テストのリスクが低い
- direct `new` / static call を interface mock / class proxy でテスト可能な設計へ移行できる

PoC scope:

- `new ConcreteDependency()` を検出する analyzer
- `DateTime.Now` / `DateTime.UtcNow` のような static dependency を検出する analyzer
- factory / adapter / clock abstraction の導入を提案する code fix
- generated adapter scaffold の候補を出す source generator
- 生成または提案された seam を使い、MiniMockito 本体の `Mock.Of<T>()` / `Mock.Class<T>()` でテストできることを sample で示す

次に検証する PoC:

- source rewriting による `new` replacement
  - user source の小さい sample に限定する
  - 変換差分を snapshot で検証する
  - production source を暗黙に変更しない dry-run mode を先に作る

三番目以降に回す PoC:

- build-time weaving による method call replacement
  - dedicated sample assembly だけを対象にする
  - PDB / coverage / CI の影響を検証する

最初に避ける PoC:

- CLR Profiling API
- detour / method patching
- BCL method の透過差し替え

これらは実装コストと実行環境制約が大きく、MiniMockito の軽量性と通常の MSTest 体験から離れやすいため、初期 PoC には向きません。

## 9. やらない判断

MiniMockito 本体に direct new interception を入れない理由:

- proxy instance を渡す設計と異なり、既存 call site の書き換えが必要になる
- process-wide な副作用を持ちやすい
- 本体の simple mock API と責務が異なる

MiniMockito 本体に static method mocking を入れない理由:

- static dispatch は instance proxy で扱えない
- global state を差し替える API になりやすく、parallel test と衝突しやすい
- runtime rewrite / profiler / detour のような高リスク実装が必要になる

MiniMockito 本体に sealed class mocking を入れない理由:

- class proxy は継承が前提であり、sealed class を override できない
- sealed class を扱うには method body や call site への介入が必要になる

MiniMockito 本体に non-virtual method mocking を入れない理由:

- non-virtual call は override で差し替えられない
- call site rewrite または method patching が必要で、proxy-based mocking ではない

MiniMockito 本体に runtime IL rewrite を入れない理由:

- lightweight mock framework の本体としては複雑さと保守リスクが大きい
- Visual Studio / MSTest / CI の通常運用に影響する
- v1 / v2 の deterministic tests を不安定にする可能性がある

MiniMockito 本体に profiler API を入れない理由:

- native component と process startup configuration が必要になりやすい
- coverage / debugger / CI との競合リスクが高い
- package の利用条件が重くなり、v1 / v2 本体の目的から外れる

## 10. 推奨ロードマップ

v2 本体でやること:

- interface mock / spy の安定化
- class proxy の public virtual method support 強化
- class spy / partial mock の boundary 明確化
- class proxy diagnostics の改善
- README / sample / tests の整備

v2 experimental でやること:

- analyzer / code fix による hard-to-mock pattern 検出
- adapter / factory migration helper
- source rewriting の dry-run PoC
- build-time weaving の isolated sample PoC
- shim scope と parallel test risk の documentation

v3 以降に回すこと:

- profiler feasibility check
- runtime IL rewrite の限定 PoC
- build-time weaving の対象拡大
- static / constructor / non-virtual call replacement の API 実験
- Visual Studio / MSTest / CI matrix を含む experimental validation

実装しないほうがよいこと:

- MiniMockito 本体への profiler / detour 導入
- BCL 全体の透過 shim
- process-wide patch を通常の unit test API として提供すること
- parallel test 安全性を保証できない shim を stable API として公開すること
- Microsoft Fakes Shim の完全代替を目標にすること

## 11. Phase 4 の結論

MiniMockito 本体は proxy-based mocking に集中します。

`MiniMockito.Shims.Experimental` は、必要になった場合だけ別 package として検証します。最初の実験は analyzer / source generator / adapter migration helper を優先し、runtime IL rewrite、CLR Profiling API、detour / method patching は後続の isolated PoC に回します。

この方針により、Visual Studio 2022 + MSTest で自然に使える軽量 mock framework という本体の目的を維持しながら、高リスクな shim 領域を段階的に調査できます。

## 12. Phase 5: integration and safety

### 目的

Phase 4 の `newobj` rewrite PoC をテストで扱いやすい形に整理し、安全性と診断を強化します。
新しい差し替え対象は追加しません。

### 変更概要

#### ShimContext safety

- `ShimContext.ActiveContextCount` 静的プロパティを追加。CreateからDisposeまでの未解放コンテキスト数を追跡し、テストのリーク検出に使用できます。
- `ShimContext.CleanupException` プロパティを追加。`Dispose()` 中のクリーンアップ失敗を格納して再スローします。
- `RequireCurrent()` のエラーメッセージを改善。「コンテキストが存在しない」と「コンテキストが Dispose 済み」を区別します。
- Nested context の挙動をコード・テスト・ドキュメントで明確化しました。
- async / threading に関する注意事項を `ShimContext` の XML doc に追記しました。

#### Parallel test safety

- `[assembly: DoNotParallelize]` は引き続き有効。
- 各テストクラスにも `[DoNotParallelize]` を追加。
- 並列実行が危険な理由 (process-wide state、async-local の境界、rewrite 出力ファイルの競合) を docs に追記。

#### Rewrite diagnostics

- `RewriteResult.RewrittenCallSiteDescriptions` プロパティ追加 (`Diagnostics` から `"Rewrote "` 始まりの行を抽出)。
- `RewriteResult.SkippedCallSiteDescriptions` プロパティ追加 (`Diagnostics` から `"Skipped "` 始まりの行を抽出)。
- `RewriteResult.ToSummary()` メソッド追加。人間が読みやすいテキスト形式の概要を返します。

#### NewInterceptionHarness テストヘルパー

`NewInterceptionHarness` クラスを追加。以下を統合した fluent API です。

- `WithTarget<T>()` — allowlist に追加
- `RewriteTargetTypeAssembly()` — 最初の target type の assembly を書き換え
- `RewriteAssembly(string)` — 明示的パスの assembly を書き換え
- `Create<TService>()` — 書き換え済み assembly からサービスのインスタンスを生成
- `CreateFake<TTarget>(params object[])` — 書き換え済み assembly から fake インスタンスを生成
- `RegisterShim<TTarget>(object)` — 書き換え済み型を使って ShimContext にルール登録
- `Invoke<TResult>(object, string, params object[])` — リフレクション経由のメソッド呼び出し
- `GetRewrittenType(Type)` — 書き換え済み load context の `Type` を取得
- `LastRewriteResult` — 最後の rewrite 結果

### 重要な注意事項

- `MiniMockito.Shims.Experimental` は **experimental** パッケージです。本体 MiniMockito の安定 API ではありません。
- BCL 型は対象外です。
- static method は対象外です。
- constructor arguments は対象外です。
- generic 型は対象外です。
- **parallel test は危険です。** `[DoNotParallelize]` を必ず付けてください。
- Visual Studio Test Explorer での完全統合は未対応です。

### この Phase で実装しなかったもの

- static method mocking
- BCL type 差し替え
- constructor arguments
- generic classes
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite

### 次に推奨する Phase

- `NewInterceptionHarness` の API を feedback を元に洗練する
- Dispose 漏れを自動検出する TestBase クラスの提供を検討する
- async state machine 内の `newobj` への対応可否を調査する
- `ShimContext` の process-wide / context-local 境界についての追加検証
