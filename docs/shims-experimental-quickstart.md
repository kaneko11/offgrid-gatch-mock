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
| high-level facade `Shims.For<T>()`（new / static をまとめて扱う） | ✅ 対応 (Phase 17) |
| `Create<IShimCreatable>()` による strongly-typed 生成 | ✅ 対応 (Phase 17) |
| `CreateObject` + `Invoke` fallback | ✅ 対応 (Phase 17) |
| cross-assembly `new ExternalType()` の差し替え（外部アセンブリ型の newobj） | ✅ 対応 (Phase 20) |
| 外部型を assembly path + type full name の文字列で指定（コンパイル時参照不要） | ✅ 対応 (Phase 21) |
| cross-assembly diagnostics（解決/登録/rewrite/skip/registry key/duplicate risk） | ✅ 対応 (Phase 21) |
| Easy API `Shims.ForAssembly(...).ReplaceNew(...)`（複数登録 / internal・external 混在 / last stub wins） | ✅ 対応 (Phase 23) |
| Inspection API（`GetValue<T>` / `GetCollection` / `ShimsObject` で rewritten object graph を path 検証） | ✅ 対応 (Phase 24) |
| インスタンスメソッド呼び出しの差し替え（`ReplaceMethod` / method shim・非 virtual / ジェネリック / 戻り値 interface 差し替え） | ✅ 対応 (Phase 25) |
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

### インストール（現行プレビュー版）

```xml
<!-- 本体（net8.0 / net48） -->
<PackageReference Include="MiniMockito.Net" Version="0.2.0-preview.7" />
<!-- 実験パッケージ（test-only・API は変わり得ます） -->
<PackageReference Include="MiniMockito.Shims.Experimental" Version="0.1.0-alpha.7" />
```

ローカル検証時は `dotnet pack -c Release -o artifacts` で nupkg を生成し、`artifacts` を NuGet ソースに
追加して参照してください（手順は README を参照）。

---

## 6. Easy Shims API — `ReplaceNew`（Phase 23・最推奨）

cross-assembly の `new` 差し替えは、`Shims.ForAssembly(...)` + `ReplaceNew(...)` が最短の書き方です。
`NewInterceptionHarness` / `ShimContext` / `WithExternalTarget` / `RegisterShim` を直接書く必要はありません。
これらの low-level API は後述の **6b / 6c（advanced）** に移しています（Easy API が内部で利用します）。

### 6.0 ReplaceNew の基本

```csharp
// 外部型を assembly path + type full name で指定（コンパイル時参照不要）
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    // → "fake-1"
}

// 外部型をコンパイル時参照できる場合
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew<ExternalDbContext>(fakeContext)) { ... }

// Type で指定する場合
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(typeof(ExternalDbContext), fakeContext)) { ... }
```

### 6.0a 複数 `ReplaceNew` / internal・external 混在

