# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## Phase 5: integration and safety

AGENTS.md、AGENTS.shims-experimental.md、docs/shims-new-interception-design.md を読んでください。

### 目的

Phase 4 の `newobj` rewrite PoC を、テストで扱いやすい形に整理し、安全性と診断を強化します。

新しい対象範囲を大きく広げず、限定された `new` 差し替えの使いやすさと安全性を上げてください。

### 対象

#### 1. ShimContext safety

- nested context の挙動を明確化
- Dispose 漏れ検出
- cleanup failure の明示
- context 外使用時の例外改善
- async / thread の扱いを documentation に明記

#### 2. Parallel test safety

- shim experimental tests の parallelization を無効化する設定を追加
- process-wide / context-local の境界を README に明記
- 同時実行が危険な理由を docs に追記

#### 3. Rewrite diagnostics

- rewrite report を読みやすくする
- unsupported pattern の理由を増やす
- rewrite された call site 一覧を出す
- rewrite されなかった call site 一覧を出す

#### 4. Test helper

専用 test helper を追加してください。

候補:

```csharp
var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteSampleAssembly();

using (ShimContext.Create())
{
    Shim.New<UserRepository>().Returns(fake);

    var service = harness.Create<UserService>();
}
```

API は実装しやすい形に調整して構いません。

#### 5. Documentation

以下を更新してください。

- `docs/shims-new-interception-design.md`
- `docs/v2-shims-experimental-design.md`
- README または experimental README

必ず以下を明記してください。

- これは experimental である
- 本体 MiniMockito の安定 API ではない
- BCL type は対象外
- static method は対象外
- constructor arguments は対象外
- generic は対象外
- parallel test は危険
- Visual Studio Test Explorer での完全統合は未対応

### 非対象

この Phase では以下を実装しないでください。

- static method mocking
- BCL type 差し替え
- constructor arguments
- generic classes
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite

### MSTest

以下のテストを追加してください。

- nested context の挙動
- Dispose 後 cleanup
- context 外 usage の例外
- rewrite diagnostics の内容
- unsupported pattern diagnostics
- harness で sample service を実行できる
- parallelization disable 設定がある
- 既存 tests が壊れていない

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
- 安全性改善の内容
- 診断改善の内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
