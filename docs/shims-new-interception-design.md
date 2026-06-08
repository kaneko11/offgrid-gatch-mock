# Shims New Interception Design

## 1. 目的

このドキュメントは、`MiniMockito.Shims.Experimental` で `new SomeClass()` を差し替えるための Phase 1 設計です。

この Phase では本格実装を行いません。MiniMockito 本体の public API、既存の interface mock / class proxy / spy / verification 実装には手を入れません。

最終的に実現したい利用イメージ:

```csharp
using MiniMockito;
using MiniMockito.Shims.Experimental;

using (ShimContext.Create())
{
    var fakeRepository = Mock.Class<UserRepository>();

    Shim.New<UserRepository>()
        .Returns(fakeRepository);

    var service = new UserService();

    var result = service.GetDisplayName(1);
}
```

対象コード例:

```csharp
public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();
        return repository.GetName(id);
    }
}
```

この `new UserRepository()` は IL 上では概ね `newobj UserRepository::.ctor()` になります。interface proxy や class proxy は、この call site を通らないため差し替えられません。direct `new` interception には、source、IL、JIT、profiler、または native patch のどこかで call site か method body に介入する必要があります。

## 2. 本体との境界

MiniMockito 本体に残すもの:

- interface mock / spy
- class proxy / class spy / partial mock
- stubbing / verification / InOrder / matcher / captor
- proxy-based diagnostics

`MiniMockito.Shims.Experimental` に分けるもの:

- direct `new` interception
- constructor call site rewrite
- shim context / rule registry / dispatcher
- assembly rewrite tooling
- dedicated sample / test runner integration
- parallel test の制約と診断

依存方向:

- `MiniMockito` は `MiniMockito.Shims.Experimental` を参照しない
- `MiniMockito.Shims.Experimental` は必要に応じて `MiniMockito` の public API を使って fake instance を受け取る
- 本体の `Mock.Of<T>()`, `Mock.Class<T>()`, `Spy.Class<T>()` の public API は変更しない

## 3. 実装方式比較

### 3.1 build-time weaving / test output assembly rewrite

概要:

- build 後、test 実行前に test output または dedicated sample assembly の IL をコピー先で書き換える
- `newobj Target::.ctor()` を `ShimDispatcher.New<T>()` 呼び出しへ置き換える
- original assembly は上書きしない

できること:

- user assembly 内の direct `new` を source 変更なしで差し替える PoC が作れる
- JIT timing に左右されにくい
- rewrite report をファイルとして残せる
- CI で rewritten output を検査しやすい

できないこと:

- BCL / runtime assembly の差し替えは対象外
- signed assembly、PDB、SourceLink、coverage への影響は別途検証が必要
- constructor arguments、generic type、complex control flow は初期対象外

Visual Studio 2022 + MSTest:

- dedicated sample test project として分ければ、通常の solution build を壊しにくい
- Test Explorer にそのまま統合するには、rewrite step と rewritten assembly 実行方法の設計が必要
- Phase 2 では Visual Studio Test Explorer の完全統合より、CLI での deterministic PoC を優先する

CI:

- dedicated job に分離しやすい
- rewritten output directory を固定し、rewrite report を artifact 化できる

並列テスト:

- shim registry が process-wide になる場合は危険
- Phase 2 PoC では dedicated test assembly で parallel disabled を前提にする

評価:

- Phase 2 の最有力方式
- runtime rewrite / profiler / detour より軽く、失敗時の診断を作りやすい

### 3.2 source rewriting

概要:

- compile 前に source を解析し、`new UserRepository()` を `ShimDispatcher.New<UserRepository>()` または factory call に置き換える

できること:

- 変換後の source を確認しやすい
- Visual Studio の preview / code fix と相性が良い
- debug step が比較的追いやすい

できないこと:

- source を持たない assembly は対象外
- user source を直接変更する方式は危険
- C# semantic model に依存し、partial / generated source / conditional compilation の扱いが難しい

評価:

- dry-run や analyzer / code fix には向く
- Phase 2 の direct interception PoC としては、source 変更が主目的に見えやすいため第二候補

### 3.3 runtime IL rewrite

概要:

- test setup 時または実行中に loaded assembly / method body を書き換える

できること:

- source や build output を変更せずに差し替える方向を検証できる
- call site 単位の動的 scope を表現できる可能性がある

できないこと:

- すでに JIT 済みの method、ReadyToRun、AOT、tiered compilation の影響が大きい
- runtime version 依存が強い
- failure mode が分かりにくい

評価:

- Phase 2 では採用しない
- build-time rewrite PoC 後に feasibility check として検討する

### 3.4 CLR Profiling API

概要:

- profiler を process 起動時に読み込み、module load / JIT / ReJIT のタイミングで IL を差し替える

できること:

- Microsoft Fakes Shim に近い方向の強力な差し替えを検証できる
- JIT 前後の制御点が runtime IL rewrite より明確になる場合がある

できないこと:

- native component、environment variable、bitness、OS 対応が必要
- Visual Studio Test Platform、coverage、debugger と競合しやすい
- managed-only の軽量 package から離れる

評価:

- Phase 2 では採用しない
- dedicated native feasibility project が必要になるため、かなり後の Phase に回す

### 3.5 detour / method patching

概要:

- JIT 後の method entry point や native code を patch し、別 method へ jump させる

できること:

- source / assembly rewrite なしで method body 差し替えを狙える
- static / sealed / non-virtual method も理論上対象にできる可能性がある

できないこと:

- inlining、tiered compilation、ReadyToRun、generic sharing に弱い
- process crash になりやすい
- restore と scope 管理が難しい
- security / sandbox 制約が大きい

評価:

- Phase 2 では採用しない
- MiniMockito の軽量な MSTest 体験とは相性が悪い

## 4. Phase 2 最小 PoC スコープ

対応するもの:

- user-defined class
- public class
- non-generic class
- public parameterless constructor
- user assembly 内の単純な `newobj Target::.ctor()`
- allowlist で明示された target type
- dedicated sample assembly
- rewritten assembly は別出力先へ生成
- original assembly は上書きしない
- MSTest での限定検証
- parallel test disabled の専用 test run

対応しないもの:

- BCL / .NET runtime type の差し替え
- `DateTime.Now`
- `File.ReadAllText`
- static method mocking
- sealed class method interception
- non-virtual method body interception
- private method interception
- constructor arguments
- generic constructors
- generic classes
- nested new の網羅
- reflection 経由の construction
- dependency injection container 内部の construction
- expression tree 内の `new`
- async state machine / iterator 内の複雑な `new`
- ReadyToRun / AOT / NativeAOT
- production assembly の in-place rewrite
- process-wide stable guarantee
- parallel test safety guarantee

## 5. API 案

最小候補:

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(fakeRepository);
}
```

対象 assembly を明示する候補:

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .ForAssembly(typeof(UserService).Assembly)
        .Returns(fakeRepository);
}
```

対象 method を明示する候補:

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .ForMethod(typeof(UserService).GetMethod(nameof(UserService.GetDisplayName))!)
        .Returns(fakeRepository);
}
```

factory 戻り値の候補:

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(() => Mock.Class<UserRepository>());
}
```

Phase 2 では API を確定しすぎません。推奨は以下です。

- `ShimContext.Create()` は必須にする
- `Shim.New<T>()` は rule builder を返す
- `.Returns(T instance)` を最初に対応する
- `.ForAssembly(...)` は rewrite allowlist と対応付ける候補として設計に残す
- `.Returns(Func<T>)` と `.ForMethod(...)` は Phase 2 では設計候補に留める

## 6. 内部構成案

```text
src/
  MiniMockito.Shims.Experimental/
    ShimContext.cs
    Shim.cs
    NewShimBuilder.cs
    NewShimRule.cs
    ShimRuleRegistry.cs
    ShimDispatcher.cs
    Rewrite/
      AssemblyRewriter.cs
      NewObjRewriter.cs
      RewritePlan.cs
      RewriteReport.cs
    Exceptions/
      ShimUnsupportedException.cs
      ShimRewriteException.cs
tests/
  MiniMockito.Shims.Experimental.Tests/
samples/
  MiniMockito.Shims.Experimental.Sample/
```

