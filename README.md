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
  MiniMockito.Shims.Experimental.Tests/          ← Shims テスト (296件)
  MiniMockito.Shims.Experimental.Net48Tests/     ← Shims net48 テスト (58件)
  MiniMockito.Shims.Experimental.Sample/         ← Shims テスト用サンプルアセンブリ
  MiniMockito.Shims.Experimental.ExternalLib/    ← cross-assembly 用サンプル外部アセンブリ (Phase 20)
  MiniMockito.Shims.Experimental.CrossAssemblySample/ ← cross-assembly 用サンプル TargetApp (Phase 20)

samples/
  MiniMockito.Sample/                       ← コンソールサンプル
  MiniMockito.Sample.MSTest/               ← MSTest 実行可能サンプル (6件)
```

**テスト結果（現時点）:**

| アセンブリ | フレームワーク | 合格 | 失敗 |
|-----------|--------------|------|------|
| MiniMockito.Tests | net8.0 | 77 | 0 |
| MiniMockito.Shims.Experimental.Tests | net8.0 | 303 | 0 |
| MiniMockito.Shims.Experimental.Net48Tests | net48 | 62 | 0 |
| MiniMockito.Net48X86Tests | net48 | 26 | 0 |
| MiniMockito.Sample.MSTest | net8.0 | 6 | 0 |
| **合計** | | **474** | **0** |

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
# → artifacts/MiniMockito.Net.0.2.0-preview.7.nupkg
# → artifacts/MiniMockito.Net.0.2.0-preview.7.snupkg  ← シンボルパッケージ

# 1b. Experimental Shims も使う場合（実験的）
dotnet pack src/MiniMockito.Shims.Experimental -c Release -o artifacts
# → artifacts/MiniMockito.Shims.Experimental.0.1.0-alpha.8.nupkg
# → artifacts/MiniMockito.Shims.Experimental.0.1.0-alpha.8.snupkg

# 1c. 両方まとめてパックする
dotnet pack -c Release -o artifacts

# 2. ローカルフィードを登録（テストプロジェクト側で実行）
dotnet nuget add source C:\path\to\artifacts --name local-minimockito

# 3. テストプロジェクトの .csproj に追加
# <PackageReference Include="MiniMockito.Net" Version="0.2.0-preview.7" />
# <PackageReference Include="MiniMockito.Shims.Experimental" Version="0.1.0-alpha.8" />  ← 実験的
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

**対象となるコード:** interface（抽象）に依存するコード。実装を差し替えてテストしたい依存を interface として受け取っている場合に使います。

```csharp
// 対象（SUT が依存する interface）
public interface IUserService
{
    string GetName(int id);
}

// テスト対象（interface を DI で受け取る）
public class Greeter
{
    private readonly IUserService _service;
    public Greeter(IUserService service) => _service = service;
    public string Greet(int id) => "Hello, " + _service.GetName(id);
}
```

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

**対象となるコード:** interface の **実装がすでに存在し**、その一部の呼び出しだけ差し替えたい場合。スタブしていない呼び出しは実インスタンスに委譲されます。

```csharp
// 対象: interface とその実装
public interface IUserService
{
    string GetName(int id);
}

public class RealUserService : IUserService
{
    public string GetName(int id) => "real-" + id;
}
```

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

**対象となるコード:** interface を持たない **具象クラス**で、差し替えたいメソッドが `public virtual` の場合。virtual メソッドのみインターセプトできます。

```csharp
// 対象: public non-sealed クラス + public virtual メソッド
public class UserRepository
{
    public virtual string FindName(int id) => /* 実 DB アクセスなど */ "db-" + id;
}
```

> non-virtual / static / private / sealed メソッドは差し替えできません。`new UserRepository()` のような
> 直接 `new` の差し替えが必要な場合はセクション 16（Experimental Shims）を参照してください。

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

**対象となるコード:** 具象クラスで、**一部の virtual メソッドだけ差し替え、残りは実装（base）をそのまま使いたい**場合。

```csharp
// 対象: 既定の実装を持つ public virtual メソッド
public class TaxCalculator
{
    public virtual decimal GetRate(string region) => 0.10m;  // 既定 10%
}
```

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

**対象となるコード:** interface も virtual も使わず、メソッド本体の中で **依存を直接 `new` している** コード、
または **user-defined な static メソッドを直接呼んでいる** コード。proxy ベース（セクション 4〜7）では
差し替えられないこれらのパターンが対象です。

```csharp
// 対象 (1): メソッド内で依存を直接 new しているコード
public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();   // ← この new を差し替えたい
        return repository.GetName(id);
    }
}

