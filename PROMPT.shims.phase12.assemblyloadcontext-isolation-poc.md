# PROMPT.shims.phase12.assemblyloadcontext-isolation-poc.md

# MiniMockito.Shims.Experimental Phase 12: AssemblyLoadContext isolation PoC

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md、docs/shims-assemblyloadcontext-isolation-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 12 として、AssemblyLoadContext isolation の限定 PoC を実装してください。

この Phase の目的は、rewritten assembly を default AssemblyLoadContext に直接ロードするのではなく、collectible AssemblyLoadContext に分離してロード・実行・unload できる最小 PoC を作ることです。

新しい interception 対象は追加しないでください。  
static method mocking、BCL type 差し替え、runtime IL rewrite、CLR Profiling API、detour / method patching は実装しないでください。

## 対象

最初は以下に限定してください。

- dedicated sample assembly
- rewritten assembly
- user-defined public class
- parameterless constructor
- constructor arguments ありの new
- existing Phase 7〜9 の shim rule
- reflection-based instance creation
- reflection-based method invocation
- collectible AssemblyLoadContext
- unload check
- original assembly は上書きしない

## 実装対象

### 1. ShimAssemblyLoadContext

以下、または同等の class を実装してください。

```csharp
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    public ShimAssemblyLoadContext(
        string mainAssemblyPath,
        IEnumerable<string> probingPaths)
        : base(name: "MiniMockito.Shims.Experimental", isCollectible: true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Resolve dependency from rewritten output directory / original test output directory.
    }
}
```

要件:

- `isCollectible: true`
- `AssemblyDependencyResolver` の利用を検討
- rewritten output directory を優先
- original test output directory を fallback
- dependency resolution diagnostics を保持
- unresolved dependency の diagnostics を出す

### 2. harness integration

既存の `NewInterceptionHarness` または同等の test helper に isolated loading option を追加してください。

候補 API:

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteSampleAssembly()
    .LoadInIsolatedContext();

var service = harness.Create("Sample.UserService");
var result = harness.Invoke<string>(service, "GetDisplayName", 1);
```

または:

```csharp
using var harness = NewInterceptionHarness.Create(options =>
{
    options.IsolateAssemblyLoadContext = true;
});
```

型 identity 問題を避けるため、最初は reflection-based API で構いません。

### 3. reflection helper

以下を実装してください。

- type lookup by full name
- instance creation
- method invocation
- generic return cast helper
- invocation error unwrap
- diagnostics

候補:

```csharp
object Create(string typeFullName, params object?[] args);

object? Invoke(object instance, string methodName, params object?[] args);

T? Invoke<T>(object instance, string methodName, params object?[] args);
```

### 4. unload support

以下を実装してください。

- harness Dispose で ALC unload
- WeakReference による unload check
- optional `AssertUnloaded()` helper
- unload failure diagnostics
- loaded assembly / resolved dependency diagnostics

候補:

```csharp
WeakReference unloadReference = harness.GetUnloadReference();

harness.Dispose();

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

Assert.IsFalse(unloadReference.IsAlive);
```

Unload はタイミング依存があるため、flaky にならないよう注意してください。  
どうしても安定しない場合は、diagnostic-only test に縮小してください。

### 5. ShimDispatcher / ShimRuleRegistry との統合

rewritten assembly から呼ばれる `ShimDispatcher` が既存 rule を見つけられるようにしてください。

注意点:

- isolated ALC 内に `MiniMockito.Shims.Experimental` が別ロードされると registry が分離してしまう可能性があります
- その場合、default ALC 側の dispatcher assembly を共有する設計を検討してください
- 共有が難しい場合は、この Phase では reflection-only / same ALC strategy に縮小して構いません
- 制約は docs に明記してください

### 6. docs 更新

以下を更新してください。

- `docs/shims-assemblyloadcontext-isolation-design.md`
- `docs/shims-new-interception-design.md`
- `docs/shims-constructor-args-design.md`

必ず以下を明記してください。

- ALC isolation は experimental
- 型 identity の制約
- strongly typed API の制約
- reflection-based harness API
- unload check の制約
- dependency resolution の制約
- Visual Studio Test Explorer 完全統合は未対応
- parallel test safety は保証しない

## MSTest

以下のテストを追加してください。

### ALC loading tests

- rewritten assembly を collectible ALC にロードできる
- type full name で type lookup できる
- reflection で service instance を作成できる
- reflection で method invocation できる
- dependency resolution diagnostics が取れる
- loaded assembly list が取れる

### shim integration tests

- isolated ALC で parameterless constructor shim が動く
- isolated ALC で constructor arguments shim が動く
- isolated ALC で WithArguments(Eq("prod")) が動く
- isolated ALC で ShimCaptor が constructor argument を capture できる
- no match fallback が動く
- original assembly は変更されない

### unload tests

- harness Dispose で unload が試行される
- WeakReference による unload check が可能
- unload failure diagnostics が分かりやすい
- flaky になる場合は、unload attempted / diagnostics の確認に縮小する

### regression tests

- non-isolated harness の既存テストが壊れていない
- Phase 7 / 8 / 9 / 10 tests が壊れていない
- existing v1 / v2 tests が壊れていない

## この Phase では対応しないこと

以下は実装しないでください。

- static method mocking
- BCL type 差し替え
- generic class shim
- generic constructor shim
- ref / out constructor arguments
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- Visual Studio Test Explorer 完全統合
- Microsoft Fakes Shim 完全互換

## 重要な注意

型 identity 問題で strongly typed API が難しい場合は、reflection-based API に縮小してください。

壊れた中途半端な implementation を残さないでください。  
ALC isolation が安定しない場合は、設計ドキュメントに制約を明記し、テストを安定する範囲に限定してください。

## 検証

最後に必ず以下を実行してください。

```bash
dotnet build
dotnet test
```

失敗した場合は修正してください。

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 実装した ALC isolation PoC
- harness API
- 型 identity の制約
- unload check の結果
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
