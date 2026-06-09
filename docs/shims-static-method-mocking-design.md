# Shims Static Method Mocking Design

## 1. 目的

このドキュメントは `MiniMockito.Shims.Experimental` における static method mocking の将来設計を整理します。

**Phase 13 では本格実装を行いません。** static call pattern の分類・IL rewrite 方針・API 設計・ALC との関係・parallel test risk・diagnostics を整理し、Phase 14 の PoC 実装プロンプトを提供することが目的です。

---

## 2. これまでの実装との比較

### 2.1 `newobj` 差し替え（Phase 4〜12）の概要

```
call site: newobj UserRepository::.ctor(string)
              ↓ rewrite
call site: call <ShimsWrappers>::__Shims_new_UserRepository_String(string)
              ↓ wrapper body
call: ShimDispatcher.NewWithArgs<UserRepository>(object?[])
              ↓ dispatch
Registry lookup by (isolated ALC の UserRepository 型) → fake instance を返す
```

特徴:
- キー: `Type` オブジェクト（isolated ALC の型）
- 型 identity 問題あり → `GetRewrittenType()` で回避
- IL opcode: `newobj` → `call`

### 2.2 static call 差し替え（Phase 14 以降）の方針

```
call site: call static DateTime Clock::Now()
              ↓ rewrite
call site: call <ShimsStaticWrappers>::__Shims_Call_Clock_Now()
              ↓ wrapper body
try: StaticShimDispatcher.TryInvoke<DateTime>("Sample.Clock", "Now", [], [], out result)
  → match: return result (shimmed value)
  → no match: return Clock.Now()  ← 直接 fallback（wrapper は rewrite 対象外）
```

特徴:
- キー: **string** ベース（型 identity 問題なし）
- wrapper は `<ShimsStaticWrappers>` クラス（rewrite 対象外）
- IL opcode: `call` → `call`（同じ opcode、Operand だけ変わる）

---

## 3. static call pattern の分類

### 3.1 優先度別一覧

| パターン | Phase 14 PoC 対象 | 優先度 | 備考 |
|---------|-----------------|--------|------|
| user-defined static method（引数なし） | ✅ 対象 | 最高 | 最小 PoC |
| user-defined static method（値型・string 引数） | ✅ 対象 | 高 | wrapper boxing で対応 |
| user-defined static method（void 戻り値） | ✅ 対象 | 高 | `InvokeVoid` を追加 |
| user-defined static property getter | ⚠️ 検討 | 中 | `get_Xxx()` call と同等 |
| overloaded static method | ⚠️ 検討 | 中 | `StaticMethodKey` に parameter types を含める |
| generic static method | ❌ Phase 15 以降 | 低 | 型 identity + generics で複雑 |
| extension method (静的実装) | ❌ Phase 15 以降 | 低 | extension はただの static method |
| async static method (Task<T>) | ❌ Phase 15 以降 | 低 | Task unwrap が必要 |
| BCL static method (DateTime.Now 等) | ❌ Phase 15 以降 | 低 | BCL assembly rewrite は対象外 |
| `DateTime.Now` / `DateTime.UtcNow` | ❌ Phase 15 以降 | 低 | user assembly 内の call site を rewrite する可能性あり（下記 Section 6）|
| `Guid.NewGuid()` | ❌ Phase 15 以降 | 低 | BCL |
| `File.ReadAllText(...)` | ❌ Phase 15 以降 | 低 | BCL |
| `static method with ref/out params` | ❌ 非対応 | 対象外 | IL 生成が複雑 |

### 3.2 Phase 14 PoC の推奨最小 scope

以下を Phase 14 の PoC 対象とする:

1. **user-defined public static class または public class の static method**
2. **non-generic**
3. **parameterless または 値型 / string 引数のみ**
4. **simple return value（値型 / string / reference type）**
5. **void return**
6. **BCL static method は対象外**

サンプル:

```csharp
// 対象例（Phase 14）
public static class Clock
{
    public static DateTime Now() => DateTime.Now;
    public static string GetZoneName() => "UTC";
    public static string GetLabel(int id) => $"label-{id}";
    public static void LogCall(string message) { /* ... */ }
}
```

---

## 4. IL rewrite 方針

### 4.1 `call` 命令の差し替え

IL 上の static method call:

```
; Clock.Now() — 引数なし
call valuetype [System.Private.CoreLib]System.DateTime Sample.Clock::Now()

; Clock.GetLabel(int) — int 引数
ldarg.1
call string Sample.Clock::GetLabel(int32)
```

Phase 7 の `newobj` rewrite と同様に、**wrapper method 生成方式**を採用する。

### 4.2 Wrapper Method 生成方式（推奨）

Wrapper は `<ShimsStaticWrappers>` クラスに生成する（rewrite 対象から除外済み）。

