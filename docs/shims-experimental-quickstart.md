# MiniMockito.Shims.Experimental — クイックスタート

> **⚠️ EXPERIMENTAL — このパッケージは実験的です。**
> API は予告なく変更されます。本番コードへの組み込みや、他のパッケージからの参照は避けてください。

---

## 1. 概要

`MiniMockito.Shims.Experimental` は、`MiniMockito` 本体の proxy ベースモックでは差し替えられない領域を PoC として検証するパッケージです。

- `MiniMockito` 本体 (v1/v2) は interface proxy / class proxy ベースです。  
- **direct `new` interception** や **static method mocking** は proxy では扱えません。
- これらの高リスク領域を、本体から完全に分離して実験します。

---

## 2. できること

| 機能 | 状態 |
|------|------|
| `new SomeClass()` の差し替え（parameterless constructor） | ✅ 対応 |
| `new SomeClass(arg)` の差し替え（constructor arguments） | ✅ 対応 |
| constructor argument matcher（Any / Eq / Is） | ✅ 対応 |
| ShimCaptor で constructor argument をキャプチャ | ✅ 対応 |
| no match 時に real constructor へ fallback | ✅ 対応 |
| last stub wins | ✅ 対応 |
| isolated AssemblyLoadContext (ALC) でのテスト分離 | ✅ 対応 |
| user-defined static method の差し替え（non-void） | ✅ 対応 (Phase 14) |
| user-defined static method の差し替え（void） | ✅ 対応 (Phase 14) |
| static method argument matcher（Any / Eq / Is） | ✅ 対応 (Phase 14) |
| ShimCaptor で static method argument をキャプチャ | ✅ 対応 (Phase 14) |
| newobj shim と static shim の同時利用 | ✅ 対応 (Phase 14) |
| ShimContext.Dispose() で確実に cleanup | ✅ 対応 |

---

## 3. できないこと

| 機能 | 状態 |
|------|------|
| BCL static method の差し替え（`DateTime.Now`, `File.ReadAllText` 等） | ❌ 未対応 |
| generic static method の差し替え（`Enumerable.Empty<T>()` 等） | ❌ 未対応 |
| expression-based API（`Shim.Static(() => Clock.Now())`） | ❌ 未対応 |
| async static method | ⚠️ 動作するが十分なテストなし |
| by-ref / out パラメータを持つ static method | ❌ 未対応 |
| sealed class のメソッド差し替え | ❌ 未対応（v2 本体外） |
| non-virtual method の差し替え | ❌ 未対応（v2 本体外） |
| private method の差し替え | ❌ 未対応 |
| production assembly の in-place rewrite | ❌ **行いません** |
| runtime IL rewrite（起動後 patch） | ❌ 未対応 |
| CLR Profiling API ベースの shim | ❌ 未対応 |
| detour / method patching | ❌ 未対応 |
| Visual Studio Test Explorer 完全統合 | ⚠️ 部分的（dotnet test は動作） |
| Microsoft Fakes Shim 完全互換 | ❌ 目標外 |

---

## 4. 安全ルール

### 4.1 Parallel test 禁止

shim dispatcher はプロセス全体で共有される状態を持ちます。  
テストを並列実行すると shim rule が衝突し、テスト結果が不定になります。

**必須:** テストアセンブリに `[assembly: DoNotParallelize]` を設定してください。

```csharp
// AssemblyInfo.cs
[assembly: DoNotParallelize]
```

各テストクラスにも `[DoNotParallelize]` を付与することを推奨します。

### 4.2 ShimContext を必ず using で囲む

`ShimContext.Dispose()` が呼ばれると、登録した shim rule が自動でクリアされます。  
`using` ブロックの外に shim rule を漏らさないでください。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>().Returns(fakeRepo);
    // ... test code ...
}
// ここで rule は自動削除される
```

### 4.3 Original assembly は上書きしない

`AssemblyRewriter` および `NewInterceptionHarness` は、書き換えたアセンブリを  
**一時ディレクトリに別ファイルとして出力します**。元のアセンブリは変更されません。

```csharp
// ✅ 安全 — 出力先は temp directory
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly();

Assert.AreNotEqual(
    typeof(UserRepository).Assembly.Location,
    harness.OutputAssemblyPath,
    StringComparison.OrdinalIgnoreCase);
```

### 4.4 ALC isolation の制約

- rewritten assembly は **collectible な isolated ALC** にロードされます。
- isolated ALC の型 identity は default ALC の同名型とは異なります。
- `harness.Create<T>()` / `harness.Invoke<T>(...)` を使って型 identity 差異を回避してください。

---

## 5. パッケージ構成

```
src/
  MiniMockito.Shims.Experimental/     ← ライブラリ本体 (実験的)

tests/
  MiniMockito.Shims.Experimental.Tests/   ← MSTest テスト
  MiniMockito.Shims.Experimental.Sample/  ← テスト用 sample assembly
