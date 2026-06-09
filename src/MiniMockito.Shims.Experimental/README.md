# MiniMockito.Shims.Experimental

> **⚠️ EXPERIMENTAL — 実験的パッケージです。API は予告なく変更されます。本番コードへの組み込みは避けてください。**

`new SomeClass()` の差し替えや user-defined static メソッドのモックを行う実験的なパッケージです。  
Mono.Cecil でビルド後に IL をリライトし、isolated AssemblyLoadContext (ALC) で動かします。

詳細なドキュメントは [リポジトリの docs/shims-experimental-quickstart.md](https://github.com/kaneko11/offgrid-gatch-mock/blob/main/docs/shims-experimental-quickstart.md) を参照してください。

## 必須設定

```csharp
// AssemblyInfo.cs
[assembly: DoNotParallelize]
```

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

## できないこと

- BCL static メソッド（`DateTime.Now`、`File.ReadAllText` 等）
- generic static メソッド
- expression-based API（`Shim.Static(() => Clock.Now())`）
- parallel test（`[assembly: DoNotParallelize]` 必須）