```csharp
// 生成される wrapper — Clock.Now()
internal static DateTime __Shims_Call_Clock_Now()
{
    if (StaticShimDispatcher.TryInvoke<DateTime>(
            "Sample.Clock", "Now", Type.EmptyTypes, [], out var result))
        return result;
    return Clock.Now(); // fallback: 直接 real call（wrapper は rewrite 対象外なのでループしない）
}

// 生成される wrapper — Clock.GetLabel(int id)
internal static string __Shims_Call_Clock_GetLabel_Int32(int id)
{
    if (StaticShimDispatcher.TryInvoke<string>(
            "Sample.Clock", "GetLabel", [typeof(int)], [(object)id], out var result))
        return result;
    return Clock.GetLabel(id);
}

// 生成される wrapper — void Clock.LogCall(string)
internal static void __Shims_Call_Clock_LogCall_String(string message)
{
    if (StaticShimDispatcher.TryInvokeVoid(
            "Sample.Clock", "LogCall", [typeof(string)], [(object)message]))
        return;
    Clock.LogCall(message);
}
```

### 4.3 Wrapper 方式の利点

| 項目 | 直接 dispatcher call | Wrapper 方式 |
|-----|---------------------|------------|
| return type 処理 | call site で boxing/unboxing が必要 | wrapper 内で処理 |
| 値型 return | IL が複雑 | wrapper が `(T)result` キャスト |
| void return | call site の修正が複雑 | wrapper で `if (found) return; else real();` |
| fallback | dispatcher 側で reflection 必要 | wrapper 内で直接 call（型安全）|
| argument boxing | call site で処理 | wrapper でボックス化 |

### 4.4 Direct Dispatcher Call 方式（参考）

Wrapper を使わず call site を直接置換する方式:

```
; Before
call DateTime Clock::Now()

; After (直接置換)
ldstr   "Sample.Clock"
ldstr   "Now"
ldsfld  class Type[] StaticShimDispatcher::EmptyTypes
newarr  object?
call bool StaticShimDispatcher::TryInvoke<DateTime>(string, string, Type[], object?[], !!0&)
... (条件分岐 + fallback が複雑になる)
```

stack effect が変わる（before は DateTime を push、after は bool + out DateTime が必要）ため、IL 生成が非常に複雑。**Phase 14 では wrapper 方式を採用する。**

### 4.5 Wrapper Method の命名規則

```
__Shims_Call_{TypeName}_{MethodName}_{Param1TypeName}_{Param2TypeName}...
```

例:
- `Clock.Now()` → `__Shims_Call_Clock_Now`
- `Clock.GetLabel(int)` → `__Shims_Call_Clock_GetLabel_Int32`
- `Clock.GetResult(string, bool)` → `__Shims_Call_Clock_GetResult_String_Boolean`

Overload 識別は parameter type name list で行う。同一 wrapper キャッシュキーも同じ命名で管理する。

### 4.6 `newobj` rewriter との差分

| 項目 | newobj rewriter | static call rewriter |
|-----|----------------|---------------------|
| 対象 opcode | `Newobj` | `Call`（static only） |
| スキップ条件 | instance method | instance / virtual method |
| allowlist | target type | target type（declaring type）|
| wrapper prefix | `__Shims_new_` | `__Shims_Call_` |
| wrapper class | `<ShimsWrappers>` | `<ShimsStaticWrappers>` |
| return type | 常に reference type (T) | 任意（値型・void 含む）|
| dispatcher | `ShimDispatcher.NewWithArgs<T>` | `StaticShimDispatcher.TryInvoke<T>` |

---

## 5. API 案

### 5.1 explicit reflection-based API（Phase 14 PoC 推奨）

```csharp
// parameterless static method
Shim.Static(typeof(Clock), nameof(Clock.Now))
    .Returns(fixedTime);

// static method with arguments + matchers
Shim.Static(typeof(Clock), nameof(Clock.GetLabel), typeof(int))
    .Returns("fake-label");

// void static method
Shim.Static(typeof(Clock), nameof(Clock.LogCall), typeof(string))
    .Callback((object?[] args) => recordedArg = (string?)args[0]);

// overload 識別（parameter types で明示）
Shim.Static(typeof(Clock), "Format", typeof(int), typeof(string))
    .Returns("formatted");
```

型安全でなく verbose だが、最初の PoC では最も安全。

### 5.2 expression-based API（Phase 15 以降で検討）

```csharp
Shim.Static(() => Clock.Now())
    .Returns(fixedTime);

Shim.Static(() => Clock.GetLabel(ShimArg.Any<int>()))
    .Returns("fake-label");
```

`Expression<Func<TResult>>` の body を解析する:

```csharp
public static StaticShimBuilder<TResult> Static<TResult>(Expression<Func<TResult>> expr)
{
    if (expr.Body is not MethodCallExpression call)
        throw new ShimException("expression must be a static method call");

    var method = call.Method;
    if (!method.IsStatic)
        throw new ShimException("expression must call a static method");

    // 引数から matcher を抽出
    var matchers = call.Arguments.Select(argExpr =>
    {
        var value = Expression.Lambda(argExpr).Compile().DynamicInvoke();
        return value is IShimArgumentMatcher m ? m : ShimArg.Eq<object>(value);
    }).ToArray();

    return new StaticShimBuilder<TResult>(method, matchers);
}
```

**Expression API の課題:**
- `ShimArg.Any<int>()` のような matcher factory は **expression-tree 評価時に実行** される（`Matches()` は呼ばれない）
- 複雑な expression（delegate call、nested method call）の解析が困難
- `Expression.Compile().DynamicInvoke()` は遅い
- overloaded method の識別は `MethodCallExpression.Method` が解決するため通常問題ない
- **Phase 14 PoC では explicit API のみ実装し、expression API は Phase 15 以降の課題とする**