```

`MiniMockito` 本体は `MiniMockito.Shims.Experimental` を参照しません。  
experimental package は本体 release の安定性に影響しません。

---

## 6. 使い方

### 6.1 parameterless constructor new shim

```csharp
using (ShimContext.Create())
{
    var fakeRepo = new UserRepository("fake");
    Shim.New<UserRepository>().Returns(fakeRepo);

    // UserService.GetDisplayName() 内の new UserRepository() が fakeRepo に差し替わる
    var result = ShimDispatcher.New<UserRepository>();
    Assert.AreSame(fakeRepo, result);
}
```

### 6.2 constructor arguments shim + WithArguments matcher

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly();

var fakeRepo = harness.CreateFake<UserRepository>("fake");

using (ShimContext.Create())
{
    // Eq("prod") — "prod" を渡したコンストラクタ呼び出しだけを差し替える
    harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, ShimArg.Eq<string>("prod"));

    var service = harness.Create<UserService>();
    var result = harness.Invoke<string>(
        service, nameof(UserService.GetDisplayNameWithArgRepository), 1);
    // new UserRepository("prod") → fakeRepo が返る
}
```

使えるマッチャー:

```csharp
ShimArg.Any<string>()          // 任意の string（null は value type は拒否）
ShimArg.Eq("prod")             // 厳密一致
ShimArg.Is<string>(s => ...)   // 述語マッチ

// static import も可能
using static MiniMockito.Shims.Experimental.ShimArg;
Shim.New<UserRepository>().WithArguments(Any<string>()).Returns(fake);
```

### 6.3 ShimCaptor — コンストラクタ引数をキャプチャ

```csharp
var captor = ShimCaptor.For<string>();

using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, captor);

    var service = harness.Create<UserService>();
    harness.Invoke<string>(service, nameof(UserService.GetDisplayNameWithArgRepository), 1);
}

Assert.AreEqual("prod", captor.Value);  // new UserRepository("prod") の "prod" をキャプチャ
```

### 6.4 No match fallback — 一致しなければ real constructor を使う

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    // Eq("other") だけ登録。"prod" には一致しない
    harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, ShimArg.Eq<string>("other"));

    var service = harness.Create<UserService>();
    // GetDisplayNameWithArgRepository は new UserRepository("prod") を呼ぶ
    // → Eq("other") に一致しない → real UserRepository("prod") が使われる
    var result = harness.Invoke<string>(
        service, nameof(UserService.GetDisplayNameWithArgRepository), 5);
    Assert.AreEqual("prod-5", result);
}
```

### 6.5 Last stub wins

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>().Returns(new UserRepository("first"));
    Shim.New<UserRepository>().Returns(new UserRepository("last"));

    var result = ShimDispatcher.New<UserRepository>();
    // 最後に登録した "last" が勝つ
    Assert.AreEqual("last-0", result.GetName(0));
}
```

### 6.6 user-defined static method shim

Phase 14 で追加。`NewInterceptionHarness.WithStaticTarget(Type)` で対象クラスを指定します。

```csharp
var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

using var harness = NewInterceptionHarness.Create()
    .WithStaticTarget(typeof(StaticClock))
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    // 文字列ベース API (string-based)
    Shim.Static<DateTime>("MyApp.StaticClock", "Now")
        .Returns(fixedTime);

    // 型ベース API (Type-based) — 内部で FullName に変換
    Shim.Static<DateTime>(typeof(StaticClock), nameof(StaticClock.Now))
        .Returns(fixedTime);

    // 引数あり
    Shim.Static<string>(typeof(StaticClock), "GetName", typeof(int))
        .WithArguments(ShimArg.Eq(42))
        .Returns("shimmed-name");

    var service = harness.Create<TimedService>();
    var result = harness.Invoke<string>(service, nameof(TimedService.GetTimedName), 1);
    Assert.AreEqual($"1-{fixedTime:yyyyMMdd}", result);
}
```

### 6.7 void static method shim

```csharp
using (ShimContext.Create())
{
    // Callback — 引数を受け取る
    Shim.Static(typeof(StaticClock).FullName!, "LogCall", typeof(string))
        .Callback(args => Console.WriteLine(args[0]));

    // DoNothing — 副作用を完全に抑制
    Shim.Static(typeof(StaticClock).FullName!, "LogCall", typeof(string))
        .DoNothing();
}
```