// 対象 (2): user-defined static メソッドを直接呼んでいるコード
public static class StaticClock
{
    public static string GetName(int id) => "real-name-" + id;
}

public class TimedService
{
    public string GetDisplayName(int id) => StaticClock.GetName(id);  // ← この static 呼び出しを差し替えたい
}
```

> **対象外:** BCL / .NET runtime の型（`new List<T>()` や `DateTime.Now`、`File.ReadAllText` 等）、
> generic / sealed / private、production アセンブリの in-place rewrite は差し替えできません。
> 差し替え対象は test / sample プロジェクト内の **user-defined な public・non-generic 型**に限ります。

### 必須設定

```csharp
// AssemblyInfo.cs — process-wide な state の並列衝突を防ぐ
[assembly: DoNotParallelize]
```

### Easy Shims API（`ReplaceNew` facade・Phase 23・最推奨）

cross-assembly の `new` 差し替えは、`Shims.ForAssembly(...)` + `ReplaceNew(...)` で短く書けます。
`NewInterceptionHarness` / `ShimContext` / `WithExternalTarget` / `RegisterShim` を直接書く必要はありません。

**対象コード（テスト対象の製品コード。これは変更しない）** — `UserService` 内の
`new ExternalDbContext()` が差し替え対象です:

```csharp
// 書き換え対象アセンブリ TargetApp：メソッド内で別アセンブリの型を直接 new している
namespace TargetApp
{
    public class UserService
    {
        public string GetDisplayName(int id)
        {
            using (var ctx = new ExternalLib.ExternalDbContext())   // ← この new を fake に差し替える
            {
                return ctx.FindName(id);
            }
        }
    }
}
```

**テストコード（上の `new` を差し替える）**:

```csharp
// 外部型を assembly path + type full name で指定（コンパイル時参照不要）
// fakeContext は自分で用意した ExternalDbContext の代用インスタンス
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    Assert.AreEqual("fake-1", result);
}

// 外部型をコンパイル時参照できる場合
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew<ExternalDbContext>(fakeContext)) { ... }

// Type で指定する場合
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(typeof(ExternalDbContext), fakeContext)) { ... }
```

1つの session で複数の `ReplaceNew(...)` を登録でき、internal target と external target を混在できます。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeDb)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalLogger", fakeLogger)
                        // internal target は fake が rewrite 済み load context の identity を要するため factory 形式
                        .ReplaceNew<InternalGreeter>(s => s.CreateFake<InternalGreeter>("g")))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "Run", 1);
}
```

- **rewrite 確定タイミング**: 初回 `CreateObject(...)` / `Create<T>()` / `Invoke<TResult>(...)` で確定。
  確定後に `ReplaceNew(...)` を追加すると分かりやすい例外（`rewrite already completed` /
  `target cannot be added after rewrite` / `create a new Shims session`）。
- **same target type に複数回 `ReplaceNew`** した場合は **last stub wins**（既存 `ShimRuleRegistry` 準拠）。
- **引数条件で fake を分けたい**場合は、低レベルの `New<T>().WithArguments(...).Returns(...)` を使ってください
  （`ReplaceNew` は catch-all）。
- **internal target**: 手作りインスタンスは rewrite 済み ALC の型 identity を持たないため差し替わりません。
  internal は `ReplaceNew<T>(s => s.CreateFake<T>(...))`（factory 形式）を使ってください。
