# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 3: rewrite plan dry-run

AGENTS.md、AGENTS.shims-experimental.md、docs/shims-new-interception-design.md を読んでください。

### 目的

`new SomeClass()` 差し替えのための rewrite plan / dry-run を実装します。

この Phase では、まだ assembly を実際には書き換えません。  
対象 assembly を解析し、どの `newobj` call site を差し替え候補として検出できるかを report してください。

### 実装対象

以下を実装してください。

- RewritePlan
- RewriteTarget
- RewriteReport
- AssemblyRewriteScanner
- NewObjCallSite
- NewObjScanOptions
- NewObjScanResult

必要に応じて名前は調整して構いません。

### 対象

最初は以下に限定してください。

- user assembly
- public class
- parameterless constructor
- non-generic class
- simple `newobj`
- allowlist で指定された target type のみ

### API 候補

```csharp
var report = AssemblyRewriteScanner.Scan(
    assemblyPath,
    new NewObjScanOptions
    {
        TargetTypes = [typeof(UserRepository)]
    });
```

report には以下を含めてください。

- 対象 assembly
- 対象 type
- 対象 constructor
- 呼び出し元 type
- 呼び出し元 method
- IL offset
- supported / unsupported
- unsupported reason

### 実装方式

可能なら Mono.Cecil などの IL inspection library を使って構いません。  
使用する場合は、なぜ必要か、代替案、依存追加の影響を README または設計ドキュメントに追記してください。

外部 mocking framework は使わないでください。

### 非対象

この Phase では以下を実装しないでください。

- IL の実際の書き換え
- rewritten assembly の出力
- runtime patch
- profiler API
- detour / method patching
- static method mocking
- BCL type 差し替え
- constructor arguments 対応

### MSTest

以下のテストを追加してください。

- sample assembly 内の `new UserRepository()` を検出できる
- allowlist に含まれない type は対象外になる
- parameterless constructor のみ supported になる
- constructor arguments ありの `new` は unsupported として report される
- generic type は unsupported として report される
- report に calling type / method / IL offset が含まれる
- unsupported reason が分かりやすい
- 既存 tests が壊れていない

### 設計ドキュメント更新

`docs/shims-new-interception-design.md` に dry-run scanner の設計と制約を追記してください。

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
- 実装した dry-run scanner
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
