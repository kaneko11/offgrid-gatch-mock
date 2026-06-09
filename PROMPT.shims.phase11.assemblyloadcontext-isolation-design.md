# PROMPT.shims.phase11.assemblyloadcontext-isolation-design.md

# MiniMockito.Shims.Experimental Phase 11: AssemblyLoadContext isolation design

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 11 として、AssemblyLoadContext isolation の設計調査を行ってください。

この Phase では本格実装はしないでください。

目的は、Phase 4〜10 で実装した rewritten assembly 実行方式に対して、assembly 汚染、型衝突、ファイルロック、unload 不可、test 間干渉を減らすための AssemblyLoadContext 分離方針を設計することです。

## 背景

現状の shims experimental は、test output assembly を rewrite し、rewritten assembly をロードして実行する方式です。

この方式では以下のリスクがあります。

- rewritten assembly と original assembly の型衝突
- default AssemblyLoadContext 汚染
- test 間で rewritten assembly が残る
- ファイルロック
- unload できない assembly
- dependency resolution の不安定さ
- MSTest / Visual Studio Test Explorer との相性
- parallel test での干渉
- coverage / debugger / PDB との干渉

Phase 11 では、これらを整理し、Phase 12 の PoC 実装に向けた設計を作成してください。

## 設計対象

以下を設計してください。

### 1. 現状の assembly loading 問題の整理

以下を調査・整理してください。

- 現在 rewritten assembly をどのようにロードしているか
- default AssemblyLoadContext にロードされているか
- unload できるか
- original assembly と rewritten assembly の型 identity がどうなるか
- dependency assembly はどこから解決しているか
- file lock が残る可能性
- test 間で state が残る可能性

### 2. collectible AssemblyLoadContext 案

以下のような設計を検討してください。

```csharp
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    public ShimAssemblyLoadContext(string mainAssemblyPath, IEnumerable<string> probingPaths)
        : base(isCollectible: true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Resolve dependencies from rewritten output directory or original test output directory.
    }
}
```

検討事項:

- `isCollectible: true`
- dependency resolution
- original test output directory
- rewritten output directory
- `AssemblyDependencyResolver`
- shadow copy
- PDB loading
- unload verification
- weak reference unload check

### 3. Harness API 案

Phase 12 で実装する可能性のある API を設計してください。

候補:

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteSampleAssembly()
    .LoadInIsolatedContext();

var service = harness.Create<UserService>();
```

または:

```csharp
var harness = NewInterceptionHarness.Create(options =>
{
    options.IsolateAssemblyLoadContext = true;
});
```

設計すべき項目:

- harness lifetime
- Dispose で unload するか
- unload failure diagnostics
- created instances の扱い
- Type lookup
- Activator での instance 作成
- generic helper の制約
- test helper と production API の境界

### 4. ShimContext との関係

以下を整理してください。

- ShimContext は ALC ごとに分離するべきか
- ShimRuleRegistry は process-wide のままでよいか
- rewritten assembly から呼ばれる `ShimDispatcher` はどの ALC の型か
- `MiniMockito.Shims.Experimental` assembly は default ALC と isolated ALC のどちらにロードされるべきか
- 型 identity が違う場合、`UserRepository` fake instance を rewritten assembly 側で使えるか
- fake instance を渡す API の制約

特に重要:

```text
original UserRepository type と isolated ALC 内の UserRepository type は別 identity になる可能性がある。
```

この場合、`Mock.Class<UserRepository>()` で作った fake が rewritten assembly 内の `UserRepository` と互換になるかを慎重に検討してください。

### 5. 型 identity 問題

以下を重点的に設計してください。

- fake instance の型 identity
- rewritten assembly 側の `UserRepository`
- default ALC 側の `UserRepository`
- public contract / interface 経由にする必要があるか
- sample assembly を isolated ALC にロードした場合、test code 側からどう型を取得するか
- reflection-only style で実行するか
- strongly typed API を維持できるか
- weakly typed harness API が必要か

候補 API:

```csharp
var service = harness.Create("Sample.UserService");
var result = service.Invoke<string>("GetDisplayName", 1);
```

または:

```csharp
var serviceType = harness.GetType("Sample.UserService");
var service = Activator.CreateInstance(serviceType);
```

### 6. unload 戦略

以下を設計してください。

- harness Dispose で ALC unload
- WeakReference で unload 確認
- GC.Collect / GC.WaitForPendingFinalizers の扱い
- unload 失敗時 diagnostics
- instance / Type / Assembly の参照を残さないための注意
- MSTest の test class field に保持した場合の注意

### 7. diagnostics

設計する diagnostics:

- loaded assembly list
- resolved dependency list
- unresolved dependency
- ALC name
- is collectible
- unload attempted
- unload succeeded / failed
- remaining strong reference の推定 hint
- rewritten assembly path
- original assembly path

### 8. Phase 12 用プロンプト

設計ドキュメントの末尾に、Phase 12 の PoC 実装用プロンプトを含めてください。

## 成果物

以下を作成してください。

- `docs/shims-assemblyloadcontext-isolation-design.md`

必要に応じて以下も更新してください。

- `docs/shims-new-interception-design.md`
- `docs/shims-constructor-args-design.md`

## この Phase では実装しないこと

以下は実装しないでください。

- AssemblyLoadContext isolation の本格実装
- static method mocking
- BCL type 差し替え
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- Visual Studio Test Explorer 完全統合

## 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- ALC isolation 設計の要約
- 型 identity 問題の整理
- ShimContext / ShimDispatcher との関係
- 推奨する Phase 12 PoC 方針
- 実装しなかったこと
- `dotnet build` の結果
- `dotnet test` の結果
