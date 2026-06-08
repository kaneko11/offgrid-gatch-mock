# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 6: constructor arguments design

AGENTS.md、AGENTS.shims-experimental.md、docs/shims-new-interception-design.md を読んでください。

### 目的

parameterless constructor の `new` 差し替え PoC の次に、constructor arguments を持つ `new` へ対応を広げられるか設計調査します。

この Phase では、まだ constructor arguments の実装はしないでください。  
設計・リスク・API・テスト方針だけを整理してください。

### 対象例

```csharp
public class UserRepository
{
    public UserRepository(string connectionString)
    {
    }
}

public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository("prod");
        return repository.GetName(id);
    }
}
```

将来的には以下のような API を検討します。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .WithArguments(Any<string>())
        .Returns(fakeRepository);
}
```

または:

```csharp
Shim.New<UserRepository>()
    .Returns(ctx =>
    {
        var connectionString = ctx.Arguments[0];
        return fakeRepository;
    });
```

### 設計すること

以下を整理してください。

- IL stack 上の constructor arguments をどう扱うか
- `newobj .ctor(arg1, arg2)` を dispatcher call に置換する方法
- argument matcher を shim に再利用できるか
- Captor を使えるか
- overload constructor の扱い
- value type / reference type 引数
- null 引数
- params / optional parameter
- generic argument
- unsupported pattern
- diagnostics
- API 案
- テスト方針

### 成果物

以下を作成または更新してください。

- `docs/shims-constructor-args-design.md`
- `docs/shims-new-interception-design.md`

### 制約

- この Phase では実装しないでください。
- static method mocking は実装しないでください。
- BCL type 差し替えは実装しないでください。
- runtime IL rewrite は実装しないでください。
- CLR Profiling API は実装しないでください。
- detour / method patching は実装しないでください。

### 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

### 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- constructor arguments 対応の設計要約
- 実装難易度
- 対応すべき最小スコープ
- 対応しないほうがよい範囲
- 次に推奨する Phase