### 5.3 fluent builder API

```csharp
// Returns<TResult>
public sealed class StaticShimBuilder<TResult>
{
    public StaticShimBuilder<TResult> WithArguments(params IShimArgumentMatcher[] matchers) { ... }
    public void Returns(TResult value) { ... }
    public void Returns(Func<object?[], TResult> factory) { ... }
    public void Throws(Exception exception) { ... }
    public void Throws<TException>() where TException : Exception, new() { ... }
}

// void overload
public sealed class StaticShimBuilder
{
    public StaticShimBuilder WithArguments(params IShimArgumentMatcher[] matchers) { ... }
    public void Callback(Action<object?[]> callback) { ... }
    public void Throws(Exception exception) { ... }
    public void Throws<TException>() where TException : Exception, new() { ... }
    public void DoNothing() { ... }  // void, no-op shim
}
```

`Shim` クラスの拡張:

```csharp
public static class Shim
{
    // 既存
    public static NewShimBuilder<T> New<T>() { ... }

    // 新規 (Phase 14)
    public static StaticShimBuilder<TResult> Static<TResult>(
        Type declaringType,
        string methodName,
        params Type[] parameterTypes) { ... }

    public static StaticShimBuilder Static(
        Type declaringType,
        string methodName,
        params Type[] parameterTypes) { ... } // void overload
}
```

---

## 6. internal model

### 6.1 StaticMethodKey

```csharp
/// <summary>
/// Identifies a specific static method by string, avoiding type identity issues across ALCs.
/// </summary>
public sealed record StaticMethodKey(
    string DeclaringTypeFullName,
    string MethodName,
    string[] ParameterTypeFullNames)
{
    // Factory from MethodInfo
    public static StaticMethodKey From(MethodInfo method)
        => new(
            method.DeclaringType!.FullName!,
            method.Name,
            method.GetParameters().Select(p => p.ParameterType.FullName!).ToArray());

    // Factory from Type + name + paramTypes
    public static StaticMethodKey From(Type declaringType, string methodName, Type[] paramTypes)
        => new(
            declaringType.FullName!,
            methodName,
            paramTypes.Select(p => p.FullName!).ToArray());

    // Key string for lookup
    public string ToKeyString()
        => $"{DeclaringTypeFullName}::{MethodName}({string.Join(",", ParameterTypeFullNames)})";
}
```

**重要:** string-based key を使うことで、isolated ALC の型と default ALC の型の identity 差異を完全に回避する。

### 6.2 StaticShimRule

```csharp
public sealed class StaticShimRule
{
    private readonly Func<object?[], object?> _factory;
    private readonly Action<object?[]>? _callback; // void 用
    private readonly Exception? _thrownException;

    public StaticMethodKey Key { get; }
    public long RegistrationOrder { get; }
    public IReadOnlyList<IShimArgumentMatcher>? ArgumentMatchers { get; }

    // 引数を評価してマッチするか
    public bool MatchesArgs(object?[] args) { ... } // NewShimRule と同じロジック

    // shim を実行して結果を返す
    public bool TryExecute(object?[] args, out object? result) { ... }
}
```

### 6.3 StaticShimRegistry

```csharp
public sealed class StaticShimRegistry
{
    private readonly Dictionary<string, List<StaticShimRule>> _rules = [];
    private readonly object _syncRoot = new();
    private long _nextOrder;

    public StaticShimRule RegisterRule(StaticMethodKey key, Func<object?[], object?> factory,
        IReadOnlyList<IShimArgumentMatcher>? matchers) { ... }

    public bool TryFindRule(StaticMethodKey key, object?[] args,
        out StaticShimRule? rule) { ... } // last-registered-wins

    public bool TryFindRuleWithDiagnostics(StaticMethodKey key, object?[] args,
        out StaticShimRule? rule, out StaticDispatchDiagnostics diagnostics) { ... }

    public void Clear() { ... }
}
```

### 6.4 StaticShimDispatcher

```csharp
public static class StaticShimDispatcher
{
    // TResult を返す static method — wrapper から呼ばれる
    public static bool TryInvoke<TResult>(
        string declaringTypeFullName,
        string methodName,
        Type[] parameterTypes,  // BCL/value 型なら ALC 間で共有される
        object?[] args,
        out TResult result)
    {
        var key = new StaticMethodKey(
            declaringTypeFullName,
            methodName,
            parameterTypes.Select(t => t.FullName!).ToArray());

        var context = ShimContext.Current;
        if (context is { IsDisposed: false })
        {
            if (context.StaticRegistry.TryFindRuleWithDiagnostics(
                    key, args, out var rule, out var diag))
            {
                context.LastStaticDispatchDiagnostics = diag;
                rule!.TryExecute(args, out var rawResult);
                result = (TResult)rawResult!;
                return true;
            }
            context.LastStaticDispatchDiagnostics = diag;
        }

        result = default!;
        return false;
    }

    // void static method — wrapper から呼ばれる
    public static bool TryInvokeVoid(
        string declaringTypeFullName,
        string methodName,
        Type[] parameterTypes,
        object?[] args)
    {
        // 同様のロジック。rule が found の場合だけ true
    }
}
```