- **`Create<T>()`** は load context / assembly identity 上 安全に cast できる場合のみ成功します。
  失敗時は分かりやすい例外を出すので `CreateObject(...)` + `Invoke<TResult>(...)` を使ってください。
- **DbContext 系**などコンストラクタ／`Dispose` に副作用がある型は、実生成に依存しない手動 fake を
  `ReplaceNew(...)` に渡してください。
- **BCL static method**（`DateTime.Now` / `File.ReadAllText` 等）は未対応のままです。
- diagnostics は `shims.Diagnostics` / `shims.LastDispatchDiagnostics` / `shims.GetAlcDiagnostics()` で取得できます。

#### 結果の検証（Inspection API・Phase 24）

`ForAssembly(...).ReplaceNew(...)` は target assembly を rewrite して別ロードするため、戻り値や
object graph の型 identity がテスト側の元の型と一致しないことがあります（`ObservableCollection<T>` の
`T` が rewritten type になる等）。**rewritten object を元の型へ無理に cast せず**、inspection API
（`object` のまま path で観察）で検証してください。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeContext))
{
    var vm = shims.CreateObject("TargetApp.UserViewModel");
    shims.Invoke(vm, "Load");

    // path 指定でスカラー値を取得（プロパティ / ネスト / indexer / Count）
    var count = shims.GetValue<int>(vm, "Items.Count");
    var firstName = shims.GetValue<string>(vm, "Items[0].Name");

    // collection は wrapper として検証（要素型が rewritten type でも OK）
    var items = shims.GetCollection(vm, "Items");
    Assert.AreEqual(1, items.Count);
    Assert.AreEqual("fake-1", items[0].Get<string>("Name"));

    // ShimsObject 経由のネスト / collection アクセス
    var name = shims.Inspect(vm).GetObject("SelectedUser").GetValue<string>("Name");
}
```

- **path 構文**: `Items`, `Items.Count`, `SelectedUser.Name`, `Items[0]`, `Items[0].Name`, `Rows[1].Cells[2].Text`。
- **`GetValue<T>`**: primitive / string / enum / value 型へ変換。`GetValue<object>` は raw を返す。
  rewritten 参照型を同名 original 型へ強制 cast はしません（不一致時は識別ヒント付き `ShimsInspectionException`）。
- **`GetCollection`**: array / `IList` / `IReadOnlyList<T>` / `ICollection<T>` / `ObservableCollection<T>` に対応。
- **例外**: path 途中の null・存在しないプロパティ・index 範囲外は `ShimsInspectionException`（requested path /
  failed segment / runtime type / reason を含む）。
- **DbContext 系**: 手動 fake を `ReplaceNew(...)` に渡し、結果（Items / ViewModel など）は inspection API で検証します。
- **`Create<T>()` との関係**: 型 identity が一致する場合のみ使用。cross-assembly / rewritten シナリオでは
  `CreateObject(...)` + `Invoke(...)` + inspection API を基本にしてください。

#### インスタンスメソッドの差し替え（`ReplaceMethod`・Phase 25 / 26）

`new` / static に続く第3の差し替え。**呼び出し側 IL を書き換える**ので、**非 virtual メソッドや
ジェネリックメソッドも差し替え可能**です（subclass override 不可なメソッドが対象）。declaring 型の
アセンブリは書き換えません。

> **⚠️ `WithStatic(...)` + `Static<T>(...)` と同じ session で併用する場合の順序制約**
>
> **`ReplaceMethod(...)` / `ReplaceNew(...)` は、必ず `Static<T>(...)` より "先" に書いてください。**
> 逆にすると、`CreateObject`/`Invoke` を一度も呼んでいない段階でも、次のエラーになります。
>
> ```
> System.InvalidOperationException: ReplaceNew(...) failed: rewrite already completed.
> target cannot be added after rewrite.
> ```
>
> **なぜこの順序が必要か:**
> `WithStatic(type)` は「この型の static メソッドを差し替え対象にする」という"登録"だけを行い、
> セッションを確定（finalize）させません。一方で `Static<T>(declaringType, methodName)` は、
> 実際に「戻り値をこう設定する」という**設定を行うメソッド**ですが、内部実装として
> 呼び出された瞬間に `EnsureFinalized()` を実行し、**その場でアセンブリの書き換え・ロードを
> 確定させてしまいます**。エラーメッセージは「`CreateObject`/`Invoke` の後に追加するとダメ」と
> 説明していますが、実際には `Static<T>(...)` 自体が `CreateObject` と同じ「確定」処理を
> こっそり引き起こしているため、`CreateObject` を1回も呼んでいなくても発生します。
>
> つまり、同じセッション内でのメソッドの役割は以下のようになっています:
>
> | メソッド | 役割 | セッションを確定させるか |
> |---|---|---|
> | `WithStatic(type)` | 差し替え対象の"登録"のみ | させない |
> | `ReplaceMethod(...)` / `ReplaceNew(...)` | インスタンスメソッド差し替えの"登録"のみ | させない（ただし確定後は使えない） |
> | `Static<T>(...)` | 実際の戻り値の"設定"を行う | **させる（`EnsureFinalized()` が呼ばれる）** |
>
> **具体例（順序による違い）:**
>
> ```csharp
> // ✅ 正しい順序: ReplaceMethod → Static<T> → CreateObject
> using (var shims = Shims.ForAssembly(targetAssemblyPath).WithStatic(typeof(StaticClock)))
> {
>     // ① まず ReplaceMethod(・ReplaceNew)をすべて書く
>     shims.ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "GetName",
>         (receiver, args) => "fake-" + args[0]);
>
>     // ② その後で Static<T> を書く(ここでセッションが確定するが、①は既に登録済みなので問題ない)
>     shims.Static<string>(typeof(StaticClock), "GetName", typeof(int))
>          .Returns("fake-clock");
>
>     // ③ 最後に CreateObject / Invoke
>     var service = shims.CreateObject(...);
> }
>
> // ❌ 誤った順序: Static<T> → ReplaceMethod
> using (var shims = Shims.ForAssembly(targetAssemblyPath).WithStatic(typeof(StaticClock)))
> {
>     shims.Static<string>(typeof(StaticClock), "GetName", typeof(int))
>          .Returns("fake-clock");            // ← この時点でセッションが確定してしまう
>
>     shims.ReplaceMethod(...);                // ← 「もう確定済みだから追加できない」とここで失敗する
> }
> ```
>
> **覚え方:** セッション内では、常に「登録系のメソッド（`WithStatic` / `ReplaceMethod` / `ReplaceNew`）を
> 全部先に書き切ってから、`Static<T>(...)` を書き、最後に `CreateObject`/`Invoke` を呼ぶ」という順番を守ってください。

**対象コード（テスト対象の製品コード。これは変更しない）** — `GatewayUserService` 内の
`ExternalGateway` 呼び出しが差し替え対象です:

```csharp
// 別アセンブリ ExternalLib：実際は外部呼び出しや DB アクセスなど、テストで動かしたくない処理
namespace ExternalLib
{
    public class ExternalGateway
    {
        public string GetName(int id)        { /* …外部リソースへアクセス… */ }
        public IEnumerable<T> Query<T>(string sql) { /* …DB へクエリ… */ }
    }
}

