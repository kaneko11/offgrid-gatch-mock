# MiniMockito.Shims.Experimental Phase 23 — Easy Shims API / ReplaceNew Facade

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および Phase 20 / Phase 21 の実装・テストを読んでください。

MiniMockito.Shims.Experimental Phase 23 として、cross-assembly new interception を簡単に使える Easy Shims API を追加してください。

## 目的

この Phase の目的は、新しい interception 機能を追加することではありません。

既存の以下の low-level API を利用者が直接意識しなくても使える high-level facade を追加してください。

- NewInterceptionHarness
- ShimContext
- WithExternalTarget
- WithTarget
- RegisterShim
- CreateObject
- Invoke

特に、Phase 20 / Phase 21 で追加した cross-assembly new interception を、実案件で短く書けるようにすることを目的とします。

既存の low-level API は壊さないでください。
public API の破壊的変更は行わないでください。

## 目標 API

### 外部型を assembly path + type full name で指定する場合

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalDbContext",
                            fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");

    var result = shims.Invoke<string>(
        service,
        "GetDisplayName",
        1);

    Assert.AreEqual("fake-1", result);
}
```

### 外部型をコンパイル時参照できる場合

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew<ExternalDbContext>(fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");

    var result = shims.Invoke<string>(
        service,
        "GetDisplayName",
        1);

    Assert.AreEqual("fake-1", result);
}
```

### Type で指定する場合

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(typeof(ExternalDbContext), fakeContext))
{
    var service = shims.CreateObject("TargetApp.UserService");

    var result = shims.Invoke<string>(
        service,
        "GetDisplayName",
        1);

    Assert.AreEqual("fake-1", result);
}
```

### 1つのテストで複数の new を差し替える場合

1つの `ShimsSession` 内で `ReplaceNew(...)` を複数回呼べるようにしてください。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalDbContext",
                            fakeDb)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalLogger",
                            fakeLogger)
                        .ReplaceNew<UserRepository>(fakeRepository))
{
    var service = shims.CreateObject("TargetApp.UserService");

    var result = shims.Invoke<string>(
        service,
        "Run",
        1);

    Assert.AreEqual("fake-result", result);
}
```

### internal target の new 差し替えも同じ書き方に寄せる

```csharp
using (var shims = Shims.For<UserService>()
                        .ReplaceNew<UserRepository>(fakeRepository))
{
    var service = shims.Create<UserService>();

    Assert.AreEqual("fake-1", service.GetDisplayName(1));
}
```

ただし `Create<T>()` は load context / assembly identity の都合で安全に cast できる場合のみ成功させてください。
安全でない場合は、分かりやすい例外を出し、`CreateObject(...)` と `Invoke<TResult>(...)` を案内してください。

## 実装対象

- `Shims.ForAssembly(string targetAssemblyPath)`
- 既存 `Shims.For<TSut>()` がある場合は互換維持
- `ShimsSession` / `ShimScenario` の拡張
- `ReplaceNew<T>(T fake)`
- `ReplaceNew(Type targetType, object fake)`
- `ReplaceNew(string externalAssemblyPath, string typeFullName, object fake)`
- 安全に実装できるなら `ReplaceNew(string typeFullName, object fake)`
- 条件付き replacement 用の builder API が既にある場合は `ReplaceNew<T>()` からも利用可能にする
- `CreateObject(string typeFullName)`
- `Create<T>()`
- `Invoke<TResult>(object instance, string methodName, params object[] args)`
- `Dispose` cleanup
- diagnostics forwarding

## 仕様

### Shims.ForAssembly

`Shims.ForAssembly(string targetAssemblyPath)` は、指定された target assembly を rewrite 対象として保持する high-level session を作成してください。

内部では `NewInterceptionHarness` を使って構いません。

### ReplaceNew<T>(T fake)

`ReplaceNew<T>(fake)` は以下を内部で行ってください。