### 6.5 StaticInvocationContext（ShimConstructorContext の対応物）

```csharp
public sealed class StaticInvocationContext
{
    public string DeclaringTypeFullName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public IReadOnlyList<object?> Arguments { get; init; } = [];
    public T? GetArgument<T>(int index) => (T?)Arguments[index];
}
```

### 6.6 StaticRewritePlan / StaticCallSite

```csharp
public sealed class StaticCallSite
{
    public string CallingTypeName { get; init; } = string.Empty;
    public string CallingMethodName { get; init; } = string.Empty;
    public int ILOffset { get; init; }
    public string TargetTypeFullName { get; init; } = string.Empty;
    public string TargetMethodName { get; init; } = string.Empty;
    public string[] ParameterTypeNames { get; init; } = [];
    public string ReturnTypeName { get; init; } = string.Empty;
    public bool IsVoid { get; init; }
}

public sealed class StaticRewriteResult
{
    public int RewrittenCallSiteCount { get; init; }
    public IReadOnlyList<StaticCallSite> RewrittenCallSites { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public IReadOnlyList<string> Skipped { get; init; } = [];
}
```

---

## 7. rule matching

### 7.1 マッチングロジック

`NewShimRule` のマッチングと同一ロジックを再利用する。

```
dispatcher が key + args を受け取る
  → StaticShimRegistry.TryFindRuleWithDiagnostics(key, args)
  → rules.LastToFirst:
      rule.matchers == null → catch-all → match
      args.Length != matchers.Count → arg count mismatch → skip
      else → 各 matcher を評価（Matches()、Captor は副作用あり）
              全 matcher が true → match
              最初の false → skip
  → 最初に match した rule → execute
  → no match → fallback（wrapper が直接呼ぶ）
```

### 7.2 `IShimArgumentMatcher` / `ShimCaptor<T>` の再利用

`IShimArgumentMatcher` は既存のまま再利用できる。

```csharp
var captor = ShimCaptor.For<string>();

Shim.Static(typeof(Clock), nameof(Clock.GetLabel), typeof(int))
    .WithArguments(ShimArg.Any<int>())
    .Returns("any-label");

// または captor で capture
Shim.Static(typeof(Clock), nameof(Clock.GetLabel), typeof(int))
    .WithArguments(captor) // captor は IShimArgumentMatcher を実装
    .Returns("captured-label");
```

**再利用できるもの:**
- `IShimArgumentMatcher` interface
- `ShimAnyMatcher<T>`
- `ShimEqMatcher<T>`
- `ShimPredicateMatcher<T>`
- `ShimCaptor<T>`
- `ShimArg` factory

**新規実装が必要なもの:**
- `StaticShimRule`（NewShimRule と類似、ただし factory が `Func<object?[], TResult>` ベース）
- `StaticShimRegistry`
- `StaticShimDispatcher`
- `StaticShimBuilder<T>`

### 7.3 last registered wins

`NewShimRule` と同じ "last registered wins" ポリシーを適用する。

### 7.4 no match fallback

Wrapper が直接 real method を呼ぶため、dispatcher 側で fallback 実装は不要。

```csharp
// wrapper の fallback
internal static DateTime __Shims_Call_Clock_Now()
{
    if (StaticShimDispatcher.TryInvoke<DateTime>(..., out var r)) return r;
    return Clock.Now(); // ← real call
}
```

### 7.5 throws

```csharp
Shim.Static(typeof(Clock), nameof(Clock.Now))
    .Throws(new InvalidOperationException("clock unavailable"));
```

`StaticShimRule.TryExecute()` で exception を throw する。Wrapper の中で propagate される。

---

## 8. BCL static method の扱い

### 8.1 Phase 14 PoC では BCL を対象外にする

理由:
- BCL assembly 自体を rewrite しない方針（production assembly in-place rewrite 禁止）
- `System.Private.CoreLib.dll` を rewrite すると全プロセスに影響
- JIT 最適化・ReadyToRun・intrinsics が壊れる可能性がある

### 8.2 `DateTime.Now` の扱い

BCL の `DateTime.get_Now()` を直接 shim するのではなく、**user assembly 内の call site を rewrite する方針**を将来検討する。

```
; UserService.cs 内の
; var t = DateTime.Now;
; は IL 上では:
call valuetype DateTime DateTime::get_Now()

; これを user assembly 内でのみ置換:
call DateTime __Shims_Call_DateTime_get_Now()
              ↓
StaticShimDispatcher.TryInvoke<DateTime>("System.DateTime", "get_Now", ...)
```

つまり:
- `DateTime.dll` 自体は書き換えない
- user assembly 内の `call DateTime::get_Now()` だけを wrapper に差し替える
- これにより BCL 本体に触れずに `DateTime.Now` をモックできる可能性がある

**ただし Phase 14 では BCL call site を rewrite しない。** `DateTime.Now` mocking は Phase 15 以降に回す。

### 8.3 BCL call を許可する場合のリスク

| リスク | 内容 |
|-------|------|
| JIT intrinsic の破壊 | `DateTime.Now` は JIT が最適化する可能性。wrapper 経由では最適化されない |
| 型解決の失敗 | BCL 型の FullName は `System.DateTime, System.Private.CoreLib, Version=...` と長い。string key が一致しない可能性 |
| readonly struct | `DateTime` は readonly struct。wrapper の return type 扱いに注意 |
| async state machine | BCL async method body 内の static call は rewrite 対象外 |