責務:

- `ShimContext`
  - test scope を表す disposable context
  - context-local rule registry を作る
  - dispose 時に rule を cleanup する
  - cleanup failure を握りつぶさない

- `Shim`
  - public entry point
  - `Shim.New<T>()` を提供する
  - context がない場合は診断付き例外を投げる

- `NewShimBuilder<T>`
  - `Returns(T instance)` で rule を登録する
  - 将来 `ForAssembly`, `ForMethod`, `Returns(Func<T>)` を受ける候補
  - target type validation を行う

- `NewShimRule`
  - target type
  - optional assembly / method scope
  - return instance または factory
  - registration order

- `ShimRuleRegistry`
  - active `ShimContext` ごとの rule を保持する
  - 初期 PoC では parallel safety を保証しない
  - 将来は AsyncLocal と process-wide fallback の整理が必要

- `ShimDispatcher`
  - rewritten call site から呼ばれる
  - `New<T>()` で matching rule を探し、fake instance を返す
  - rule がない場合の fallback 方針を明確にする

- `AssemblyRewriter`
  - input assembly を読み、rewrite plan に従って output directory へ書き出す
  - original assembly は上書きしない
  - PDB copy / rewrite report 生成を担当する

- `NewObjRewriter`
  - method body 内の `newobj Target::.ctor()` を検出する
  - allowlist に一致する call site だけ変換する
  - unsupported pattern を report する

- `RewritePlan`
  - target assembly
  - output path
  - allowlisted target types
  - rewrite mode
  - optional include / exclude method list

- `RewriteReport`
  - rewritten call sites
  - skipped call sites
  - unsupported patterns
  - diagnostics

- `ShimUnsupportedException`
  - unsupported target type / unsupported pattern 用

- `ShimRewriteException`
  - assembly rewrite failure 用

## 7. call site rewrite 方針

概念的な変換:

```csharp
var repository = new UserRepository();
```

IL 上の対象:

```text
newobj instance void UserRepository::.ctor()
```

変換後の概念:

```csharp
var repository = ShimDispatcher.New<UserRepository>();
```

IL 方針:

- `newobj Target::.ctor()` を `call !!0 ShimDispatcher::New<Target>()` に置き換える
- 対象 constructor が parameterless であることを検証する
- stack behavior が同じになるように、constructor arguments がある `newobj` は対象外にする
- value type construction は対象外にする
- generic target type は対象外にする
- rewritten assembly は `MiniMockito.Shims.Experimental` assembly への参照を持つ

fallback 方針:

- Phase 2 PoC では、rewritten call site が呼ばれたのに active rule がない場合は `ShimUnsupportedException` を投げる案を優先する
- original constructor を呼ぶ fallback は便利だが、`ShimDispatcher.New<T>()` から安全に元の `newobj` へ戻すには再帰回避や reflection construction が絡む
- 初期 PoC では「rewrite された call site は明示 rule 必須」とするほうが診断が明確

## 8. test runner 方針

出力先:

- `artifacts/shims/<configuration>/<target-framework>/rewritten/` のような別 directory を候補にする
- original `bin` output は上書きしない
- `RewriteReport.json` または Markdown report を同じ directory に出す

実行方法:

- Phase 2 は専用 sample assembly を rewrite し、その rewritten assembly を `dotnet test` または VSTest console 相当で実行する PoC にする
- 通常の `dotnet test MiniMockito.sln` に最初から混ぜない
- CI では dedicated job に分ける

Visual Studio Test Explorer:

- Phase 2 では完全統合しない
- Test Explorer からは通常 test を実行し、shim rewrite PoC は CLI script / MSBuild target で実行する方針にする
- 将来、MSBuild target と `.runsettings` で rewritten output を指定できるか検証する

dedicated test project:

- `tests/MiniMockito.Shims.Experimental.Tests` を候補にする
- shims test assembly は parallel disabled を既定にする
- v1 / v2 本体 test project とは分ける

parallel test:

- Phase 2 PoC では parallel safety を保証しない
- assembly-level MSTest parallelization disabled を検討する
- documentation に明記する

## 9. 失敗診断

