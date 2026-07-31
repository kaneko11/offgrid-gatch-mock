# MiniMockito.Shims.Experimental Phase 20 — Cross-Assembly new Interception PoC

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および MiniMockito.Shims.Experimental 関連の実装・テストを読んでください。

MiniMockito.Shims.Experimental Phase 20 として、クロスアセンブリ new 差し替え対応の最小 PoC を実装してください。

## 目的

リライト対象アセンブリ内で呼ばれている、外部アセンブリ型の `newobj` を shim に差し替えられるようにしてください。

実案件固有の型名・DLL名はハードコードしないでください。

以下のような名前は、実装・テスト・API名に直接使わないでください。

- CommonModels
- SP_USER_DATEntities
- 車両販売_買取下取一覧

これらは docs の「実案件適用例」としてのみ扱ってください。

## 背景

現在は、リライト対象アセンブリ自身に定義されている型の `newobj` 差し替えは動作しています。

しかし実アプリでは、以下のような構造があります。

```csharp
// TargetApp.dll 側
using ExternalLib;

namespace TargetApp
{
    public class UserService
    {
        public string GetDisplayName(int id)
        {
            using (var context = new ExternalDbContext())
            {
                return context.GetName(id);
            }
        }
    }
}
```

```csharp
// ExternalLib.dll 側
namespace ExternalLib
{
    public class ExternalDbContext
    {
        public virtual string GetName(int id)
        {
            return "real-" + id;
        }
    }
}
```

この場合、リライト対象は `TargetApp.dll` ですが、`newobj` の declaring type は `ExternalLib.dll` の型です。

Phase 20 では、この cross-assembly newobj を差し替え可能にしてください。

## この Phase の対象

実装対象:

- `WithExternalTarget<TExternal>()`
- `WithExternalTarget(Type externalType)`
- 外部アセンブリ型 `newobj` の検出
- 外部アセンブリ型 `newobj` の rewrite
- 外部型の `TypeReference` / `AssemblyReference` 維持
- `RegisterShim<TExternal>(fake)` の外部型対応
- `RegisterShim(Type externalType, object fake)` の追加
- 外部型に対する registry lookup
- FullName ベースまたは FullName + AssemblySimpleName ベースの shim key
- net8 tests
- net48 tests
- docs 更新

## この Phase で優先すること

優先順位は以下です。

1. 外部アセンブリ型の `newobj` を検出できること
2. 外部アセンブリ型の `newobj` を既存 dispatcher / wrapper 経由に rewrite できること
3. `RegisterShim<TExternal>(fake)` した fake が `new ExternalType()` の代わりに返ること
4. no match 時は元 constructor fallback になること
5. internal target の既存 newobj rewrite が壊れないこと

`CreateFake<T>()` の外部型完全対応はこの Phase の主目的ではありません。まずは、ユーザーが自分で用意した fake instance を `RegisterShim<TExternal>(fake)` できることを優先してください。

## API 例

型をコンパイル時参照できる場合:

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget<ExternalDbContext>()
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        var fake = Mock.Class<ExternalDbContext>();

        Mock.When(() => fake.GetName(1))
            .ThenReturn("fake-1");

        harness.RegisterShim<ExternalDbContext>(fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);

        Assert.AreEqual("fake-1", result);
    }
}
```

Type で指定する場合:

```csharp
var externalType = typeof(ExternalDbContext);

using (var harness = NewInterceptionHarness.Create()
    .WithExternalTarget(externalType)
    .RewriteAssembly(targetAssemblyPath))
{
    using (ShimContext.Create())
    {
        var fake = Mock.Class<ExternalDbContext>();

        harness.RegisterShim(externalType, fake);

        var service = harness.CreateObject("TargetApp.UserService");
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);

        Assert.AreEqual("fake-1", result);
    }
}
```

## テスト用プロジェクト

実案件 DLL ではなく、MiniMockito リポジトリ内にテスト用の外部アセンブリを作ってください。

候補:

```text
tests/MiniMockito.Shims.Experimental.ExternalLib/
  ExternalDbContext.cs

tests/MiniMockito.Shims.Experimental.CrossAssemblySample/
  CrossAssemblyUserService.cs
```

`ExternalLib`:

```csharp
namespace ExternalLib
{
    public class ExternalDbContext : IDisposable
    {
        public virtual string GetName(int id)
        {
            return "real-" + id;
        }

        public void Dispose()
        {
        }
    }
}
```

`CrossAssemblySample`:

```csharp
using ExternalLib;

