# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 4: newobj rewrite PoC

AGENTS.md、AGENTS.shims-experimental.md、docs/shims-new-interception-design.md を読んでください。

### 目的

限定された `newobj` call site を `ShimDispatcher.New<T>()` に差し替える PoC を実装します。

この Phase は experimental です。  
本体 MiniMockito には組み込まず、`MiniMockito.Shims.Experimental` と専用テスト内に閉じ込めてください。

### 対象

最初は以下に限定してください。

- dedicated sample assembly
- user-defined public class
- non-generic class
- parameterless constructor
- simple `new UserRepository()`
- allowlist で指定された target type
- rewritten assembly は別出力先にコピー
- original assembly は上書きしない

### 非対象

この Phase では以下を実装しないでください。

- production assembly の in-place rewrite
- BCL type 差し替え
- static method mocking
- sealed / non-virtual method body interception
- constructor arguments
- generic classes
- generic constructors
- ref / out
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Visual Studio Test Explorer への完全統合
- parallel test safety guarantee

### 目標

以下の production-like code を、

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

conceptually 以下のように差し替える。

```csharp
public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = ShimDispatcher.New<UserRepository>();
        return repository.GetName(id);
    }
}
```

実際には IL の `newobj UserRepository::.ctor()` を、`ShimDispatcher.New<UserRepository>()` call に置き換える PoC を実装してください。

### 実装対象

以下を実装してください。

- AssemblyRewriter
- NewObjRewriter
- RewriteOptions
- RewriteResult
- RewrittenAssemblyLoader または test helper
- 必要な metadata / reference resolver
- diagnostics

名前は既存構成に合わせて調整して構いません。

### API 候補

```csharp
var result = AssemblyRewriter.RewriteNewObj(
    inputAssemblyPath,
    outputAssemblyPath,
    new RewriteOptions
    {
        TargetTypes = [typeof(UserRepository)]
    });
```

### テスト方式

MSTest で以下を検証してください。

- sample assembly を rewrite できる
- rewrite report に書き換え件数が含まれる
- rewritten assembly の `UserService.GetDisplayName` 実行時に、`new UserRepository()` が ShimDispatcher.New<UserRepository>() 経由になる
- Shim.New<UserRepository>().Returns(fake) で fake が使われる
- ShimContext Dispose 後は rule が cleanup される
- original assembly は変更されない
- unsupported pattern は rewrite せず report される
- 既存 v1 / v2 tests が壊れていない

### 注意

rewritten assembly のロードと実行が難しい場合は、以下のどちらかで最小 PoC に縮小してください。

1. IL 書き換え後の assembly を保存し、scan で `ShimDispatcher.New<T>()` call に置き換わったことを検証する
2. sample method を reflection で実行できるところまでに限定する

中途半端な壊れた実装を残さないでください。

### 診断メッセージ

失敗時には可能な限り以下を含めてください。

- Target type:
- Constructor:
- Calling assembly:
- Calling method:
- Rewrite mode:
- Reason:
- Supported patterns:
- Unsupported patterns:
- Hint:

### 検証

最後に必ず以下を実行してください。

```bash
dotnet build
dotnet test
```

失敗した場合は修正してください。

### 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 実装した rewrite PoC
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 動作するケース
- 動作しないケース
- 次に推奨する Phase
