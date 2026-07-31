# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 1: new interception 設計

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md を読んでください。

### 目的

`new SomeClass()` を差し替えるための実装設計を行います。

この Phase では、まだ本格実装はしないでください。  
direct new interception を実現するための最小 PoC 方針、API、実装方式、リスク、テスト戦略を明確にしてください。

### 実現したい最終イメージ

```csharp
using (ShimContext.Create())
{
    var fakeRepository = Mock.Class<UserRepository>();

    Shim.New<UserRepository>()
        .Returns(fakeRepository);

    var service = new UserService();

    var result = service.GetDisplayName(1);
}
```

対象コード例:

```csharp
public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();
        return repository.GetName(id);
    }
}
```

この `new UserRepository()` を test scope 内で fake に差し替えたいです。

### 設計すること

以下を設計してください。

1. 実装方式の比較
   - build-time weaving / test output assembly rewrite
   - source rewriting
   - runtime IL rewrite
   - CLR Profiling API
   - detour / method patching

2. 最小 PoC のスコープ
   - user-defined class
   - public class
   - parameterless constructor
   - non-generic class
   - user assembly 内の単純な `newobj`
   - BCL type は対象外
   - constructor arguments は対象外
   - static method mocking は対象外
   - sealed / non-virtual method interception は対象外
   - parallel test safety は保証しない
   - dedicated test project / sample assembly で検証する

3. API 案

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(fakeRepository);
}
```

必要であれば、対象 assembly / method を明示する API も検討してください。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .ForAssembly(typeof(UserService).Assembly)
        .Returns(fakeRepository);
}
```

4. 内部構成案
   - ShimContext
   - Shim
   - NewShimBuilder<T>
   - NewShimRule
   - ShimRuleRegistry
   - ShimDispatcher
   - AssemblyRewriter
   - NewObjRewriter
   - RewritePlan
   - RewriteReport
   - ShimUnsupportedException
   - ShimRewriteException

5. call site rewrite 方針

`newobj UserRepository::.ctor()` を概念的に以下へ変換する方針を整理してください。

```csharp
var repository = ShimDispatcher.New<UserRepository>();
```

6. test runner 方針
   - rewrite 済み assembly をどこに出力するか
   - rewritten assembly をどう実行するか
   - 通常の `dotnet test` とどう統合するか
   - Visual Studio Test Explorer でどう扱うか
   - dedicated test project に分けるか
   - parallel test を無効化するか

7. 失敗診断
   - Target type:
   - Constructor:
   - Calling assembly:
   - Calling method:
   - Rewrite mode:
   - Reason:
   - Supported patterns:
   - Unsupported patterns:
   - Hint:

8. Phase 2 用の実装プロンプト

次の Phase で Codex CLI に渡せる実装プロンプトを、設計ドキュメントの末尾に含めてください。

### 成果物

以下を作成してください。

- `docs/shims-new-interception-design.md`

### 制約

- この Phase では本格実装しないでください。
- MiniMockito 本体の public API を壊さないでください。
- 既存 v1 / v2 テストを壊さないでください。
- runtime IL rewrite を本体に入れないでください。
- CLR Profiling API を実装しないでください。
- detour / method patching を実装しないでください。
- BCL type の差し替えは対象外にしてください。
- static method mocking は対象外にしてください。

### 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

### 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 設計した方式
- 最初の PoC スコープ
- 採用しなかった方式と理由
- 次に推奨する Phase
