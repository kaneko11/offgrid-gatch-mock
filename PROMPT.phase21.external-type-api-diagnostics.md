# MiniMockito.Shims.Experimental Phase 21 — External Type API / String-Based Target / Diagnostics Hardening

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および Phase 20 の実装・テストを読んでください。

MiniMockito.Shims.Experimental Phase 21 として、cross-assembly new interception の API と diagnostics を強化してください。

## 目的

Phase 20 で実装した `WithExternalTarget<T>()` / `WithExternalTarget(Type)` を発展させ、テストプロジェクトが外部アセンブリ型をコンパイル時参照できない場合にも使いやすくしてください。

また、cross-assembly new interception の失敗理由を diagnostics で追いやすくしてください。

## 背景

実アプリでは、テスト対象アセンブリと外部依存アセンブリが別々に存在し、テストプロジェクトから外部型を直接参照したくない場合があります。

その場合、以下のように指定できる API が必要です。

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(
        assemblyPath: externalAssemblyPath,
        typeFullName: "ExternalLib.ExternalDbContext")
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        var fake = harness.CreateFakeExternal("ExternalLib.ExternalDbContext");

        harness.RegisterShim("ExternalLib.ExternalDbContext", fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
    }
}
```

## 実装対象

追加 API 候補:

```csharp
public NewInterceptionHarness WithExternalTarget(string assemblyPath, string typeFullName);

public Type ResolveExternalType(string assemblyPath, string typeFullName);

public object CreateFakeExternal(Type targetType, params object[] args);
public object CreateFakeExternal(string typeFullName, params object[] args);

public void RegisterShim(string typeFullName, object fake);
public void RegisterShim(string typeFullName, string assemblySimpleName, object fake);
```

high-level scenario API がある場合は、以下も検討してください。

```csharp
Shims.ForAssembly(targetAssemblyPath)
     .WithExternalNew(externalAssemblyPath, "ExternalLib.ExternalDbContext");
```

ただし、この Phase でも既存 low-level API を壊さないでください。

## 仕様

- `WithExternalTarget(string assemblyPath, string typeFullName)` は指定 assembly から type を解決する
- 型解決に失敗した場合は、候補 assembly / searched path / type name を含む分かりやすい例外を出す
- `ResolveExternalType(...)` は public API または internal helper として実装してよい
- `RegisterShim(string typeFullName, object fake)` は FullName ベースで登録する
- `RegisterShim(string typeFullName, string assemblySimpleName, object fake)` は FullName + AssemblySimpleName ベースで登録する
- duplicate FullName risk がある場合は diagnostics に出す
- `CreateFakeExternal(...)` は public / non-sealed / parameterless ctor のみサポートする
- unsafe な外部型 proxy 生成はしない
- DbContext 専用処理は入れない

## diagnostics 強化

以下を diagnostics に出せるようにしてください。

- external assembly path
- external type full name
- type resolution success / failure
- candidate assembly loaded
- target assembly being rewritten
- external target registered
- external newobj detected
- external newobj rewritten
- external newobj skipped
- skipped reason
- registry key used
- FullName fallback used
- duplicate FullName risk
- assembly reference preserved
- external type fake creation supported / unsupported
- fallback to original constructor

diagnostics はテストで検証可能にしてください。

## テスト

以下を追加してください。

1. `WithExternalTarget(string assemblyPath, string typeFullName)` で外部型を登録できる
2. 存在しない assembly path の場合、分かりやすい例外になる
3. 存在しない typeFullName の場合、分かりやすい例外になる
4. `RegisterShim(string typeFullName, fake)` で外部 newobj が差し替わる
5. `RegisterShim(string typeFullName, assemblySimpleName, fake)` で外部 newobj が差し替わる
6. `CreateFakeExternal(Type)` が対応型で成功する
7. `CreateFakeExternal(string)` が対応型で成功する
8. sealed 型では `CreateFakeExternal` が NotSupportedException になる
9. parameterless ctor がない型では `CreateFakeExternal` が NotSupportedException になる
10. diagnostics に external target registered が出る
11. diagnostics に external newobj rewritten が出る
12. diagnostics に skipped reason が出る
13. diagnostics に registry key が出る
14. net8 tests が通る
15. net48 tests が通る
16. 既存 shims tests が壊れていない

## docs

以下を更新してください。

- docs/shims-experimental-quickstart.md
- docs/shims-net48-compatibility-design.md
- docs/shims-experimental-phase14-milestone.md
- README.md

追記内容:

- 型を直接参照できる場合の `WithExternalTarget<T>()`
- `Type` で指定する場合の `WithExternalTarget(Type)`
- assembly path + type full name で指定する場合の `WithExternalTarget(string, string)`
- `RegisterShim(string, object)`
- `CreateFakeExternal(...)`
- diagnostics の読み方
- FullName match のリスク
- DbContext 系では手動 fake を推奨すること
- BCL static method は未対応のまま

## 対象外

この Phase では以下を実装しないでください。

- BCL static method mocking
- DateTime.Now mocking
- File.ReadAllText mocking
- sealed external class mocking
- DbContext 専用の特殊対応
- external assembly 自体の rewrite
- production assembly in-place rewrite
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Microsoft Fakes Shim 完全互換

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

最後の報告は日本語でお願いします。

報告には以下を含めてください。

- 追加した API
- string-based external target 指定の方式
- diagnostics の追加内容
- `CreateFakeExternal` の対応範囲
- 追加したテスト
- dotnet build / dotnet test の結果
