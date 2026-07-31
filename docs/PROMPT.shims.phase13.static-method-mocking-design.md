# PROMPT.shims.phase13.static-method-mocking-design.md

# MiniMockito.Shims.Experimental Phase 13: static method mocking design

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md、docs/shims-assemblyloadcontext-isolation-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 13 として、static method mocking の設計調査を行ってください。

この Phase では static method mocking の本格実装はしないでください。  
目的は、`DateTime.Now` や user-defined static method のような static call を、将来どの方式で差し替えるかを設計することです。

## 背景

これまでの shims experimental は `newobj` を `ShimDispatcher.New<T>()` / `ShimDispatcher.NewWithArgs<T>()` に差し替える方式を検証してきました。

static method mocking は `newobj` ではなく、IL の `call` 命令を差し替える領域です。

例:

```csharp
public static class Clock
{
    public static DateTime Now() => DateTime.Now;
}

public class UserService
{
    public DateTime GetNow()
    {
        return Clock.Now();
    }
}
```

将来的には以下のような API を検討します。

```csharp
using (ShimContext.Create())
{
    Shim.Static(() => Clock.Now())
        .Returns(fixedTime);

    var service = harness.Create<UserService>();

    Assert.AreEqual(fixedTime, service.GetNow());
}
```

ただし、この Phase では実装せず、設計とリスク整理に限定してください。

## 設計対象

以下を設計してください。

### 1. 対象 call pattern

static method mocking で扱う候補を分類してください。

- user-defined static method
- BCL static method
- property getter static call
- `DateTime.Now`
- `DateTime.UtcNow`
- `Guid.NewGuid()`
- `File.ReadAllText(...)`
- extension method
- generic static method
- overloaded static method
- static method with arguments
- static method returning void
- static method throwing exception

最初の PoC で扱うべき対象を限定してください。

推奨最小 scope:

- user-defined static method
- non-generic static class or normal class
- public static method
- simple return value
- no arguments または simple arguments
- BCL static method は対象外

### 2. IL rewrite 方針

`call SomeStatic.Type::Method(...)` を dispatcher call に置き換える方針を設計してください。

概念例:

Before:

```csharp
var now = Clock.Now();
```

After:

```csharp
var now = ShimStaticDispatcher.Invoke<DateTime>(
    typeof(Clock),
    "Now",
    Type.EmptyTypes,
    Array.Empty<object?>());
```

または、generated wrapper method を使う案も検討してください。

```csharp
private static DateTime __Shims_Static_Clock_Now()
{
    return ShimStaticDispatcher.Invoke<DateTime>(...);
}
```

Phase 7 の wrapper method generation の知見を再利用できるか検討してください。

### 3. API 案

候補 API を設計してください。

```csharp
Shim.Static(() => Clock.Now())
    .Returns(fixedTime);
```

```csharp
Shim.Static(() => Clock.GetName(ShimArg.Any<int>()))
    .Returns("fake");
```

ただし expression tree 解析が複雑な場合は、最初は明示 API でも構いません。

```csharp
Shim.Static(typeof(Clock), nameof(Clock.Now))
    .Returns(fixedTime);
```

```csharp
Shim.Static<DateTime>(typeof(Clock), nameof(Clock.Now), Type.EmptyTypes)
    .Returns(fixedTime);
```

設計すべき項目:

- expression-based API
- explicit reflection-based API
- method overload 識別
- argument matcher
- return value
- throws
- void method
- async return
- generic method
- BCL method の扱い

### 4. internal model

以下の model を検討してください。

- StaticShimRule
- StaticShimBuilder<TResult>
- StaticShimDispatcher
- StaticMethodKey
- StaticInvocationContext
- StaticArgumentMatcher
- StaticRewritePlan
- StaticCallSite
- StaticRewriteReport
- StaticUnsupportedReason

### 5. rule matching

以下を整理してください。

- method identity
- declaring type
- method name
- parameter types
- generic arguments
- argument values
- argument matchers
- last stub wins
- no match fallback
- throws
- return default

new shim と同じ matcher / captor を共有できるか検討してください。

### 6. BCL static method の扱い

以下を明確にしてください。

- 初期 PoC では BCL static method を対象外にする
- `DateTime.Now` は後続 Phase に回す
- BCL call を rewrite する場合のリスク
- framework assembly を rewrite しない方針
- user assembly 内の call site だけ rewrite する方針
- `DateTime.Now` 自体を書き換えるのではなく、user assembly 内の `call DateTime::get_Now` を dispatcher に置換する可能性

### 7. AssemblyLoadContext との関係

Phase 11 / 12 の ALC isolation と static method rewrite の関係を整理してください。

- rewritten assembly を isolated ALC にロードするか
- StaticShimDispatcher registry は default ALC と共有できるか
- method key の type identity 問題
- reflection-based API の必要性
- static call rewrite 後の dependency resolution

### 8. parallel test risk

static method mocking は process-wide に見えやすいため、parallel test risk を整理してください。

- rule registry scope
- ShimContext scope
- async-local scope
- static dispatcher
- last stub wins
- test 同時実行時の干渉
- `[DoNotParallelize]` の必要性

### 9. diagnostics

以下の diagnostics を設計してください。

- target method
- declaring type
- method signature
- calling assembly
- calling method
- IL offset
- actual arguments
- tried rules
- selected rule
- no match fallback
- unsupported reason
- BCL method warning
- generic method warning

### 10. Phase 14 用プロンプト

設計ドキュメントの末尾に、Phase 14 の static method mocking skeleton / dry-run 用プロンプトを含めてください。

## 成果物

以下を作成してください。

- `docs/shims-static-method-mocking-design.md`

必要に応じて以下も更新してください。

- `docs/v2-shims-experimental-design.md`
- `docs/shims-new-interception-design.md`
- `docs/shims-assemblyloadcontext-isolation-design.md`

## この Phase では実装しないこと

以下は実装しないでください。

- static method mocking の本格実装
- static call rewrite
- BCL type 差し替え
- `DateTime.Now` mocking
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- Visual Studio Test Explorer 完全統合
- Microsoft Fakes Shim 完全互換

## 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- static method mocking 設計の要約
- 最初の PoC scope
- 対象外にしたものと理由
- BCL static method の扱い
- ALC isolation との関係
- parallel test risk
- Phase 14 の推奨方針
- `dotnet build` の結果
- `dotnet test` の結果