失敗時の message には可能な限り以下を含める。

```text
Target type:
Constructor:
Calling assembly:
Calling method:
Rewrite mode:
Reason:
Supported patterns:
Unsupported patterns:
Hint:
```

例:

```text
New interception target is not supported.
Target type: Sample.UserRepository
Constructor: .ctor(System.String)
Calling assembly: Sample.Tests
Calling method: Sample.UserService.GetDisplayName
Rewrite mode: TestOutputAssemblyRewrite
Reason: ConstructorArgumentsNotSupported
Supported patterns:
  public non-generic class
  public parameterless constructor
  direct newobj in user assembly
Unsupported patterns:
  constructor arguments
  BCL types
  generic types
Hint: Use a public parameterless constructor or exclude this call site from the rewrite plan.
```

## 10. テスト戦略

Phase 2 PoC tests:

- rewrite plan が allowlisted target type を保持できる
- rewriter が sample assembly 内の単純な `newobj UserRepository::.ctor()` を検出できる
- rewriter が rewritten output を別 directory に出す
- rewritten call site が `ShimDispatcher.New<UserRepository>()` を呼ぶ
- active rule がある場合、fake instance が返る
- active rule がない場合、診断付き例外になる
- constructor arguments は unsupported report になる
- BCL type は unsupported report になる
- generic type は unsupported report になる
- original assembly が上書きされない
- rewrite report に rewritten / skipped / unsupported が出る
- v1 / v2 の existing tests は通常通り通る

専用 sample:

- `UserService` が `new UserRepository()` を直接呼ぶ
- `UserRepository` は public non-generic class + public parameterless constructor
- fake は `Mock.Class<UserRepository>()` で作る
- `Shim.New<UserRepository>().Returns(fake)` で差し替える

CI:

- main CI は v1 / v2 の通常 test を継続
- shims experimental は dedicated CI job にする
- Phase 2 では shims job を optional 扱いにするか、最小 PoC のみに限定する

## 11. Phase 3 dry-run scanner 設計

Phase 3 では assembly の実際の書き換えは行わず、対象 assembly を読み取って `newobj` call site の候補だけを report します。

追加するモデル:

- `RewritePlan`
  - scan 対象 assembly path
  - allowlist された `RewriteTarget`
- `RewriteTarget`
  - allowlist の target type
  - Phase 3 では `Type` ベースで指定する
- `RewriteReport`
  - scan 対象 assembly
  - allowlist target
  - `NewObjScanResult`
- `AssemblyRewriteScanner`
  - dry-run scan の入口
  - IL の読み取りだけを行う
- `NewObjCallSite`
  - target type
  - target constructor
  - calling type
  - calling method
  - IL offset
  - supported / unsupported
  - unsupported reason
- `NewObjScanOptions`
  - allowlist target types
- `NewObjScanResult`
  - detected call sites
  - supported call sites
  - unsupported call sites

Phase 3 の scan 方針:

- `MethodBody.GetILAsByteArray()` で method body の IL byte sequence を取得する
- `System.Reflection.Emit.OpCodes` の metadata から opcode table を作る
- `OpCodes.Newobj` の operand metadata token を `Module.ResolveMethod(...)` で `ConstructorInfo` に解決する
- allowlist に含まれる target type の call site だけ report する
- support 判定は report 上の分類に留め、IL は変更しない

Phase 3 の supported pattern:

- user assembly 内の target type
- public class
- non-generic class
- public parameterless constructor
- allowlist で指定された target type
- simple `newobj`

Phase 3 の unsupported pattern:

- BCL / .NET runtime type
- non-public target type
- generic type
- constructor arguments
- non-public constructor
- value type / interface / abstract type

Mono.Cecil などの IL inspection library は Phase 3 では追加しません。

理由:

- dry-run scanner は `newobj` 検出だけが目的で、`System.Reflection` と `MethodBody.GetILAsByteArray()` で足りる
- 依存追加なしで build / CI / NuGet metadata への影響を小さくできる
- Phase 4 以降で実際の assembly rewrite、PDB handling、metadata 書き換えが必要になった時点で Mono.Cecil 等を再検討するほうが妥当

代替案:

- Mono.Cecil
  - IL と metadata の読み書きに強い
  - Phase 4 以降の rewrite 実装では有力
  - 依存 package と license / version 管理が必要
- `System.Reflection.Metadata`
  - 低レベルで読み取りに強い
  - 書き換えには追加実装が重くなる
- `System.Reflection`
  - Phase 3 の読み取り dry-run には十分
  - assembly load context に assembly を読み込むため、完全な offline inspection ではない

Phase 3 の制約:

- assembly は実際には書き換えない
- rewritten assembly は出力しない
- call site の置換可能性は report の supported flag で示すだけ
- scanner は assembly を load するため、untrusted assembly の scan には使わない
- BCL type、constructor arguments、generic type は unsupported として report する

## 12. リスク

主なリスク:

- assembly rewrite が PDB / coverage / Test Explorer と干渉する
- rewritten output と original output の取り違えが起きる
- process-wide registry により parallel test で干渉する
- fake instance の lifetime が `ShimContext` を越えて漏れる
- constructor side effect が実行されなくなるため、production code の前提が変わる
- unsupported pattern を誤って rewrite すると runtime failure になる
- external dependency を入れる場合、package policy と license 確認が必要

軽減策:

- original assembly は絶対に上書きしない
- allowlist 必須にする
- rewrite report を必須にする
- dedicated sample / test project に限定する
- parallel disabled を明記する
- unsupported pattern は fail-fast する
- Phase 2 / Phase 3 では BCL / constructor args / generic / async state machine complex case に手を出さない

## 13. 採用しない方式と理由

Phase 2 で採用しない:

- source rewriting
  - 変換結果を見やすいが、direct interception の PoC より migration helper に近くなる
  - user source 変更の扱いが重い

- runtime IL rewrite
  - JIT timing と runtime version 依存が強い
  - 初期 PoC の失敗診断が難しい

- CLR Profiling API
  - native component と process startup configuration が必要
  - Visual Studio / coverage / CI との競合が大きい

- detour / method patching
  - process crash risk が高い
  - runtime internals 依存が強すぎる
  - MiniMockito の lightweight 方針に合わない

Phase 2 で採用する:

- build-time weaving / test output assembly rewrite
  - original output を残せる
  - dedicated sample assembly に限定できる
  - rewrite report を作りやすい
  - MSTest と CI で段階的に検証しやすい

## 14. Phase 2 用の実装プロンプト案

```markdown
AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md を読んでください。
MiniMockito.Shims.Experimental Phase 2 の範囲だけを実装してください。

目的:
- user assembly 内の単純な `newobj Target::.ctor()` を test output assembly rewrite で `ShimDispatcher.New<Target>()` に差し替える最小 PoC を実装する。
- MiniMockito 本体の public API は変更しない。
- 既存 v1 / v2 テストを壊さない。

実装範囲:
- `src/MiniMockito.Shims.Experimental/`
- `tests/MiniMockito.Shims.Experimental.Tests/`
- dedicated sample assembly
- `ShimContext`
- `Shim`
- `NewShimBuilder<T>`
- `NewShimRule`
- `ShimRuleRegistry`
- `ShimDispatcher`
- `RewritePlan`
- `RewriteReport`
- `AssemblyRewriter`
- `NewObjRewriter`
- `ShimUnsupportedException`
- `ShimRewriteException`

PoC 対象:
- user-defined public non-generic class
- public parameterless constructor
- direct `new Target()` の単純な `newobj`
- allowlist で明示された target type
- rewritten assembly は別 output directory に生成
- original assembly は上書きしない
- MSTest の dedicated test project で検証
- parallel test safety は保証しないため dedicated shims test は parallel disabled にする

非対象:
- BCL / .NET runtime type
- `DateTime.Now`
- `File.ReadAllText`
- static method mocking
- sealed class method interception
- non-virtual method body interception
- private method interception
- constructor arguments
- generic constructors / generic classes
- reflection construction
- expression tree 内の new
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly の in-place rewrite

検証:
- 既存 `dotnet build`
- 既存 `dotnet test`
- shims experimental の dedicated test

最後の報告は日本語でお願いします。
```
