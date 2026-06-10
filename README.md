# MiniMockito.Net

.NET / MSTest 向けの軽量モックフレームワークです。  
interface mock / spy を中心に、v2 で class proxy（public virtual メソッド）をサポートします。

Microsoft Fakes の代替ではありません。proxy ベースでできないこと（`new` の差し替え、static メソッド等）は別パッケージ `MiniMockito.Shims.Experimental` に分離しています。

---

## 目次

1. [できること / できないこと](#1-できること--できないこと)
2. [インストール / ビルド](#2-インストール--ビルド)
3. [他プロジェクトからの使い方](#3-他プロジェクトからの使い方)
4. [Interface Mock](#4-interface-mock)
5. [Interface Spy](#5-interface-spy)
6. [Class Proxy](#6-class-proxy)
7. [Class Spy / Partial Mock](#7-class-spy--partial-mock)
8. [スタブ](#8-スタブ)
9. [マッチャー](#9-マッチャー)
10. [Captor（引数キャプチャ）](#10-captor引数キャプチャ)
11. [検証（Verify）](#11-検証verify)
12. [InOrder（順序検証）](#12-inorder順序検証)
13. [Strict / Lenient](#13-strict--lenient)
14. [非同期メソッド](#14-非同期メソッド)
15. [エラーメッセージ](#15-エラーメッセージ)
16. [Experimental Shims（new / static の差し替え）](#16-experimental-shimsnew--static-の差し替え)
17. [.NET Framework 4.8（net48）での使い方](#17-net-framework-48net48での使い方)
18. [既知の制約](#18-既知の制約)

---

## 1. できること / できないこと

### v2 でできること

- `Mock.Of<T>()` で interface モックを作成
- `Spy.Of<T>(realInstance)` で interface spy を作成
- `Mock.Class<T>()` で class モックを作成
- `Spy.Class<T>()` / `Mock.Class<T>(ClassMockOptions.CallBase)` で class spy / partial mock を作成
- メソッド・引数・タイムスタンプ・戻り値・例外・スレッド ID・モック ID を記録
- `When` / `ThenReturn` / `ThenThrow` / `ThenAnswer` / `ThenReturnSequence` でスタブを設定
- `Any` / `Eq` / `Is` / `Null` / `NotNull` / `InRange` で引数マッチング
- `Capture<T>()` で引数キャプチャ
- `Times.Once` / `Exactly` / `Never` / `AtLeast` / `AtMost` で呼び出し検証
- `VerifyNoInteractions` / `VerifyNoMoreInteractions`
- `InOrder` で複数モック間の順序検証
- `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>` のデフォルト非同期戻り値

### v2 でできないこと

| 機能 | 対応状況 |
|------|---------|
| `new SomeClass()` の差し替え | ❌ → `MiniMockito.Shims.Experimental` |
| static メソッドのモック | ❌ → `MiniMockito.Shims.Experimental`（user-defined のみ） |
| sealed class のモック | ❌ |
| non-virtual メソッドの差し替え | ❌ |
| private メソッドの差し替え | ❌ |
| コンストラクタの差し替え | ❌ → `MiniMockito.Shims.Experimental` |
| runtime IL rewrite | ❌ |
| CLR Profiler API ベースの shim | ❌ |
| BCL / .NET Framework の呼び出し差し替え | ❌ |

---

## 2. インストール / ビルド

現在はソースからビルドします。

```powershell
dotnet restore
dotnet build
dotnet test
```

プロジェクト構成:

```
src/
  MiniMockito/                              ← ライブラリ本体 (v2)
  MiniMockito.Shims.Experimental/          ← 実験的パッケージ (new / static shim)

tests/
  MiniMockito.Tests/                              ← v2 テスト (77件)
  MiniMockito.Shims.Experimental.Tests/          ← Shims テスト (235件)
  MiniMockito.Shims.Experimental.Net48Tests/     ← Shims net48 テスト (23件)
  MiniMockito.Shims.Experimental.Sample/         ← Shims テスト用サンプルアセンブリ

samples/
  MiniMockito.Sample/                       ← コンソールサンプル
  MiniMockito.Sample.MSTest/               ← MSTest 実行可能サンプル (6件)
```

**テスト結果（現時点）:**

| アセンブリ | フレームワーク | 合格 | 失敗 |
|-----------|--------------|------|------|
| MiniMockito.Tests | net8.0 | 77 | 0 |
| MiniMockito.Shims.Experimental.Tests | net8.0 | 235 | 0 |
| MiniMockito.Shims.Experimental.Net48Tests | net48 | 23 | 0 |
| MiniMockito.Sample.MSTest | net8.0 | 6 | 0 |
| **合計** | | **341** | **0** |

---

## 3. 他プロジェクトからの使い方

### 方法 A: プロジェクト参照（同一リポジトリ・monorepo の場合）

テストプロジェクトの `.csproj` に追加します。

```xml
<!-- MiniMockito 本体のみ使う場合 -->
<ItemGroup>
  <ProjectReference Include="パス/to/offgrid-gatch-mock/src/MiniMockito/MiniMockito.csproj" />
</ItemGroup>

<!-- Experimental Shims も使う場合（実験的） -->
<ItemGroup>
  <ProjectReference Include="パス/to/offgrid-gatch-mock/src/MiniMockito/MiniMockito.csproj" />
  <ProjectReference Include="パス/to/offgrid-gatch-mock/src/MiniMockito.Shims.Experimental/MiniMockito.Shims.Experimental.csproj" />
</ItemGroup>
```

### 方法 B: ローカル NuGet パック（別リポジトリから使う場合）

```powershell
# 1a. パッケージを生成（本体）
dotnet pack src/MiniMockito -c Release -o artifacts
# → artifacts/MiniMockito.Net.0.2.0-preview.5.nupkg
# → artifacts/MiniMockito.Net.0.2.0-preview.5.snupkg  ← シンボルパッケージ

# 1b. Experimental Shims も使う場合（実験的）
dotnet pack src/MiniMockito.Shims.Experimental -c Release -o artifacts
# → artifacts/MiniMockito.Shims.Experimental.0.1.0-alpha.3.nupkg
# → artifacts/MiniMockito.Shims.Experimental.0.1.0-alpha.3.snupkg

# 1c. 両方まとめてパックする
dotnet pack -c Release -o artifacts

# 2. ローカルフィードを登録（テストプロジェクト側で実行）
dotnet nuget add source C:\path\to\artifacts --name local-minimockito

# 3. テストプロジェクトの .csproj に追加
# <PackageReference Include="MiniMockito.Net" Version="0.2.0-preview.5" />
# <PackageReference Include="MiniMockito.Shims.Experimental" Version="0.1.0-alpha.3" />  ← 実験的
```

**Shims パッケージに含まれるもの:**

| ファイル | 内容 |
|---------|------|
| `lib/net8.0/MiniMockito.Shims.Experimental.dll` | ライブラリ本体（net8.0） |
| `lib/net8.0/MiniMockito.Shims.Experimental.xml` | XML ドキュメント（IDE インテリセンス用） |
| `lib/net48/MiniMockito.Shims.Experimental.dll` | ライブラリ本体（net48） |
| `lib/net48/MiniMockito.Shims.Experimental.xml` | XML ドキュメント（IDE インテリセンス用） |
| `README.md` | パッケージ説明 |
| `Mono.Cecil 0.11.6` | 依存パッケージ（自動で取得される） |

### 使い始め

```csharp
using MiniMockito;
using static MiniMockito.Mock;   // When / Verify / Any などをクラス名なしで使う

[TestClass]
public class MyServiceTests
{
    [TestMethod]
    public void GetDisplayName_Returns_Mocked_Value()
    {
        var repo = Mock.Of<IUserRepository>();

        When(() => repo.FindById(Any<int>()))
            .ThenReturn("mocked-user");

        var sut = new MyService(repo);
        Assert.AreEqual("mocked-user", sut.GetDisplayName(1));

        Verify(() => repo.FindById(1), Times.Once());
    }
}
```

---

## 4. Interface Mock

```csharp
var service = Mock.Of<IUserService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

Assert.AreEqual("abc", service.GetName(123));
Verify(() => service.GetName(123), Times.Once());
```

lenient モックはスタブが未設定の呼び出しにデフォルト値を返します。  
interface mock は `DispatchProxy` ベースなので `T` は interface である必要があります。

---

## 5. Interface Spy

```csharp
var realService = new RealUserService();
var spy = Spy.Of<IUserService>(realService);

When(() => spy.GetName(0))
    .ThenReturn("stubbed");

Assert.AreEqual("stubbed", spy.GetName(0));   // stub あり → "stubbed"
Assert.AreEqual("real-7", spy.GetName(7));    // stub なし → real 実装に委譲
```

スタブに一致しない呼び出しは、渡した実インスタンスに委譲されます。

---

## 6. Class Proxy

```csharp
var repository = Mock.Class<UserRepository>();

When(() => repository.FindName(1))
    .ThenReturn("mocked");

Assert.AreEqual("mocked", repository.FindName(1));
Verify(() => repository.FindName(1), Times.Once());
```

class proxy の制約:

- `T` は public かつ non-sealed クラス
- `T` に public または protected のパラメーターなしコンストラクターが必要
- **public virtual メソッドのみ** インターセプト可能
- non-virtual / static / private / generic / `ref` / `out` は非対応

---

## 7. Class Spy / Partial Mock

```csharp
var calculator = Spy.Class<TaxCalculator>();

When(() => calculator.GetRate("test"))
    .ThenReturn(0.20m);

Assert.AreEqual(0.20m, calculator.GetRate("test"));    // stub あり → 0.20m
Assert.AreEqual(0.10m, calculator.GetRate("default")); // stub なし → base 実装
```

`Spy.Class<T>()` と `Mock.Class<T>(ClassMockOptions.CallBase)` はスタブに一致しない呼び出しで base 実装を呼びます。

---

## 8. スタブ

```csharp
// 例外をスロー
When(() => service.GetName(1))
    .ThenThrow(new InvalidOperationException());

// 引数に応じて動的に返す
When(() => service.GetName(Any<int>()))
    .ThenAnswer(ctx => "id=" + ctx.Arguments[0]);

// 順番に返す（末尾の値を繰り返す）
When(() => service.GetName(2))
    .ThenReturnSequence("a", "b", "c");
```

---

## 9. マッチャー

```csharp
When(() => service.GetName(Any<int>())).ThenReturn("any");
When(() => service.GetName(Eq(10))).ThenReturn("ten");
When(() => service.GetName(Is<int>(v => v > 0))).ThenReturn("positive");
When(() => service.Find(Null<string>())).ThenReturn("missing");
When(() => service.Find(NotNull<string>())).ThenReturn("present");
When(() => service.GetName(InRange(1, 5))).ThenReturn("range");
```

マッチャーを使わない引数は等値比較（equality）でマッチします。

---

## 10. Captor（引数キャプチャ）

```csharp
var captor = Capture<string>();

service.Save("abc");

Verify(() => service.Save(captor.Value));

Assert.AreEqual("abc", captor.CapturedValue);
```

Captor の値は `Verify` が成功した後にのみセットされます。

---

## 11. 検証（Verify）

```csharp
service.Save("abc");

Verify(() => service.Save("abc"), Times.Once());
Verify(() => service.Save("missing"), Times.Never());
VerifyNoMoreInteractions(service);
```

`Verify` に成功すると、一致した invocation が「検証済み」としてマークされます。  
`Verify(...)` 自体の式評価は invocation として記録されません。

---

## 12. InOrder（順序検証）

```csharp
var first = Mock.Of<IWorkflowStep>();
var second = Mock.Class<WorkflowStep>();

first.Start();
second.Save();
first.End();

var order = InOrder(first, second);
order.Verify(() => first.Start());
order.Verify(() => second.Save());
order.Verify(() => first.End());
```

`InOrder` はグローバルな invocation シーケンス番号を使うため、  
interface mock・class proxy・spy をまたいだ順序検証が可能です。

---

## 13. Strict / Lenient

```csharp
var lenient = Mock.Of<IUserService>();                             // lenient（デフォルト）
var strict  = Mock.Of<IUserService>(MockBehavior.Strict);         // strict
var strictClass = Mock.Class<UserRepository>(MockBehavior.Strict); // class proxy strict
```

- **Lenient**: スタブ未設定の呼び出しはデフォルト値を返します。
- **Strict**: スタブ未設定の呼び出しは `MockException` / `ClassProxyException` をスローします。

---

## 14. 非同期メソッド

スタブ未設定の非同期メソッドは完了済みのデフォルト値を返します:

- `Task` → `Task.CompletedTask`
- `Task<T>` → `default(T)` の完了済み Task
- `ValueTask` / `ValueTask<T>` → 同様

```csharp
When(() => service.GetNameAsync(Any<int>()))
    .ThenReturn("abc");   // 論理的な戻り値を直接渡す

Assert.AreEqual("abc", await service.GetNameAsync(123));
```

---

## 15. エラーメッセージ

検証失敗時のメッセージ（IDE / CI 診断用のラベル付き）:

```
Wanted:
Actual invocations:
Matching invocations:
Method:
Expected count:
Actual count:
Arguments:
Closest recorded calls:
```

class proxy 失敗時のメッセージ:

```
Target class:
Method:
Reason:
Supported methods:
Unsupported methods:
Hint:
```

---

## 16. Experimental Shims（new / static の差し替え）

> **⚠️ 実験的パッケージです。API は予告なく変更されます。本番コードへの組み込みは避けてください。**

`MiniMockito.Shims.Experimental` は `new SomeClass()` や user-defined static メソッドを  
テスト中に差し替えるための実験的な仕組みを提供します。

Mono.Cecil で IL をビルド後にリライトし、isolated AssemblyLoadContext (ALC) で動かします。  
元のアセンブリは**絶対に上書きしません**。

### 必須設定

```csharp
// AssemblyInfo.cs — process-wide な state の並列衝突を防ぐ
[assembly: DoNotParallelize]
```

### new SomeClass() の差し替え

```csharp
using MiniMockito.Shims.Experimental;

[TestClass]
[DoNotParallelize]
public class RepositoryShimTests
{
    [TestMethod]
    public void New_UserRepository_IsShimmed()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()        // 差し替え対象クラスを登録
            .RewriteTargetTypeAssembly();        // IL をリライト（temp dir へ書き出し）

        var fake = harness.CreateFake<UserRepository>("fake");

        using (ShimContext.Create())
        {
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();  // リライト済み ALC から生成
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayName), 1);

            Assert.AreEqual("fake-1", result);
        }
    }
}
```

### コンストラクタ引数マッチャー

```csharp
using static MiniMockito.Shims.Experimental.ShimArg;

using (ShimContext.Create())
{
    // Eq("prod") に一致するコンストラクタ呼び出しだけ差し替える
    harness.RegisterShimWithMatchers<UserRepository>(fake, Eq<string>("prod"));

    // Any<string>() — 任意の string に一致
    // Is<string>(s => s.StartsWith("prod")) — 述語マッチ
}
```

### ShimCaptor（コンストラクタ引数をキャプチャ）

```csharp
var captor = ShimCaptor.For<string>();

using (ShimContext.Create())
{
    harness.RegisterShimWithMatchers<UserRepository>(fake, captor);
    // ... テスト実行 ...
}

Assert.AreEqual("prod", captor.Value);
```

### user-defined static メソッドの差し替え

```csharp
var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

using var harness = NewInterceptionHarness.Create()
    .WithStaticTarget(typeof(StaticClock))   // static 差し替え対象を登録
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

### Experimental Shims の制約

| 制約 | 理由 |
|------|------|
| `[assembly: DoNotParallelize]` 必須 | ShimDispatcher が process-wide state を持つ |
| BCL static メソッドは差し替え不可 | `DateTime.Now`、`File.ReadAllText` 等 |
| generic static メソッドはスキップ | Mono.Cecil での取り扱いが複雑 |
| original assembly は変更しない | 一時ディレクトリへの書き出しのみ |
| `using (ShimContext.Create())` 必須 | Dispose しないと rule が残る |

詳細は [`docs/shims-experimental-quickstart.md`](docs/shims-experimental-quickstart.md) を参照してください。

---

## 17. .NET Framework 4.8（net48）での使い方

`MiniMockito.Net` と `MiniMockito.Shims.Experimental` はどちらも `net8.0;net48` のマルチターゲットです。  
net48 プロジェクトから参照した場合、NuGet が自動的に net48 向けバイナリを選択します。  
**モック API（`Mock.Of` / `When` / `Verify` 等）は net8.0 と完全に同じです。**

### csproj の設定

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <!-- LangVersion を省略すると C# 7.3 が既定になる。 -->
    <!-- より新しい構文を使いたい場合は明示指定する。    -->
    <!-- <LangVersion>12.0</LangVersion>               -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MiniMockito.Net" Version="0.2.0-preview.5" />
    <!-- Shims を使う場合（実験的） -->
    <PackageReference Include="MiniMockito.Shims.Experimental" Version="0.1.0-alpha.3" />
  </ItemGroup>
</Project>
```

### Interface Mock（C# 7.3）

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito;
using static MiniMockito.Mock;

[TestClass]
public class UserServiceTests
{
    [TestMethod]
    public void GetDisplayName_ReturnsStubbed()
    {
        var repo = Mock.Of<IUserRepository>();

        When(() => repo.FindById(Any<int>()))
            .ThenReturn("stubbed-user");

        var sut = new UserService(repo);
        Assert.AreEqual("stubbed-user", sut.GetDisplayName(1));

        Verify(() => repo.FindById(1), Times.Once());
    }
}
```

> C# 7.3 では `using var` は使えません。`Dispose` が必要なオブジェクトは `using (var x = ...) { }` を使います。

### Class Proxy（C# 7.3）

```csharp
[TestMethod]
public void ClassProxy_VirtualMethod_ReturnsStubbed()
{
    var repository = Mock.Class<UserRepository>();

    When(() => repository.FindName(1))
        .ThenReturn("mocked");

    Assert.AreEqual("mocked", repository.FindName(1));
    Verify(() => repository.FindName(1), Times.Once());
}
```

### Experimental Shims — new の差し替え（C# 7.3）

```csharp
using MiniMockito.Shims.Experimental;
using static MiniMockito.Shims.Experimental.ShimArg;

[TestClass]
[DoNotParallelize]
public class Net48ShimTests
{
    [TestMethod]
    public void New_UserRepository_IsShimmed()
    {
        // C# 7.3: using (...) { } を使う（using var は不可）
        using (var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly())
        {
            var fake = harness.CreateFake<UserRepository>("fake");

            using (ShimContext.Create())
            {
                harness.RegisterShim<UserRepository>(fake);

                object service = harness.Create<UserService>();
                string result = harness.Invoke<string>(service, "GetDisplayName", 1);

                Assert.AreEqual("fake-1", result);
            }
        }
    }

    [TestMethod]
    public void New_UserRepository_WithArgMatcher()
    {
        using (var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly())
        {
            var fake = harness.CreateFake<UserRepository>("prod-fake");

            using (ShimContext.Create())
            {
                // C# 7.3: Eq<T>() / Any<T>() の書き方は同じ
                harness.RegisterShimWithMatchers<UserRepository>(fake, Eq<string>("prod"));

                object service = harness.Create<UserService>();
                string result = harness.Invoke<string>(service, "GetDisplayNameWithArg", 1);

                Assert.AreEqual("prod-fake-1", result);
            }
        }
    }
}
```

### Experimental Shims — static の差し替え（C# 7.3）

```csharp
[TestMethod]
public void Static_GetLabel_IsShimmed()
{
    using (var harness = NewInterceptionHarness.Create()
        .WithStaticTarget(typeof(StaticClock))
        .RewriteTargetTypeAssembly())
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName,
                    "GetLabel",
                    typeof(int))
                .Returns("shimmed-label");

            object service = harness.Create<TimedService>();
            string result = harness.Invoke<string>(service, "GetLabel", 5);

            Assert.AreEqual("shimmed-label", result);
        }
    }
}
```

### C# バージョン別の主な構文差異

| 構文 | LangVersion 7.3 | LangVersion 12 |
|------|----------------|----------------|
| ローカルの using | `using (var x = ...) { }` | `using var x = ...` |
| 配列リテラル | `new object[] { (object)42 }` | `[(object)42]` |
| コレクション型 | `new List<string>()` | `[]` |
| index-from-end | `arr[arr.Length - 1]` | `arr[^1]` |
| null forgiving | 不可 | `x!` |
| nullable 型アノテーション | 不可 | `string?` |

> LangVersion を指定していない net48 プロジェクトは C# 7.3 が既定です。  
> `<LangVersion>12.0</LangVersion>` を設定すれば上表の右列の構文が使えます。

---

## 18. 既知の制約

- interface mock / spy は interface のみ対応
- class mock / spy は public non-sealed クラスかつ public / protected parameterless コンストラクター必須
- class proxy は public virtual メソッドのみインターセプト
- static / sealed / non-virtual / private / コンストラクターの直接 new インターセプトは本体では非対応
- runtime IL rewrite / profiler API ベースの shim は本体では非対応
- generic メソッドと `ref` / `out` パラメーターは class proxy MVP 対象外
- Moq / NSubstitute / FakeItEasy / JustMock / Rhino Mocks / Microsoft Fakes / Castle DynamicProxy への依存なし