### 8.4 allowlist による制限

```csharp
// 安全のため BCL assembly を allowlist から除外
private static readonly HashSet<string> BclAssemblyPrefixes = new(StringComparer.OrdinalIgnoreCase)
{
    "System.",
    "Microsoft.",
    "mscorlib",
};

// AllowedDeclaringTypes に BCL が含まれていてもスキップ
if (IsBclType(declaringType))
{
    diagnostics.Add($"Skipped: BCL type '{declaringType.FullName}' is not supported in Phase 14.");
    continue;
}
```

---

## 9. AssemblyLoadContext との関係

### 9.1 type identity 問題の解消

static method mocking では dispatcher key を **string** ベースにすることで、Phase 12 の型 identity 問題を完全に回避できる。

```
ルール登録側（default ALC）:
  Shim.Static(typeof(Clock), "Now")
  → key: StaticMethodKey("Sample.Clock", "Now", [])

dispatcher 側（isolated ALC の rewritten assembly から呼ばれる）:
  StaticShimDispatcher.TryInvoke<DateTime>("Sample.Clock", "Now", Type.EmptyTypes, [], ...)
  → key: StaticMethodKey("Sample.Clock", "Now", [])

→ 完全一致！型 identity 問題なし。
```

### 9.2 StaticShimDispatcher の共有

`StaticShimDispatcher` は `MiniMockito.Shims.Experimental.dll` に置く。

- isolated ALC の Load() で `null` を返す（parent ALC fallback）
- `ShimContext.StaticRegistry` も default ALC の registry
- rule 登録と dispatch が同じ registry を参照 ✓

### 9.3 harness との統合

```csharp
// 既存 harness の RewriteAssembly では newobj を rewrite
// Phase 14 では static call も rewrite するオプションを追加

using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()            // newobj 対象
    .WithStaticTarget(typeof(Clock))         // static call 対象
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    // static shim 登録（default ALC の typeof(Clock) でよい — string key で一致）
    Shim.Static(typeof(Clock), nameof(Clock.Now))
        .Returns(fixedTime);

    var service = harness.Create<UserService>();
    var result = harness.Invoke<DateTime>(service, nameof(UserService.GetNow));
    Assert.AreEqual(fixedTime, result);
}
```

### 9.4 dependency resolution

static call rewrite 後の dependency は `newobj` rewrite と同様:
- wrapper class `<ShimsStaticWrappers>` は rewritten assembly 内に生成
- `StaticShimDispatcher` は default ALC 共有
- BCL 型（`Type.EmptyTypes` 等）は共有 ✓

---

## 10. ShimContext 拡張

```csharp
public sealed class ShimContext : IDisposable
{
    // 既存
    internal ShimRuleRegistry Registry { get; } = new();
    public ShimDispatchDiagnostics? LastDispatchDiagnostics { get; internal set; }

    // Phase 14 追加
    internal StaticShimRegistry StaticRegistry { get; } = new();
    public StaticDispatchDiagnostics? LastStaticDispatchDiagnostics { get; internal set; }

    public void Dispose()
    {
        // 既存
        Registry.Clear();
        // 追加
        StaticRegistry.Clear();
    }
}
```

---

## 11. parallel test risk

static method mocking は `newobj` shim と **同じリスク**を持つ。

| リスク | 説明 |
|-------|------|
| `StaticShimRegistry` の test 間干渉 | `ShimContext` スコープ外で registry に rule が残留する可能性（`ShimContext.Dispose()` で Clear されれば安全） |
| `AsyncLocal` による非同期 flow 分離 | `ShimContext` は `AsyncLocal` ベース → async/await 内は安全だが、parallel test では干渉する |
| last stub wins の順序不定 | 並列実行中に複数の test が同じ method に rule を登録すると、どの rule が勝つか不定 |
| wrapper method のスレッドセーフ性 | `StaticShimDispatcher.TryInvoke` は registry を lock で保護するため thread-safe |

**結論:** `[assembly: DoNotParallelize]` または `[DoNotParallelize]` を継続して必須とする。
`[DoNotParallelize]` でも `async` 内では `AsyncLocal` による分離が機能するため、`async` 対応テストは安全に書ける。

---

## 12. diagnostics

### 12.1 StaticDispatchDiagnostics

```csharp
public sealed class StaticDispatchDiagnostics
{
    public string DeclaringTypeFullName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string[] ParameterTypeFullNames { get; init; } = [];
    public IReadOnlyList<object?> ActualArguments { get; init; } = [];
    public IReadOnlyList<TriedRuleInfo> TriedRules { get; init; } = [];
    public bool MatchFound { get; init; }
    public bool FalledBack => !MatchFound;

    public string Format() { ... }

    public sealed class TriedRuleInfo
    {
        public long RegistrationOrder { get; init; }
        public IReadOnlyList<string> MatcherDescriptions { get; init; } = [];
        public bool Matched { get; init; }
        public string MismatchReason { get; init; } = string.Empty;
    }
}
```

`Format()` 出力例:

```
No matching static shim rule was found.

Target: Sample.Clock::GetLabel(System.Int32)
Actual arguments:
  [0] 42 (System.Int32)

Tried rules:
  Rule #1:
    [0] expected: Eq<Int32>(99)
    result: mismatch
    reason: Matcher [0] (Eq<Int32>(99)) did not match actual value: 42

Fallback: real static method call
```

### 12.2 Phase 14 で追加する diagnostics

| 診断項目 | 内容 |
|---------|------|
| `TargetMethod` | declaring type + method name + parameter types |
| `CallingMethod` | rewritten assembly 内の calling method |
| `ILOffset` | 差し替えた IL 命令の offset |
| `ActualArguments` | dispatcher に渡された boxed 引数 |
| `TriedRules` | 評価した rule ごとの matcher 結果 |
| `SelectedRule` | match した rule の registration order |
| `FalledBack` | fallback した（real call）か |
| `BclMethodWarning` | BCL method が allowlist に含まれていた場合の警告 |
| `GenericMethodWarning` | generic static method がスキップされた場合の警告 |

---

## 13. フェーズ間の関係

| Phase | 内容 |
|-------|------|
| 4〜6 | parameterless newobj rewrite PoC |
| 7 | constructor arguments 対応（wrapper method 生成パターン確立） |
| 8 | WithArguments matcher API |
| 9 | ShimCaptor |
| 10 | API polish / diagnostics |
| 11 | ALC isolation 設計 |
| 12 | ALC isolation PoC |
| **13（本ドキュメント）** | **static method mocking 設計** |
| 14 | static method mocking skeleton / PoC |
| 15 | BCL call site rewrite（DateTime.Now 等）調査 |

---

## 14. Phase 14 実装プロンプト

```markdown
# PROMPT.shims.phase14.static-method-mocking-poc.md

# MiniMockito.Shims.Experimental Phase 14: static method mocking skeleton / PoC

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、
docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md、
docs/shims-assemblyloadcontext-isolation-design.md、
docs/shims-static-method-mocking-design.md を読んでください。

## この Phase の目的

Phase 13 の設計をベースに、user-defined static method の差し替え最小 PoC を実装してください。

本格実装ではありません。以下の最小 scope に限定します。

## 実装対象

### 0. sample assembly への Clock クラス追加

`MiniMockito.Shims.Experimental.Sample` に以下を追加してください:

```csharp
public static class Clock
{
    public static DateTime Now() => DateTime.Now;
    public static string GetLabel(int id) => $"label-{id}";
    public static void LogCall(string message) { }
}

public class TimedUserService
{
    public string GetTimedDisplayName(int id)
    {
        var ts = Clock.Now().ToString("yyyyMMdd");
        return $"{id}-{ts}";
    }

    public string GetLabeledName(int id)
    {
        var label = Clock.GetLabel(id);
        return $"user-{label}";
    }
}
```

### 1. StaticMethodKey

- string-based record (`DeclaringTypeFullName`, `MethodName`, `ParameterTypeFullNames[]`)
- `From(MethodInfo)` / `From(Type, string, Type[])` factory
- `ToKeyString()` → `"TypeFull::Method(p1,p2)"`
- equality / hash code based on key strings

### 2. StaticShimRule

- `Key: StaticMethodKey`
- `RegistrationOrder: long`
- `ArgumentMatchers: IReadOnlyList<IShimArgumentMatcher>?`
- `MatchesArgs(object?[]) → bool` （`NewShimRule.MatchesArgs` と同じロジック）
- `TryExecute(object?[], out object? result) → bool`
- factory: `Func<object?[], object?>` （TResult は object? で返す）
- throws: `Exception?`
- void callback: `Action<object?[]>?`

### 3. StaticShimRegistry

- `RegisterRule(StaticMethodKey, Func<object?[], object?>, IReadOnlyList<IShimArgumentMatcher>?) → StaticShimRule`
- `RegisterVoidRule(StaticMethodKey, Action<object?[]>?, IReadOnlyList<IShimArgumentMatcher>?) → StaticShimRule`
- `TryFindRule(StaticMethodKey, object?[], out StaticShimRule?) → bool` (last-wins)
- `TryFindRuleWithDiagnostics(...)` → optional for Phase 14
- `Clear()`

### 4. StaticShimDispatcher

```csharp
public static class StaticShimDispatcher
{
    public static bool TryInvoke<TResult>(
        string declaringTypeFullName,
        string methodName,
        Type[] parameterTypes,
        object?[] args,
        out TResult result);

    public static bool TryInvokeVoid(
        string declaringTypeFullName,
        string methodName,
        Type[] parameterTypes,
        object?[] args);
}
```

### 5. StaticShimBuilder<TResult> と StaticShimBuilder (void)

```csharp
public sealed class StaticShimBuilder<TResult>
{
    public StaticShimBuilder<TResult> WithArguments(params IShimArgumentMatcher[] matchers) { ... }
    public void Returns(TResult value) { ... }
    public void Returns(Func<TResult> factory) { ... }
    public void Returns(Func<object?[], TResult> factory) { ... }
    public void Throws(Exception ex) { ... }
    public void Throws<TException>() where TException : Exception, new() { ... }
}