namespace CrossAssemblySample
{
    public class CrossAssemblyUserService
    {
        public string GetDisplayName(int id)
        {
            using (var context = new ExternalDbContext())
            {
                return context.GetName(id);
            }
        }
    }
}
```

## 実装詳細

### 1. 外部 target 登録

`NewInterceptionHarness` に以下を追加してください。

```csharp
public NewInterceptionHarness WithExternalTarget<TExternal>();
public NewInterceptionHarness WithExternalTarget(Type externalType);
```

内部では internal target と external target を区別してください。

候補:

```csharp
internal sealed class ExternalNewTarget
{
    public Type OriginalType { get; }
    public string TypeFullName { get; }
    public string AssemblySimpleName { get; }
}
```

### 2. Cecil scanner / rewriter

リライト対象アセンブリ内で以下を検出してください。

```il
newobj instance void [ExternalLib]ExternalLib.ExternalDbContext::.ctor()
```

`WithExternalTarget` に登録済みの型なら、既存 new shim と同じ方式で dispatcher / wrapper へ置換してください。

重要:

- 外部型の `TypeReference` を壊さない
- 外部型の `AssemblyReference` を維持する
- `module.ImportReference(externalType)` を適切に使う
- 外部型をリライト対象アセンブリ内の型として扱わない
- 既存 internal newobj rewrite を壊さない

### 3. Shim key

外部型は load context / assembly identity 差異により完全一致できない場合があります。

Phase 20 では、外部型については `Type.FullName` ベース、または `FullName + AssemblySimpleName` ベースで照合できるようにしてください。

最低条件:

- `RegisterShim<TExternal>(fake)` が外部型 newobj から見つかる
- `RegisterShim(Type externalType, object fake)` が外部型 newobj から見つかる
- 同じ FullName の型が複数ある場合の制約を diagnostics / docs に明記する

### 4. CreateFake<T>() の扱い

この Phase では、外部型について `CreateFake<T>()` を無理に完全対応しないでください。

対応する場合も以下に限定してください。

- public
- non-sealed
- parameterless ctor あり
- constructor 副作用がない前提

`DbContext` 特化処理は入れないでください。

`CreateFake<T>()` が外部型で安全に作れない場合は、分かりやすい `NotSupportedException` を出し、手動 fake を `RegisterShim<T>(fake)` するよう案内してください。

## 追加テスト

MSTest で以下を追加してください。

1. `WithExternalTarget<T>()` で外部型を登録できる
2. `WithExternalTarget(Type)` で外部型を登録できる
3. リライト対象アセンブリ内の外部型 `newobj` を検出できる
4. 外部型 `newobj` が shim dispatcher / wrapper に置換される
5. `RegisterShim<T>(fake)` 後、`new ExternalType()` が fake に差し替わる
6. `RegisterShim(Type, fake)` 後、`new ExternalType()` が fake に差し替わる
7. no match の場合は元 constructor fallback になる
8. external target に登録していない外部型 newobj は rewrite されない
9. internal target の newobj rewrite が壊れていない
10. 外部型の assembly reference が rewritten assembly に維持される
11. net8 tests が通る
12. net48 tests が通る
13. 既存 shims tests が壊れていない
14. 既存 MiniMockito 本体 tests が壊れていない

## diagnostics

以下を diagnostics に含めてください。

- external target registered
- external newobj detected
- external newobj rewritten
- external target not registered
- external type reference import failed
- assembly reference preserved / added
- shim lookup by FullName fallback used
- duplicate FullName risk
- unsupported external target reason

## docs

以下を更新してください。

- docs/shims-experimental-quickstart.md
- docs/shims-net48-compatibility-design.md
- docs/shims-experimental-phase14-milestone.md
- README.md の Shims.Experimental セクション

追記内容:

- cross-assembly new interception の使い方
- `WithExternalTarget<T>()`
- `WithExternalTarget(Type)`
- `RegisterShim<T>(fake)`
- `RegisterShim(Type, fake)`
- 外部型 fake は手動で作って `RegisterShim` するのが第一推奨であること
- `CreateFake<T>()` の外部型制約
- DbContext 系の注意点
- FullName match の制約
- `[DoNotParallelize]` 必須
- BCL static method は未対応のまま

## 実案件名の扱い

docs の参考例としてのみ、以下のような説明を追加して構いません。

```text
たとえば、TargetApp.dll 内で new ExternalLib.ExternalDbContext() を呼んでいる場合、
TargetApp.dll だけを rewrite し、ExternalLib.ExternalDbContext を WithExternalTarget<T>() で登録できます。
```

実装・テスト・API に、実案件固有の DLL 名や型名をハードコードしないでください。

## 対象外

この Phase では以下を実装しないでください。

- BCL static method mocking
- DateTime.Now mocking
- File.ReadAllText mocking
- sealed external class mocking
- non-virtual method override
- DbContext 専用の特殊対応
- production assembly in-place rewrite
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Microsoft Fakes Shim 完全互換
- 外部アセンブリそのものの rewrite

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
- external newobj rewrite の方式
- Register / Resolve の型キー方式
- CreateFake<T>() の外部型対応範囲
- 追加したテスト
- 実案件に適用する場合の注意点
- dotnet build / dotnet test の結果