1つの `ShimsSession`（`Shims`）内で `ReplaceNew(...)` を何度でも登録でき、internal target と
external target を混在できます。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeDb)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalLogger", fakeLogger)
                        .ReplaceNew<InternalGreeter>(s => s.CreateFake<InternalGreeter>("g")))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "Run", 1);
}
```

### 6.0b 仕様メモ

- **rewrite 確定タイミング**: 初回 `CreateObject(...)` / `Create<T>()` / `Invoke<TResult>(...)` で確定。
  登録済みの `ReplaceNew(...)` はこのタイミングでまとめて反映されます。
- **確定後の追加**: rewrite 確定後に `ReplaceNew(...)` を追加すると `InvalidOperationException`
  （`rewrite already completed` / `target cannot be added after rewrite` / `create a new Shims session`）。
- **same target type に複数回 `ReplaceNew`**: **last stub wins**（既存 `ShimRuleRegistry` 準拠）。
- **引数条件で fake を分けたい**場合: `ReplaceNew` は catch-all なので、低レベルの
  `New<T>().WithArguments(...).Returns(...)`（セクション 6.2 / 6b）を使ってください。
- **internal target の fake**: 手作りインスタンスは rewrite 済み ALC の型 identity を持たないため
  差し替わりません。internal は `ReplaceNew<T>(s => s.CreateFake<T>(...))`（factory 形式）を使います。
- **`Create<T>()`**: load context / assembly identity 上 安全に cast できる場合のみ成功。
  失敗時は分かりやすい例外を出すので `CreateObject(...)` + `Invoke<TResult>(...)` を使ってください。
- **DbContext 系**: コンストラクタ／`Dispose` に副作用がある型は、実生成に依存しない手動 fake を
  `ReplaceNew(...)` に渡してください（`CreateFakeExternal` での自動生成は避ける）。
- **BCL static method**（`DateTime.Now` / `File.ReadAllText` 等）は未対応のままです。
- **`ShimContext` は不要**: session 内部で生成・破棄されます。`Dispose()`（= `using` 終了）で
  `ShimContext` / `NewInterceptionHarness` / rewritten assembly loader が cleanup されます。
- **diagnostics**: `shims.Diagnostics` / `shims.LastDispatchDiagnostics` / `shims.GetAlcDiagnostics()`。

### 6.0c Inspection API（Phase 24・object graph の検証）

`ForAssembly(...).ReplaceNew(...)` は target assembly を rewrite して別ロードするため、**rewritten
object の型 identity はテスト側の元の型と一致しない**ことがあります（`Create<T>()` で strongly typed に
戻せない、`ObservableCollection<T>` の `T` が rewritten type になる等）。この Phase の inspection API は、
**rewritten object を `object` のまま path で観察・検証**します（元の型へ無理に cast しません）。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(externalAssemblyPath, "ExternalLib.ExternalDbContext", fakeContext))
{
    var vm = shims.CreateObject("TargetApp.UserViewModel");
    shims.Invoke(vm, "Load");

    var count = shims.GetValue<int>(vm, "Items.Count");
    var firstName = shims.GetValue<string>(vm, "Items[0].Name");

    var items = shims.GetCollection(vm, "Items");
    Assert.AreEqual("fake-1", items[0].Get<string>("Name"));

    var name = shims.Inspect(vm).GetObject("SelectedUser").GetValue<string>("Name");
}
```

API:

| メソッド | 内容 |
|---------|------|
| `GetValue(object, path)` / `GetValue<T>(object, path)` | path 評価 + （型付き時）変換 |
| `GetProperty(object, name)` / `GetProperty<T>(object, name)` | 単一プロパティ/フィールド読み取り |
| `Inspect(object)` → `ShimsObject` | wrapper（`GetValue` / `Get<T>` / `GetObject` / `GetCollection`） |
| `GetCollection(object, path)` → `ShimsCollection` | `Count` / `this[int]` / `GetRawItem` / `ToList` / `IEnumerable<ShimsObject>` |

- **path 構文**: `Items`, `Items.Count`, `SelectedUser.Name`, `Items[0]`, `Items[0].Name`, `Rows[1].Cells[2].Text`。
- **Count**: public `Count` / `ICollection.Count` / `ICollection<T>.Count` / `IReadOnlyCollection<T>.Count` /
  最後の手段として `IEnumerable` の列挙数。
- **`GetValue<T>`**: assignable ならそのまま、primitive / enum / string / `decimal` / `DateTime` / nullable は
  `Convert.ChangeType` 等で変換、`T==object` は raw を返す。**rewritten 参照型を同名 original 型へ強制 cast しません**。
- **collection 対応**: array / `IList` / `IReadOnlyList<T>` / `ICollection<T>` / `ObservableCollection<T>`。
  `ObservableCollection<T>` は BCL collection として扱い、要素 `T` が rewritten type でも wrapper で検証できます。
- **例外**: path 途中 null・存在しないプロパティ・index 範囲外・変換不可は `ShimsInspectionException`
  （requested path / failed segment / runtime type / reason、識別不一致時は「different load context」ヒント）。
- **`Create<T>()` との関係**: 型 identity が一致する場合のみ使用。cross-assembly / rewritten シナリオでは
  `CreateObject(...)` + `Invoke(...)` + inspection API を基本にします。

### 6.0d インスタンスメソッド差し替え（`ReplaceMethod`・Phase 25）