public sealed class StaticShimBuilder
{
    public StaticShimBuilder WithArguments(params IShimArgumentMatcher[] matchers) { ... }
    public void DoNothing() { ... }
    public void Callback(Action<object?[]> action) { ... }
    public void Throws(Exception ex) { ... }
}
```

### 6. Shim クラスへの追加

```csharp
public static StaticShimBuilder<TResult> Static<TResult>(
    Type declaringType,
    string methodName,
    params Type[] parameterTypes) { ... }

public static StaticShimBuilder Static(
    Type declaringType,
    string methodName,
    params Type[] parameterTypes) { ... }
```

### 7. ShimContext への拡張

```csharp
internal StaticShimRegistry StaticRegistry { get; } = new();
```

`Dispose()` で `StaticRegistry.Clear()` を追加する。

### 8. StaticCallSiteScanner

```csharp
// call 命令をスキャンして StaticCallSite を返す
public static IReadOnlyList<StaticCallSite> Scan(
    ModuleDefinition module,
    ISet<string> targetTypeFullNames);
```

### 9. StaticCallSiteRewriter（最小 PoC）

- `call` 命令で宣言型が allowlist に含まれているものを wrapper に差し替える
- `<ShimsStaticWrappers>` クラスに wrapper を生成
- wrapper 命名: `__Shims_Call_{TypeName}_{MethodName}_{ParamTypes...}`
- 引数は boxing して `object?[]` に束ねる
- wrapper は `StaticShimDispatcher.TryInvoke<T>()` または `TryInvokeVoid()` を呼び、
  found の場合はその結果を返し、not found の場合は real method を直接 call する
- by-ref, generic, params はスキップ（診断に記録）

### 10. AssemblyRewriter 拡張

`AssemblyRewriter.RewriteNewObj` と同様に `AssemblyRewriter.RewriteStaticCalls` を追加。
または既存の `RewriteNewObj` を拡張して `RewriteOptions` に static target を追加。

### 11. MSTest

以下のテストを追加してください。

#### unit tests（rewrite なし）

- `StaticShimDispatcher.TryInvoke` が rule なし / あり で正しく動く
- `WithArguments(Eq(42))` が正しく match する
- `ShimCaptor` が static call の引数を capture する
- void static method が shim される
- throws が propagate される

#### integration tests（rewrite あり）

- `TimedUserService.GetTimedDisplayName` で `Clock.Now()` が shim される
- `TimedUserService.GetLabeledName` で `Clock.GetLabel(int)` が shim される（Eq matcher）
- shim なし fallback で real `Clock.Now()` が呼ばれる
- original assembly は変更されない

#### regression tests

- Phase 7〜12 の既存テストが壊れていない
- `ShimDispatcher.New<T>` と `StaticShimDispatcher.TryInvoke` が同じ `ShimContext` 内で共存する

## 実装しないこと

- BCL static method rewrite
- expression-based API (`Shim.Static(() => Clock.Now())`)
- generic static method
- by-ref / out パラメータ
- async static method
- `DateTime.Now` mocking
- runtime IL rewrite
- production assembly in-place rewrite

## 型 identity 方針

`StaticMethodKey` は string ベースで識別するため、isolated ALC の型と default ALC の型の
identity 差異を考慮する必要はない。

`Shim.Static(typeof(Clock), "Now")` で登録した key と、
isolated ALC の rewritten assembly から呼ばれた dispatcher の key は
同じ string になるため一致する。

## 検証

最後に以下を実行してください。

```bash
dotnet build
dotnet test
```

## 完了時の報告（日本語）

- 変更ファイル一覧
- 実装した static method mocking PoC
- API
- 型 identity の方針
- rewrite 対象と対象外
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
```

---

## 15. Phase 14 実装ノート

> **実装日:** 2026-06-09  
> **ビルド:** 成功（0 警告、0 エラー）  
> **テスト:** 214 件すべて成功（Phase 14 新規 30 件含む）

### 15.1 追加・変更ファイル一覧

| ファイル | 種別 | 内容 |
|--------|------|------|
| `tests/.../Sample/StaticClock.cs` | NEW | `StaticClock` 静的クラス・`TimedService` クラス（テスト用 sample） |
| `src/.../StaticMethodKey.cs` | NEW | string ベースの static method 識別子 |
| `src/.../StaticShimRule.cs` | NEW | 3 種類のルール（ReturnValue / Void / Throw） |
| `src/.../StaticShimBuilder.cs` | NEW | `StaticShimBuilder<TResult>` / `StaticShimBuilder` 流暢 API |
| `src/.../StaticShimRegistry.cs` | NEW | `Dictionary<string, List<StaticShimRule>>` ルール格納、lock 保護 |
| `src/.../StaticShimDispatcher.cs` | NEW | `TryInvoke<TResult>` / `TryInvokeVoid` — wrapper から呼ばれる entry point |
| `src/.../StaticInvocationContext.cs` | NEW | 呼び出しコンテキスト DTO |
| `src/.../StaticDispatchDiagnostics.cs` | NEW | dispatch 試行の診断情報、`Format()` 出力付き |
| `src/.../Rewrite/StaticCallSite.cs` | NEW | IL rewrite 結果の call site 記述 |
| `src/.../Rewrite/StaticRewriteResult.cs` | NEW | `StaticCallRewriter.Rewrite()` の戻り値 |
| `src/.../Rewrite/StaticCallRewriter.cs` | NEW | Mono.Cecil ベース static call 書き換え器 |
| `src/.../ShimContext.cs` | MODIFIED | `StaticRegistry`・`LastStaticDispatchDiagnostics` プロパティ追加、`Dispose` で `StaticRegistry.Clear()` |
| `src/.../Shim.cs` | MODIFIED | `Static<TResult>(string, string, params Type[])` など 4 オーバーロード追加 |
| `src/.../Rewrite/RewriteOptions.cs` | MODIFIED | `StaticTargetTypes` プロパティ追加 |
| `src/.../Rewrite/AssemblyRewriter.cs` | MODIFIED | `StaticTargetTypes.Count > 0` 時に `StaticCallRewriter.Rewrite()` 呼び出し |
| `src/.../NewInterceptionHarness.cs` | MODIFIED | `WithStaticTarget(Type)`・`RewriteTargetTypeAssembly()` 更新 |
| `tests/.../Phase14StaticShimTests.cs` | NEW | MSTest 30 件（unit / integration / regression） |
| `docs/shims-static-method-mocking-design.md` | MODIFIED | 本節（Phase 14 実装ノート）追加 |