### 6.8 newobj shim と static shim の共存

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()               // newobj 差し替え
    .WithStaticTarget(typeof(StaticClock))      // static call 差し替え
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    harness.RegisterShim<UserRepository>(fakeRepo);

    Shim.Static<DateTime>(typeof(StaticClock).FullName!, "Now")
        .Returns(fixedTime);

    // 両方の shim が同一 ShimContext 内で共存する
}
```

---

## 7. ALC 隔離の仕組み

```
テストコード (default ALC)
  ↓ NewInterceptionHarness.RewriteTargetTypeAssembly()
  ↓ AssemblyRewriter.RewriteNewObj() — temp dir に rewritten assembly を書き出す
  ↓ RewrittenAssemblyLoader — collectible な isolated ALC にロード
  ↓ harness.Create<UserService>() — isolated ALC から UserService を生成
  ↓ harness.Invoke<string>(...) — reflection で UserService のメソッドを呼ぶ
    ↓ rewritten IL: new UserRepository() → <ShimsWrappers>::__Shims_new_UserRepository()
      ↓ ShimDispatcher.New<UserRepository>()
        ↓ ShimContext.Current.Registry (process-wide) から fake を検索
          ↓ fake instance を返す
```

`MiniMockito.Shims.Experimental` 本体は isolated ALC から parent (default) ALC へ
fallback されるため、`ShimDispatcher` / `ShimContext` / `ShimRuleRegistry` は  
プロセス全体でシングルトンとして共有されます。

---

## 8. Diagnostics

### 8.1 ShimContext.LastDispatchDiagnostics (newobj)

```csharp
using var ctx = ShimContext.Create();
Shim.New<UserRepository>().Returns(fake);

ShimDispatcher.New<UserRepository>();

var diag = ctx.LastDispatchDiagnostics;
Console.WriteLine(diag?.Format());
```

### 8.2 ShimContext.LastStaticDispatchDiagnostics (static method)

```csharp
using var ctx = ShimContext.Create();
Shim.Static<string>(typeof(Clock).FullName!, "GetName", typeof(int))
    .WithArguments(ShimArg.Eq(99))
    .Returns("shimmed");

// id=1 を渡すと Eq(99) に一致しない → fallback
StaticShimDispatcher.TryInvoke<string>(
    typeof(Clock).FullName!, "GetName",
    [typeof(int)], [(object)1],
    out _);

var diag = ctx.LastStaticDispatchDiagnostics;
// Format() で人間可読な診断文字列を取得
Console.WriteLine(diag?.Format());
// → Target: MyApp.Clock::GetName(System.Int32)
// → Tried rules:
// →   Rule #1: [0] expected: Eq<Int32>(99), result: mismatch
// → Fallback: real static method call
```

### 8.3 ALC diagnostics

```csharp
var diag = harness.GetAlcDiagnostics();
Console.WriteLine(diag.Format());
// → ALC name: ShimIsolated-MiniMockito.Shims.Experimental.Sample
// → Collectible: True
// → Rewritten path: ...
```

---

## 9. エラーメッセージの読み方

### No active ShimContext

```
No active ShimContext.
Reason: Shim.New<T>() requires an active shim context.
Supported patterns:
  using (ShimContext.Create()) { Shim.New<T>().Returns(fake); }
Hint: Wrap shim setup in using (ShimContext.Create()) before registering rules.
```

**原因:** `ShimContext.Create()` の外で `Shim.New<T>()` / `Shim.Static<T>(...)` を呼んでいます。

### PublicParameterlessConstructorNotFound

```
New shim fallback cannot create a real instance.
Target type: MyApp.NoDefaultCtor
Reason: PublicParameterlessConstructorNotFound
```

**原因:** shim rule がない状態で、parameterless constructor を持たない型の  
`ShimDispatcher.New<T>()` が呼ばれました。

### BCL static 差し替えについて

BCL 型（`DateTime`, `File`, `Guid` 等）の static method は、Phase 14 では差し替えられません。  
allowlist に BCL 型を指定しても、rewriter は対応 call site を自動でスキップします。

```
Skipped BCL static call at StaticClock.Now IL_0000: System.DateTime::get_Now()
```

---

## 10. Known Constraints

- BCL static method (`DateTime.Now` 等) は差し替え不可
- expression-based static API (`Shim.Static(() => Clock.Now())`) は未実装
- generic static method はスキップされる
- by-ref / out パラメータを持つ static method はスキップされる
- parallel test は `[assembly: DoNotParallelize]` 必須
- ALC unload は GC ベース — タイミングは非決定的
- coverage / PDB は rewritten assembly と一致しない（テスト限定の許容事項）
- Visual Studio Test Explorer で ALC isolation テストが不安定になる場合がある

---

## 11. 関連ドキュメント

| ドキュメント | 内容 |
|------------|------|
| `docs/v2-shims-experimental-design.md` | 方式比較（runtime rewrite, profiler API, build-time weaving） |
| `docs/shims-new-interception-design.md` | newobj interception 設計 |
| `docs/shims-constructor-args-design.md` | constructor args shim 設計 |
| `docs/shims-assemblyloadcontext-isolation-design.md` | ALC isolation 設計 |
| `docs/shims-static-method-mocking-design.md` | static method mocking 設計 |
| `docs/shims-experimental-phase14-milestone.md` | Phase 4〜14 到達点まとめ |