`new` / static に続く第3の差し替え。**呼び出し側 IL を書き換える**ので、**非 virtual メソッドや
ジェネリックメソッドも差し替え**できます（subclass override 不可なメソッドが対象）。declaring 型の
アセンブリ（外部 DLL）は書き換えません。

```csharp
// 非 virtual メソッド
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "GetName",
                            (receiver, args) => "fake-" + args[0]))
{
    var svc = shims.CreateObject("TargetApp.GatewayUserService");
    var result = shims.Invoke<string>(svc, "Run", 1);   // 内部の gateway.GetName(1) → "fake-1"
}

// ジェネリックメソッド（戻り値を interface に差し替え）。
// gateway.Query<T>(sql).ToList() のように「即 IEnumerable<T> として消費」される call site が対象。
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "Query",
                            (receiver, args) => new List<GatewayItem> { new GatewayItem("fake-1") },
                            typeof(IEnumerable<>)))   // ← 戻り値の差し替え先 interface（open generic）
{
    var svc = shims.CreateObject("TargetApp.GatewayUserService");
    var rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRows");
}
```

- **virtual 不要**: call-site 書き換えなので非 virtual / 生成不可能戻り値型でも差し替え可能。
- **ジェネリック**: 型引数 1 個まで。call site の具象インスタンス化ごとに concrete ラッパーを生成。
- **戻り値型の差し替え**: 宣言戻り値が生成不可能な具象型（内部 ctor。EF の `DbRawSqlQuery<T>` 相当）でも、
  結果が直後に `IEnumerable<T>` 等の interface として消費されるなら、`returnSubstituteInterface` に
  open generic interface を指定して差し替え可能。安全に差し替えられない消費形（具象型のローカルに格納等）は skip + 診断。
- **no match フォールバック**: shim 未登録の call site は実メソッドを呼ぶ。
- **対象外**: BCL 宣言型メソッド（`DateTime.Now` 等）、`ref`/`out`/`params`、複数型引数、プロパティ/インデクサ。
- **EF 適用**: `context.Database.SqlQuery<T>(sql).ToList()` は、`Database`/`SqlQuery` を method target にして
  `returnSubstituteInterface = typeof(IEnumerable<>)` を指定すれば差し替え可能（生 SQL を実行せず canned データを返す）。

---

## 6a. 使い方（high-level facade）— Phase 17

`Shims` facade を使うと、`NewInterceptionHarness` / `ShimContext` / `RegisterShim` / reflection Invoke
を直接意識せずに `new` / user-defined static method の差し替えが書けます。
`Shims.For<TAnchor>()` の `TAnchor` は、差し替え対象の call site を含むアセンブリを決めるための型
（通常は差し替えたい `new` / static 呼び出しを行うサービス型）です。

> rewrite は `WithNew` / `WithStatic` の設定後、`New` / `Static` / `Create` / `CreateObject` /
> `Invoke` / `CreateFake` の **初回呼び出し時に確定**します。確定後に `WithNew` / `WithStatic` を
> 追加しようとすると `InvalidOperationException` を投げます。

### 6.0.1 new 差し替え

```csharp
using static MiniMockito.Shims.Experimental.ShimArg;

using (var shims = Shims.For<UserService>()
                        .WithNew<UserRepository>())
{
    // 差し替え後の値を返す fake は、rewrite 済み ALC 上のインスタンスを使う。
    var fakeRepo = shims.CreateFake<UserRepository>("fake");

    shims.New<UserRepository>()
         .WithArguments(Eq("prod"))     // 省略すると catch-all
         .Returns(fakeRepo);

    // 型 identity 問題を避けるため CreateObject + Invoke を使う（推奨）。
    var service = shims.CreateObject(typeof(UserService).FullName);
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    // → "fake-1"
}
```

### 6.0.2 user-defined static method 差し替え

```csharp
using (var shims = Shims.For<TimedService>()
                        .WithStatic(typeof(StaticClock)))
{
    shims.Static<string>(typeof(StaticClock), "GetName", typeof(int))
         .WithArguments(ShimArg.Eq(1))
         .Returns("fake-clock");

    var service = shims.CreateObject(typeof(TimedService).FullName);
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
    // → "fake-clock"

    // void static method は Callback / DoNothing / Throws が使える。
    shims.Static(typeof(StaticClock), "LogCall", typeof(string))
         .Callback(args => Console.WriteLine(args[0]));
}
```

