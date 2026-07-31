# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 2: project skeleton

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md を読んでください。

### 目的

`MiniMockito.Shims.Experimental` の project skeleton を作成します。

この Phase では、まだ `newobj` の実際の書き換えは実装しません。  
Shim API、rule registry、diagnostics、test project の土台を作ってください。

### 作成するプロジェクト

以下を追加してください。

```text
src/
  MiniMockito.Shims.Experimental/

tests/
  MiniMockito.Shims.Experimental.Tests/
```

既存 solution に追加してください。

### public API の最小形

以下の API が compile できるようにしてください。

```csharp
using MiniMockito.Shims.Experimental;

using (ShimContext.Create())
{
    var fakeRepository = new UserRepository();

    Shim.New<UserRepository>()
        .Returns(fakeRepository);
}
```

この Phase では実際の `new UserRepository()` 差し替えはまだ行わなくてよいです。  
ただし、rule が registry に登録されるところまでは実装してください。

### 実装対象

以下を実装してください。

- ShimContext
  - Create()
  - IDisposable
  - 現在の context 管理
  - Dispose 時の cleanup

- Shim
  - New<T>()

- NewShimBuilder<T>
  - Returns(T instance)
  - Returns(Func<T> factory) が可能なら実装

- NewShimRule
  - TargetType
  - Factory
  - ContextId

- ShimRuleRegistry
  - rule 登録
  - rule 検索
  - context 単位の cleanup

- ShimDispatcher
  - New<T>() の入口
  - 登録済み rule があれば fake / factory result を返す
  - rule がなければ new T() を呼ぶ。ただし T は parameterless constructor 前提

- Exceptions
  - ShimException
  - ShimUnsupportedException
  - ShimRewriteException

### 制約

- runtime IL rewrite はまだ実装しないでください。
- assembly rewriting はまだ実装しないでください。
- CLR Profiling API は実装しないでください。
- detour / method patching は実装しないでください。
- static method mocking は実装しないでください。
- MiniMockito 本体の public API を壊さないでください。
- 既存 v1 / v2 テストを壊さないでください。

### MSTest

以下のテストを追加してください。

- ShimContext.Create() で context を作成できる
- Shim.New<T>().Returns(instance) で rule を登録できる
- ShimDispatcher.New<T>() は rule がある場合に fake instance を返す
- ShimDispatcher.New<T>() は rule がない場合に parameterless constructor で実インスタンスを作る
- Dispose 後は rule が cleanup される
- context 外で Shim.New<T>() した場合は分かりやすい例外になる
- 複数 context の rule が混ざらない
- 既存 v1 / v2 テストが壊れていない

### 注意

この Phase の `ShimDispatcher.New<T>()` は手動呼び出しでテストします。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>().Returns(fake);

    var actual = ShimDispatcher.New<UserRepository>();

    Assert.AreSame(fake, actual);
}
```

まだ production code 内の `new UserRepository()` を自動的に差し替える必要はありません。

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
- 実装した skeleton
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