### 15.2 実装した API

```csharp
// 非 void: string-based
Shim.Static<DateTime>("My.Ns.Clock", "Now")
    .Returns(fixedTime);

// 非 void: Type-based (内部で FullName に変換)
Shim.Static<string>(typeof(StaticClock), "GetName", typeof(int))
    .WithArguments(ShimArg.Eq(42))
    .Returns("shimmed-name");

// void
Shim.Static(typeof(StaticClock).FullName!, "LogCall", typeof(string))
    .Callback(args => Console.WriteLine(args[0]));

// ハーネス
using var harness = NewInterceptionHarness.Create()
    .WithStaticTarget(typeof(StaticClock))   // Phase 14 新規
    .WithTarget<UserRepository>()            // newobj（Phase 7〜12 と共存）
    .RewriteTargetTypeAssembly();
```

### 15.3 型 identity 方針（実装確認済み）

`StaticMethodKey` の形式: `"Full.TypeName::MethodName(Param.Type1,Param.Type2)"`

- **登録時**: `Shim.Static(typeof(StaticClock), "Now")` → `"...Sample.StaticClock::Now()"`
- **dispatch 時** (wrapper 内): `TryInvoke<DateTime>("...Sample.StaticClock", "Now", [], [], out result)` → 同じ key を生成

isolated ALC の型と default ALC の型の identity 差異は **string の一致** によって完全に吸収される。`GetRewrittenType()` のような型変換は static shim では不要。

### 15.4 rewrite 対象と対象外

| 条件 | 結果 |
|------|------|
| allowlist の non-BCL static method | ✅ wrapper に書き換え |
| BCL 型 (`System.Private.CoreLib` 等) | ⛔ スキップ |
| generic メソッド / generic 型 | ⛔ スキップ |
| by-ref / out パラメータ | ⛔ スキップ |
| `<ShimsStaticWrappers>` クラス自身 | ⛔ 無限ループ防止のため除外 |
| `StaticTargetTypes` が空のとき | ⛔ `StaticCallRewriter` 呼び出し自体なし |

### 15.5 テスト構成（Phase 14 新規 30 件）

```
Phase14StaticShimTests [DoNotParallelize]
├── Unit tests (no IL rewrite)       … 17 件
│   ├── NoRule → returns false
│   ├── WithRule → returns shimmed value
│   ├── Eq / Any / ShimCaptor matchers
│   ├── void method (Callback / DoNothing)
│   ├── throws propagation
│   ├── last-stub-wins
│   ├── Type-based API
│   ├── diagnostics (MatchFound / FalledBack / Format)
│   └── registry Clear / Dispose
├── Integration tests (harness+rewrite)  … 8 件
│   ├── Now() shimmed
│   ├── GetName(int) + Eq matcher
│   ├── no-match fallback to real
│   ├── bool arg shimmed
│   ├── Any matcher catch-all
│   ├── no shim registered → real method
│   ├── original assembly not modified
│   └── static + newobj shim coexist
└── Regression / unsupported           … 5 件
    ├── generic target type (no crash)
    ├── newobj shim still works after Phase 14
    ├── no StaticTargetTypes → no static rewrite
    └── v1/v2 Shim.New still compiles
```

### 15.6 既知の制約

| 制約 | 内容 |
|------|------|
| BCL rewrite 未対応 | `DateTime.Now` など BCL static は差し替え不可（Phase 15 検討） |
| expression-based API なし | `Shim.Static(() => Clock.Now())` の型安全 API は Phase 15 以降 |
| generic static method 未対応 | `Enumerable.Empty<T>()` などはスキップ |
| async static method | 動作するが `AsyncLocal` の注意事項は同様 |
| parallel test | `[DoNotParallelize]` 必須（`ShimContext` の process-wide 性質は不変） |

### 15.7 次に推奨する Phase

- **Phase 15**: `DateTime.Now` など BCL static call の差し替え調査（ALC からの BCL アクセス、PoC）
- **Phase 16**: expression-based `Shim.Static(() => Clock.Now())` API（Roslyn source generator 検討）