### 6.0.3 new + static 共存

```csharp
using (var shims = Shims.For<UserService>()
                        .WithNew<UserRepository>()
                        .WithStatic(typeof(StaticClock)))
{
    var fakeRepo = shims.CreateFake<UserRepository>("fake");
    shims.New<UserRepository>().Returns(fakeRepo);

    shims.Static<string>(typeof(StaticClock), "GetName", typeof(int))
         .Returns("static-name");

    // newobj shim と user-defined static shim が同じ session 内で共存する。
}
```

### 6.0.4 Create() の扱いと CreateObject / Invoke fallback

rewrite 済みの型は **isolated load context**（net8: collectible ALC、net48:
`Assembly.Load(byte[])`）にロードされるため、rewrite 済みの concrete 型は default load context の
同名型へキャストできません。したがって:

- `Create<TConcrete>()`（例: `Create<UserService>()`）は **安全にキャストできない**ため、
  分かりやすい `InvalidOperationException` を投げ、`CreateObject` + `Invoke` の使用を案内します。
- `Create<T>()` が strongly-typed で成功するのは、**load context をまたいで identity を共有する
  contract**（= このアセンブリで宣言された interface、`IShimCreatable`）を指定した場合だけです。

```csharp
// ✅ 動くパターン: 共有 contract IShimCreatable を実装したサービス
//    （CreatableService : IShimCreatable）
using (var shims = Shims.For<CreatableService>().WithNew<UserRepository>())
{
    var fakeRepo = shims.CreateFake<UserRepository>("fake");
    shims.New<UserRepository>().Returns(fakeRepo);

    IShimCreatable service = shims.Create<IShimCreatable>();
    var result = service.Describe();   // 差し替えが効いた状態で呼べる
}

// ✅ 一般的なパターン（推奨）: CreateObject + Invoke
using (var shims = Shims.For<UserService>().WithNew<UserRepository>())
{
    var fakeRepo = shims.CreateFake<UserRepository>("fake");
    shims.New<UserRepository>().Returns(fakeRepo);

    var service = shims.CreateObject(typeof(UserService).FullName);
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);
}

// ❌ Create<UserService>() は InvalidOperationException を投げる:
//    "Create<T>() cannot safely return a strongly-typed instance for this type. ...
//     Use instead: var obj = shims.CreateObject(...); shims.Invoke<TResult>(obj, ...)."
```

### 6.0.5 net48 / C# 7.3 での high-level facade

API は net8.0 と完全に同じです。C# 7.3 では `using var` が使えないため `using` statement を使います。

```csharp
[TestClass]
[DoNotParallelize]
public sealed class Net48HighLevelTests
{
    [TestMethod]
    public void ParameterlessNew_IsShimmed()
    {
        using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
        {
            object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
            shims.New<Net48UserRepository>().Returns(fakeRepo);

            object service = shims.CreateObject(typeof(Net48UserService).FullName);
            string result = shims.Invoke<string>(service, "GetDisplayName", 1);

            Assert.AreEqual("fake-1", result);
        }
    }
}
```

---

## 6b. Advanced — low-level API（NewInterceptionHarness / ShimContext）

> 以下は `Shims` facade が内部で利用している低レベル API です。細かい制御が必要な場合に使います。
> 通常は上記 high-level facade（セクション 6）を推奨します。

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

## 6c. Advanced — cross-assembly new interception（Phase 20）

これまでの new 差し替えは「リライト対象アセンブリ自身に定義された型」の `newobj` が対象でした。
Phase 20 では、リライト対象アセンブリの中で呼ばれている **外部アセンブリ型** の `newobj` を
差し替えられます。

たとえば、`TargetApp.dll` 内で `new ExternalLib.ExternalDbContext()` を呼んでいる場合、
`TargetApp.dll` だけを rewrite し、`ExternalLib.ExternalDbContext` を `WithExternalTarget<T>()`
で登録できます（`ExternalLib.dll` そのものは書き換えません）。