// 書き換え対象アセンブリ TargetApp：テストしたいロジック。上の重い呼び出しを内部で使っている
namespace TargetApp
{
    public class GatewayUserService
    {
        public string Run(int id)
            => new ExternalGateway().GetName(id);                              // ← この呼び出しを差し替える

        public List<GatewayItem> LoadRows()
            => new ExternalGateway().Query<GatewayItem>("select ...").ToList(); // ← これも

        // 要素型が「書き換え対象アセンブリ側」の DTO（QueryRow）のケース
        public List<QueryRow> LoadQueryRows()
            => new ExternalGateway().Query<QueryRow>("select ...").ToList();
    }

    // 書き換え対象アセンブリ側の DTO（可変プロパティ）。NewList / NewObject の組み立て対象。
    public class QueryRow
    {
        public string Name { get; set; }
        public int Code { get; set; }
    }
}
```

**テストコード（上の呼び出しを差し替える）**:

```csharp
// 非 virtual メソッド：GatewayUserService.Run が呼ぶ ExternalGateway.GetName を差し替える
using (var shims = Shims.ForAssembly(targetAssemblyPath)
        .ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "GetName",
            (receiver, args) => "fake-" + args[0]))   // receiver=gateway, args[0]=id
{
    var svc = shims.CreateObject("TargetApp.GatewayUserService");
    Assert.AreEqual("fake-1", shims.Invoke<string>(svc, "Run", 1));
}

