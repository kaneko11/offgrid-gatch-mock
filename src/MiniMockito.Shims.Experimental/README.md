# MiniMockito.Shims.Experimental

> **⚠️ EXPERIMENTAL / TEST-ONLY — 実験的かつテスト専用パッケージです。API は予告なく変更されます。本番コードへの組み込みは避けてください。**

`new SomeClass()` の差し替えや user-defined static メソッドのモックを行う実験的なパッケージです。  
Mono.Cecil でビルド後に IL をリライトし、isolated AssemblyLoadContext (ALC) で動かします。
**production assembly を in-place で書き換えることはありません**（rewrite 済みコピーを一時ディレクトリに出力します）。

詳細なドキュメントは [リポジトリの docs/shims-experimental-quickstart.md](https://github.com/kaneko11/offgrid-gatch-mock/blob/main/docs/shims-experimental-quickstart.md) を参照してください。

## 必須設定

```csharp
// AssemblyInfo.cs — process-wide な state の並列衝突を防ぐ（必須）
[assembly: DoNotParallelize]
```

## Easy API（`Shims.ForAssembly(...).ReplaceNew(...)`・最推奨）

cross-assembly の `new` 差し替えは Easy API で短く書けます。

```csharp
using MiniMockito.Shims.Experimental;

// 外部型を assembly path + type full name で指定（コンパイル時参照不要）
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    // → "fake-1"
}
```

- 1 session で複数 `ReplaceNew(...)` を登録でき、internal / external を混在できます。
- 同じ target type に複数回 `ReplaceNew` した場合は **last stub wins**。
- 引数条件で fake を分けたい場合は `New<T>().WithArguments(...).Returns(...)` を使います。

## 型安全な instance method 差し替え

新規コードでは exact `MethodInfo` を第一推奨にします。戻り値型や引数をメソッド名・コメント・
呼び出し方から推測せず、Reflection で確認してください。

```csharp
var method = typeof(ExternalLib.ExternalTableLoader).GetMethod(
    "Load",
    new[]
    {
        typeof(object),
        typeof(string),
        typeof(bool)
    });

using (var shims = Shims.ForAssembly(targetAssemblyPath))
{
    shims.ReplaceMethod<int>(method)
         .WithArguments(
             ShimArg.Any<object>(),
             ShimArg.Any<string>(),
             ShimArg.Eq(true))
         .Returns(0);

    var service = shims.CreateObject(
        "TargetApp.ConstructorCallsIntMethod");
}
```

- overload は exact `MethodInfo` または全 `parameterTypes` で指定します。
- optional parameter もシグネチャに含めます。引数なしは `Type.EmptyTypes` を使います。
- void は `ReplaceVoidMethod(...).DoNothing()` / `.Callback(...)` を使います。
- `ReplaceMethod<TResult>` は reflected return type と `TResult` を登録時に照合します。
- legacy untyped `ReplaceMethod` は後方互換用 advanced API です。
- legacy callback が non-nullable value type に `null` を返すと、unbox 前に
  `ShimReturnTypeMismatchException` を投げます。

## new SomeClass() の差し替え

```csharp
using MiniMockito.Shims.Experimental;

[TestClass]
[DoNotParallelize]
public class MyTests
{
    [TestMethod]
    public void New_UserRepository_IsShimmed()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fake = harness.CreateFake<UserRepository>("fake");

        using (ShimContext.Create())
        {
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayName), 1);

            Assert.AreEqual("fake-1", result);
        }
    }
}
```

## user-defined static メソッドの差し替え

```csharp
var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

using var harness = NewInterceptionHarness.Create()
    .WithStaticTarget(typeof(StaticClock))
    .RewriteTargetTypeAssembly();

using (ShimContext.Create())
{
    Shim.Static<DateTime>(typeof(StaticClock).FullName!, "Now")
        .Returns(fixedTime);

    var service = harness.Create<TimedService>();
    var result = harness.Invoke<string>(service, nameof(TimedService.GetTimedName), 1);

    Assert.AreEqual($"1-{fixedTime:yyyyMMdd}", result);
}
```

## できないこと / 注意

- **test-only**: production コードには組み込まないでください。
- **production assembly の in-place rewrite はしません**（rewrite 済みコピーを別出力します）。
- BCL static メソッドのモックは **未対応**（`DateTime.Now`、`File.ReadAllText` 等）。
- generic static メソッド、expression-based API（`Shim.Static(() => Clock.Now())`）は未対応。
- Microsoft Fakes Shim の完全互換・完全代替ではありません。
- parallel test は不可（`[assembly: DoNotParallelize]` 必須）。
- public API は将来変更され得ます（alpha）。