```csharp
// ExternalLib.dll
namespace ExternalLib
{
    public class ExternalDbContext : IDisposable
    {
        public virtual string GetName(int id) => "real-" + id;
        public void Dispose() { }
    }
}

// TargetApp.dll（こちらだけ rewrite する）
namespace TargetApp
{
    public class UserService
    {
        public string GetDisplayName(int id)
        {
            using (var context = new ExternalLib.ExternalDbContext())
                return context.GetName(id);
        }
    }
}
```

### 6c.1 型をコンパイル時参照できる場合

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget<ExternalDbContext>()
    .RewriteAssembly(typeof(UserService).Assembly.Location))
{
    using (ShimContext.Create())
    {
        // 外部型の fake は「自分で作って RegisterShim する」のが第一推奨。
        // 手書きの subclass でも、Mock.Class<ExternalDbContext>() でもよい（GetName は virtual）。
        var fake = new FakeExternalDbContext();   // : ExternalDbContext, GetName を override
        harness.RegisterShim<ExternalDbContext>(fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
        // → fake が返した値
    }
}
```

### 6c.2 Type で指定する場合

```csharp
var externalType = typeof(ExternalDbContext);

using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(externalType)
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        harness.RegisterShim(externalType, fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

### 6c.3 仕組みと制約

- **rewrite**: 外部型の `newobj` も内部型と同じく `ShimDispatcher.New<T>()` 経由に置換します。
  外部型の `TypeReference` / `AssemblyReference` は `module.ImportReference` でそのまま維持され、
  rewritten assembly は引き続き外部アセンブリ（例 `ExternalLib`）を参照します。
- **型 identity の共有**: 外部 target に登録したアセンブリは isolated ALC ではなく
  **parent (default) ALC から共有**されます。これにより、テスト側で作った fake（default ALC の
  `ExternalDbContext` の subclass）が、rewritten code が期待する型と一致し、差し替えが成立します。
- **shim key**: 外部型のルックアップは runtime `Type` の完全一致ではなく **`Type.FullName`
  （+ assembly simple name）ベース**で照合します（`ShimDispatchDiagnostics.ResolvedByFullNameFallback`
  が `true` になります）。
- **FullName 重複の制約**: 同一 `FullName` の外部型が異なるアセンブリに複数存在する場合、
  FullName ベース照合は曖昧になり得ます（`ShimDispatchDiagnostics.DuplicateFullNameRisk`）。
  実運用ではこのような重複を避けてください。
- **fake は手動が第一推奨**: 外部型については `CreateFake<T>()` は **未対応**で、
  分かりやすい `NotSupportedException` を投げます。手書きの subclass か `Mock.Class<T>()` で fake を
  作り、`RegisterShim<T>(fake)` / `RegisterShim(Type, fake)` してください。
- **未登録の外部型**: `WithExternalTarget` に登録していない外部型の `newobj` は rewrite されず、
  実コンストラクタのまま動きます。
- **DbContext 系の注意**: EF の `DbContext` など、コンストラクタや `Dispose` に副作用がある型は、
  実インスタンス生成に依存しない fake（必要メソッドだけ override した subclass）を用意してください。
  `CreateFake<T>()` で安全に生成することはできません。
- **BCL static は対象外**: `DateTime.Now` / `File.ReadAllText` などの BCL static method は
  Phase 20 でも未対応のままです。
- **`[DoNotParallelize]` 必須**: 他の shim と同様、プロセス共有状態を使うため並列実行は禁止です。

### 6c.4 型をコンパイル時参照できない場合（assembly path + type full name）— Phase 21

テストプロジェクトが外部アセンブリ型を **コンパイル時参照したくない / できない** 場合は、
assembly path と type full name の文字列で外部 target を指定できます。

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(
        assemblyPath: externalAssemblyPath,            // 例: "...\\ExternalLib.dll"
        typeFullName: "ExternalLib.ExternalDbContext")
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        // 型を参照できない場合、fake は手書き subclass / Mock.Class などで用意し、FullName で登録する
        harness.RegisterShim("ExternalLib.ExternalDbContext", fake);

        // FullName + assembly simple name で登録すると重複検出に使われる
        // harness.RegisterShim("ExternalLib.ExternalDbContext", "ExternalLib", fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

関連 API:

| API | 用途 |
|-----|------|
| `WithExternalTarget(string assemblyPath, string typeFullName)` | path + FullName で外部 target を解決・登録 |
| `ResolveExternalType(string assemblyPath, string typeFullName)` | 外部型を `Type` として解決（解決失敗時は `ShimExternalTargetException`） |
| `RegisterShim(string typeFullName, object fake)` | FullName ベースで fake を登録 |
| `RegisterShim(string typeFullName, string assemblySimpleName, object fake)` | FullName + assembly simple name で登録 |
| `CreateFakeExternal(Type targetType, params object[] args)` | 外部型の素のインスタンスを生成（後述の制約あり） |
| `CreateFakeExternal(string typeFullName, params object[] args)` | 登録済み外部型 FullName から素のインスタンスを生成 |

- **解決失敗時の例外**: assembly path が存在しない / typeFullName が見つからない場合は、
  `ShimExternalTargetException` を投げます。メッセージには searched path・candidate assembly・
  type full name・reason（`ExternalAssemblyFileNotFound` / `ExternalTypeNotFound` 等）を含みます。
- **`CreateFakeExternal` の対応範囲**: `public` かつ `non-sealed`・`non-abstract` な class のみ。
  引数なしの場合は public parameterless ctor が必須です。**proxy / 挙動 override は生成しません**
  （素のインスタンスを返すだけ）。対応外の型では `NotSupportedException`
  （reason: `SealedTypeNotSupported` / `PublicParameterlessConstructorNotFound` 等）を投げ、
  手動 fake を `RegisterShim(...)` するよう案内します。
- **挙動を差し替えたい場合**: `CreateFakeExternal` は素のインスタンスを返すだけなので、メソッドの
  戻り値を変えたいときは手書き subclass か `Mock.Class<T>()` を `RegisterShim` してください。

### 6c.5 diagnostics の読み方（Phase 21）

cross-assembly の失敗理由を追えるよう、2 系統の diagnostics を用意しています。

- **harness レベル**: `harness.Diagnostics`（`IReadOnlyList<string>`）
  - `External assembly path: ...`
  - `External type full name: ...`
  - `Candidate assembly loaded: ...`
  - `Type resolution: success / failure — ...`
  - `External target registered: {FullName} (assembly {asm})`
  - `Target assembly being rewritten: ...`
  - `Registry key used: {FullName} | {asm}`
  - `Duplicate FullName risk: {FullName} registered for assemblies [...]`
  - `External type fake creation supported / unsupported: ...`
- **rewrite レベル**: `harness.LastRewriteResult.Diagnostics`
  - `External newobj detected: ...`
  - `External newobj rewritten: ... assembly reference '...' preserved.`
  - `External newobj skipped: ... Skipped reason: ...`
- **dispatch レベル**: `ShimContext.LastDispatchDiagnostics`
  - `ResolvedByFullNameFallback` / `DuplicateFullNameRisk`、`Format()` で人間可読化

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(externalAssemblyPath, "ExternalLib.ExternalDbContext")
    .RewriteAssembly(targetAssemblyPath);

// 解決・登録・rewrite の経緯を確認
foreach (var line in harness.Diagnostics) Console.WriteLine(line);
foreach (var line in harness.LastRewriteResult!.Diagnostics) Console.WriteLine(line);
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

- high-level facade `Shims.Create<TConcrete>()` は型 identity 問題で安全に返せないため例外を投げる
  → `CreateObject(typeFullName)` + `Invoke(...)` を使う（`Create<T>()` が成功するのは共有 contract
  `IShimCreatable` を指定した場合のみ）
- BCL static method (`DateTime.Now` 等) は差し替え不可
- cross-assembly 外部型は FullName ベース照合（同一 FullName が複数アセンブリにあると曖昧 → `Duplicate FullName risk` diagnostics）
- cross-assembly 外部型に `CreateFake<T>()` は未対応（手動 fake + `RegisterShim` を使う）
- `CreateFakeExternal(...)` は public・non-sealed・non-abstract・parameterless ctor のみ対応（proxy 生成はしない）
- 外部型の挙動差し替えは手書き subclass / `Mock.Class<T>()` を `RegisterShim` する（`CreateFakeExternal` は素のインスタンスのみ）
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