// ジェネリックメソッド + 戻り値 interface 差し替え：LoadRows が呼ぶ Query<T>(sql).ToList() を差し替える
using (var shims = Shims.ForAssembly(targetAssemblyPath)
        .ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "Query",
            (receiver, args) => new List<GatewayItem> { new GatewayItem("fake-1") },
            typeof(IEnumerable<>)))                    // 戻り値を IEnumerable<T> として返す
{
    var svc = shims.CreateObject("TargetApp.GatewayUserService");
    var rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRows");
}
```

- virtual 不要（call-site 書き換え）。ジェネリックは型引数 1 個まで。
- **戻り値型の差し替え**: 宣言戻り値が生成不可能な具象型（内部 ctor。EF の `DbRawSqlQuery<T>` 相当）でも、
  直後に `IEnumerable<T>` 等として消費されるなら `returnSubstituteInterface` 指定で差し替え可能。
- no match は実メソッドにフォールバック。**対象外**: BCL 宣言型メソッド・`ref`/`out`・複数型引数。

##### canned 戻り値を組み立てる（`NewObject` / `NewList`・Phase 26）

shim の戻り値は **書き換え後の型**で組む必要があります（同名でも元の型は load context が違い割り当て不可）。
`NewList` / `NewObject` は型を名前で解決し、**匿名オブジェクトのメンバを名前一致で代入**してくれるので、
`Activator` + `SetValue` を書かずに済みます。

```csharp
// shims を先に宣言 → ReplaceMethod を登録（delegate 内で shims を参照するため。
// C# は using (var shims = ...) の初期化子内で自分自身を参照できない）
var shims = Shims.ForAssembly(targetAssemblyPath);
shims.ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "Query",
    (recv, args) => shims.NewList("TargetApp.QueryRow",
                                  new { Name = "A", Code = 1 },          // 1 行目
                                  new { Name = "B", Code = 2 }),         // 2 行目
    typeof(IEnumerable<>));
using (shims)
{
    var svc = shims.CreateObject("TargetApp.GatewayUserService");
    var rows = shims.Invoke<System.Collections.IList>(svc, "LoadQueryRows"); // List<書き換え後の型> は IList で受ける
    Assert.AreEqual("A", shims.GetValue<string>(rows[0], "Name"));
}
```

- `NewList(typeFullName, params rows)` … 各 row（プロパティバッグ）から1要素ずつ作り `List<書き換え後の型>` を返す。
  `typeof(IEnumerable<>)` の戻り値差し替えにそのまま使える。
- `NewObject(typeFullName, new { ... })` … 単一インスタンス版。
- `GetRewrittenType(typeFullName)` … 書き換え後アセンブリの型を名前解決（独自に組みたいとき）。
- `new { Name = "A", Code = 1 }` のように**複数プロパティ・異なる型**を並べられる（設定しないものは既定値）。
  値の型が違えば変換（`Convert.ChangeType` / enum）し、該当メンバが無ければ分かりやすい例外。

- **EF**: `context.Database.SqlQuery<T>(sql).ToList()` も `Database`/`SqlQuery` を target にし
  `typeof(IEnumerable<>)` 指定で差し替え可能（生 SQL を実行せず canned データを返す）。
  canned 行は `shims.NewList("My.QueryData", new { 車名 = "テスト車名" })` の形で組める。

> 低レベル API（`NewInterceptionHarness` / `ShimContext` / `WithExternalTarget` / `RegisterShim`）は
> 引き続き利用可能で、以下の advanced セクションに残しています。Easy API はこれらを内部で利用しています。

### 高レベル API（`Shims` facade・Phase 17・推奨）

`Shims.For<TAnchor>()` を使うと、`NewInterceptionHarness` / `ShimContext` / `RegisterShim` /
reflection Invoke を直接意識せずに `new` / user-defined static method を差し替えられます。

```csharp
using MiniMockito.Shims.Experimental;
using static MiniMockito.Shims.Experimental.ShimArg;