- target type が rewrite 対象 assembly 内の型なら `WithTarget<T>()` 相当
- target type が外部アセンブリ型なら `WithExternalTarget<T>()` 相当
- `RegisterShim<T>(fake)` 相当
- 初回 `CreateObject` / `Create<T>` / `Invoke` 時に rewrite を確定

internal / external の判定が困難な場合は、まず `WithExternalTarget<T>()` として扱っても構いません。ただし既存 internal newobj rewrite が壊れないようにしてください。

### ReplaceNew(Type targetType, object fake)

`ReplaceNew(Type targetType, object fake)` は以下を内部で行ってください。

- `WithExternalTarget(Type)` または `WithTarget(Type)` 相当
- `RegisterShim(Type, fake)` 相当
- target type が未対応の場合は分かりやすい例外

### ReplaceNew(string externalAssemblyPath, string typeFullName, object fake)

`ReplaceNew(string externalAssemblyPath, string typeFullName, object fake)` は以下を内部で行ってください。

- `WithExternalTarget(externalAssemblyPath, typeFullName)` 相当
- `RegisterShim(typeFullName, fake)` 相当
- 型解決失敗時は `ShimExternalTargetException` などの既存例外を活用

### 複数 replacement の仕様

1つの `ShimsSession` 内で `ReplaceNew(...)` を複数回呼べるようにしてください。

要件:

- rewrite 確定前であれば `ReplaceNew(...)` は何度でも追加可能にする
- external target と internal target を同じ session 内で混在できるようにする
- 複数の external assembly / typeFullName を同じ session 内で登録できるようにする
- 初回 `CreateObject(...)` / `Create<T>()` / `Invoke<TResult>(...)` 時に、登録済みの replacement をまとめて rewrite 対象に反映する
- rewrite 確定後に `ReplaceNew(...)` を追加しようとした場合は、分かりやすい例外を出す
- 同じ target type に複数回 `ReplaceNew(type, fake)` した場合は、既存 `ShimRuleRegistry` の仕様に合わせて last stub wins とする
- 同じ target type に対して引数条件を分けたい場合は、`ReplaceNew<T>().WithArguments(...).Returns(...)` が実装可能なら対応する
- 条件付き builder API が Phase 23 で重すぎる場合は、既存 `New<T>().WithArguments(...).Returns(...)` を使う方針を docs に明記する

想定例:

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalDbContext",
                            fakeDb)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalLogger",
                            fakeLogger)
                        .ReplaceNew<UserRepository>(fakeRepository))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "Run", 1);
}
```

同じ型で last stub wins の想定:

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew<ExternalDbContext>(firstFake)
                        .ReplaceNew<ExternalDbContext>(lastFake))
{
    var service = shims.CreateObject("TargetApp.UserService");
    var result = shims.Invoke<string>(service, "GetDisplayName", 1);

    // lastFake が使われる
}
```

### rewrite 確定タイミング

rewrite は以下の初回実行時に確定して構いません。

- `CreateObject(...)`
- `Create<T>()`
- `Invoke<TResult>(...)`

rewrite 確定後に `ReplaceNew` / `WithExternalTarget` / `WithTarget` 相当の target 追加を行おうとした場合は、分かりやすい例外を出してください。

例外メッセージには以下を含めてください。

- rewrite already completed
- target cannot be added after rewrite
- create a new Shims session

### ShimContext 管理

`ShimContext` は `ShimsSession` 内部で管理してください。

利用者は以下を書かなくてよいようにしてください。

```csharp
using (ShimContext.Create())
{
}
```

`Dispose()` で以下を cleanup してください。

- `ShimContext`
- `NewInterceptionHarness`
- rewritten assembly loader / ALC / temp resources
- diagnostics snapshot if needed

### diagnostics forwarding

`ShimsSession` から diagnostics を取得できるようにしてください。

候補:

```csharp
public IReadOnlyList<string> Diagnostics { get; }
public ShimDispatchDiagnostics LastDispatchDiagnostics { get; }
public ShimAlcDiagnostics GetAlcDiagnostics();
```

既存 diagnostics を壊さず、high-level API 経由でも参照できるようにしてください。

## C# 7.3 / net48 制約

net48 tests と docs の sample では以下を守ってください。

- `using var` は使わない
- `using (...) { }` を使う
- nullable reference syntax は使わない
- target-typed new は使わない
- collection expression は使わない
- record / init / file-scoped namespace は使わない
- switch expression は使わない
- async streams は使わない

## MSTest

以下を追加または更新してください。

1. `ReplaceNew<T>(fake)` で external newobj が差し替わる
2. `ReplaceNew(Type, fake)` で external newobj が差し替わる
3. `ReplaceNew(string assemblyPath, string typeFullName, fake)` で external newobj が差し替わる
4. 1つの `ShimsSession` で external target を2つ `ReplaceNew` できる
5. 1つの `ShimsSession` で internal target と external target を混在して `ReplaceNew` できる
6. 同じ target type に2回 `ReplaceNew` した場合 last stub wins になる
7. rewrite 確定前は複数 `ReplaceNew` できる
8. rewrite 確定後は `ReplaceNew` 追加で分かりやすい例外になる
9. `CreateObject + Invoke` で実行できる
10. no match fallback が壊れていない
11. internal target の newobj rewrite が壊れていない
12. internal target に対して `ReplaceNew<T>(fake)` が使える
13. net48 / C# 7.3 でも同じ Easy API が使える
14. diagnostics が取得できる
15. `ShimContext` を利用者が直接作らなくても動く
16. `Dispose` 後に cleanup される
17. 既存 low-level API の tests が壊れていない
18. 既存 MiniMockito 本体 tests が壊れていない

## docs

以下を更新してください。

- `docs/shims-experimental-quickstart.md`
- `docs/shims-net48-compatibility-design.md`
- `docs/shims-experimental-phase14-milestone.md`
- `README.md` の Shims.Experimental セクション

docs の方針:

- Easy API を最初に紹介する
- low-level `NewInterceptionHarness` API は advanced section に移す
- cross-assembly new interception の推奨書き方を `ReplaceNew(...)` にする
- 1つのテストで複数 `ReplaceNew(...)` を登録する例を追加する
- same target type に複数回 `ReplaceNew` した場合は last stub wins であることを明記する
- 引数条件で fake を分けたい場合の推奨方法を明記する
- net48 sample は `using` statement 形式にする
- BCL static method は未対応と明記する
- DbContext 系では手動 fake を `ReplaceNew` / `RegisterShim` に渡す方針を明記する
- `Create<T>()` が使えない場合は `CreateObject(...)` + `Invoke<TResult>(...)` を使うことを明記する

## この Phase で実装しないもの

- BCL static method mocking
- DateTime.Now mocking
- File.ReadAllText mocking
- sealed external class mocking
- DbContext 専用処理
- external assembly 自体の rewrite
- production assembly in-place rewrite
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Microsoft Fakes Shim 完全互換
- expression-based API
- public API の破壊的変更

## ビルド・テスト

最後に以下を実行してください。

```powershell
dotnet build
dotnet test
```

可能なら net48 project 単体も実行してください。

```powershell
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/MiniMockito.Shims.Experimental.Net48Tests.csproj
```

失敗した場合は修正してください。

## 報告

最後の報告は日本語でお願いします。

報告には以下を含めてください。

- 追加した Easy API
- `ReplaceNew(...)` の内部動作
- 複数 replacement の対応内容
- same target type の last stub wins の扱い
- rewrite 確定タイミング
- `ShimContext` 管理方式
- diagnostics forwarding の内容
- 追加したテスト
- docs 更新内容
- dotnet build / dotnet test の結果