[TestClass]
[DoNotParallelize]
public class RepositoryShimTests
{
    [TestMethod]
    public void New_UserRepository_IsShimmed()
    {
        // TAnchor = 差し替え対象 call site を含むアセンブリを決める型（通常はサービス型）
        using var shims = Shims.For<UserService>()
                               .WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");

        shims.New<UserRepository>()
             .WithArguments(Eq("prod"))   // 省略すると catch-all
             .Returns(fakeRepo);

        // 型 identity 問題を避けるため CreateObject + Invoke を使う（推奨）
        var service = shims.CreateObject(typeof(UserService).FullName);
        var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

user-defined static method、new + static 共存も同じ session で書けます。

```csharp
using (var shims = Shims.For<TimedService>().WithStatic(typeof(StaticClock)))
{
    shims.Static<string>(typeof(StaticClock), "GetName", typeof(int))
         .WithArguments(Eq(1))
         .Returns("fake-clock");

    var service = shims.CreateObject(typeof(TimedService).FullName);
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);   // → "fake-clock"
}
```

> **Create() の扱い:** rewrite 済みの型は isolated load context にロードされるため、
> `Create<UserService>()` のような concrete 型は安全にキャストできず `InvalidOperationException`
> を投げます（`CreateObject` + `Invoke` を案内）。`Create<T>()` が strongly-typed で成功するのは、
> load context をまたいで identity を共有する contract `IShimCreatable` を実装したクラスを
> `Create<IShimCreatable>()` で生成した場合だけです。詳細は
> [`docs/shims-experimental-quickstart.md`](docs/shims-experimental-quickstart.md) を参照してください。

### 低レベル API（new SomeClass() の差し替え）

`Shims` facade が内部で使う低レベル API です。細かい制御が必要なときに使います。

```csharp
using MiniMockito.Shims.Experimental;

[TestClass]
[DoNotParallelize]
public class RepositoryShimLowLevelTests
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

### クロスアセンブリの new 差し替え（Phase 20）

リライト対象アセンブリ内で呼ばれている **外部アセンブリ型** の `new` も差し替えられます。  
たとえば `TargetApp.dll` 内で `new ExternalLib.ExternalDbContext()` を呼んでいる場合、
`TargetApp.dll` だけを rewrite し、`ExternalLib.ExternalDbContext` を `WithExternalTarget<T>()`
で登録します（`ExternalLib.dll` そのものは書き換えません）。

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget<ExternalDbContext>()                 // 外部型を登録
    .RewriteAssembly(typeof(UserService).Assembly.Location)) // TargetApp.dll を rewrite
{
    using (ShimContext.Create())
    {
        // 外部型の fake は「自分で作って RegisterShim する」のが第一推奨
        // （手書き subclass でも Mock.Class<ExternalDbContext>() でも可）
        var fake = new FakeExternalDbContext();
        harness.RegisterShim<ExternalDbContext>(fake);        // RegisterShim(Type, fake) も可

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

- 外部型の `TypeReference` / `AssemblyReference` は維持され、外部型は parent load context から共有されます。
- shim key は `Type.FullName`（+ assembly simple name）ベース。同一 FullName が複数アセンブリにあると曖昧です。
- 外部型に `CreateFake<T>()` は **未対応**（`NotSupportedException`）。手動 fake + `RegisterShim` を使います。
- `WithExternalTarget` に未登録の外部型 `newobj` は rewrite されず実コンストラクタのまま動きます。
- `DbContext` 系などコンストラクタ／`Dispose` に副作用がある型は、実生成に依存しない fake を用意してください。
- BCL static method（`DateTime.Now` 等）は引き続き対象外です。net8 / net48 両方で動作します。

#### 外部型をコンパイル時参照できない場合（Phase 21）

外部型を **コンパイル時参照したくない / できない** 場合は、assembly path と type full name の
文字列で指定できます。

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(externalAssemblyPath, "ExternalLib.ExternalDbContext") // path + FullName
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        harness.RegisterShim("ExternalLib.ExternalDbContext", fake);           // FullName で登録
        // harness.RegisterShim("ExternalLib.ExternalDbContext", "ExternalLib", fake); // FullName + asm 名

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

- `WithExternalTarget(string, string)` / `ResolveExternalType(string, string)` は指定 assembly から
  型を解決し、解決失敗時は `ShimExternalTargetException`（searched path・type full name・reason 付き）。
- `CreateFakeExternal(Type / string, args)` は public・non-sealed・non-abstract・parameterless ctor の型のみ
  対応（proxy は生成しない）。挙動を変えたい場合は手書き subclass / `Mock.Class<T>()` を `RegisterShim`。
- 診断は `harness.Diagnostics`（解決・登録・registry key・duplicate FullName risk）と
  `harness.LastRewriteResult.Diagnostics`（external newobj detected / rewritten / skipped + reason）で確認できます。

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

### .NET Framework 4.8 + x86（PlatformTarget=x86）の interface mock / spy

interface mock / spy は内部で proxy を生成します。生成方式は **ターゲットフレームワークごとに自動で選択**されます。

- **net8.0:** `System.Reflection.DispatchProxy` backend
- **net48:** `System.Runtime.Remoting.Proxies.RealProxy` backend

これは、`.NET Framework 4.8` + `PlatformTarget=x86` の組み合わせで `DispatchProxy` が内部の
`TypeBuilder.CreateTypeInfo()` に失敗し、次の例外が発生するためです。

```text
TypeLoadException: アクセスが拒否されました: 'MiniMockito.Proxy.MiniMockitoDispatchProxy'
```

net48 では `DispatchProxy` を使わず `RealProxy` fallback backend を使うため、**x86 でも interface
mock / spy / strict / async（`Task` / `Task<T>` / `ValueTask` / `ValueTask<T>`）が動作します**。

- **public API は完全に同一**です。`Mock.Of<T>()` / `Spy.Of<T>(real)` / `When` / `Verify` /
  matcher / captor / `InOrder` などのコードはそのまま動きます。backend 選択は内部的に行われ、
  利用者コードの変更は不要です。
- `PlatformTarget=x86` / `Prefer32Bit=true` の MSTest プロジェクトでも interface mock / spy が使えます。
- これは `MiniMockito.Shims.Experimental`（`new` / static 差し替え）とは **別問題・別レイヤー**です。
  Shims は assembly rewrite + ALC、本件は interface proxy backend の話で、互いに独立しています。

```xml
<!-- x86 で実行する net48 MSTest プロジェクトの例 -->
<PropertyGroup>
  <TargetFramework>net48</TargetFramework>
  <LangVersion>7.3</LangVersion>
  <PlatformTarget>x86</PlatformTarget>
  <Prefer32Bit>true</Prefer32Bit>
</PropertyGroup>
```

```csharp
// x86 でもこのコードはそのまま動作する（backend は自動で RealProxy が選択される）
var repo = Mock.Of<IUserRepository>();
When(() => repo.FindById(Any<int>())).ThenReturn("mocked");
Assert.AreEqual("mocked", repo.FindById(1));
Verify(() => repo.FindById(1), Times.Once());
```

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
    <PackageReference Include="MiniMockito.Net" Version="0.2.0-preview.7" />
    <!-- Shims を使う場合（実験的） -->
    <PackageReference Include="MiniMockito.Shims.Experimental" Version="0.1.0-alpha.8" />
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
