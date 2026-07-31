# PROMPT.shims.phase10.api-polish-diagnostics.md

# MiniMockito.Shims.Experimental Phase 10: API polish / diagnostics hardening

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 10 として、Shim API polish と diagnostics hardening を実装してください。

この Phase の目的は、Phase 7〜9 で実装した constructor arguments / WithArguments / ShimCaptor API を、利用しやすく、診断しやすく、ドキュメント化された状態に整えることです。

新しい大きな interception 機能は追加しないでください。

## この Phase では実装しないこと

以下は実装しないでください。

- static method mocking
- BCL type 差し替え
- generic class shim
- generic constructor shim
- ref / out constructor arguments
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- AssemblyLoadContext isolation の本格実装
- Microsoft Fakes Shim 完全互換

## 対象

### 1. static using で使いやすい API の確認・整備

以下が compile して動作することを確認・整備してください。

```csharp
using static MiniMockito.Shims.Experimental.ShimArg;

Shim.New<UserRepository>()
    .WithArguments(Any<string>())
    .Returns(fakeRepository);
```

対象 API:

```csharp
Any<T>()
Eq<T>(value)
Is<T>(predicate)
Captor<T>()
```

すでに `ShimArg.Any<T>()` / `ShimArg.Eq<T>()` / `ShimArg.Is<T>()` / `ShimArg.Captor<T>()` がある場合は、static using で自然に使えることをテストで固定してください。

### 2. サンプル整備

以下のサンプルを docs または README に追加してください。

```csharp
Shim.New<UserRepository>()
    .WithArguments(Any<string>())
    .Returns(fakeRepository);
```

```csharp
Shim.New<UserRepository>()
    .WithArguments(Eq("prod"))
    .Returns(fakeRepository);
```

```csharp
Shim.New<UserRepository>()
    .WithArguments(Is<string>(s => s.StartsWith("prod")))
    .Returns(fakeRepository);
```

```csharp
var captured = Captor<string>();

Shim.New<UserRepository>()
    .WithArguments(captured)
    .Returns(fakeRepository);
```

複数引数 constructor:

```csharp
Shim.New<UserRepository>()
    .WithArguments(Eq("prod"), Eq(1), Any<bool>())
    .Returns(fakeRepository);
```

last stub wins:

```csharp
Shim.New<UserRepository>()
    .WithArguments(Any<string>())
    .Returns(defaultRepository);

Shim.New<UserRepository>()
    .WithArguments(Eq("prod"))
    .Returns(prodRepository);
```

no match fallback:

```csharp
Shim.New<UserRepository>()
    .WithArguments(Eq("prod"))
    .Returns(fakeRepository);

// "dev" does not match Eq("prod"), so real constructor fallback is used.
```

### 3. diagnostics hardening

matcher mismatch / no matching rule の診断を改善してください。

可能な範囲で以下を含めてください。

- target type
- actual arguments
- actual argument index
- actual argument type
- actual argument value
- tried rule list
- matcher descriptions
- mismatch reason
- selected rule
- fallback したかどうか
- captor partial capture の注意

例:

```text
No matching new shim rule was found.

Target type: UserRepository
Actual arguments:
  [0] "dev" (System.String)

Tried rules:
  Rule #3:
    [0] expected: Eq("prod")
    result: mismatch

Fallback: real constructor
```

fallback が仕様の場合は例外にしないでください。  
ただし、debug 用に report / diagnostics を取得できる設計があれば望ましいです。

### 4. XML documentation

以下に XML documentation を追加または改善してください。

- `ShimArg`
- `IShimArgumentMatcher`
- `ShimCaptor<T>`
- `ShimConstructorContext`
- `NewShimBuilder<T>.WithArguments`
- `ShimDispatcher.NewWithArgs<T>`
- `ShimContext`
- `Shim.New<T>()`

説明には experimental API であることを含めてください。

### 5. docs 更新

以下を更新してください。

- `docs/shims-constructor-args-design.md`
- `docs/shims-new-interception-design.md`
- experimental README がある場合はそれも更新

必ず以下を明記してください。

- `WithArguments` なし rule は catch-all
- `WithArguments()` 空配列は argument count 0 のみ一致
- 複数 rule 一致時は last stub wins
- no match 時は実 constructor fallback
- captor は partial capture を許容する
- `ShimCaptor<T>.Value` は最後に capture した値を返す
- 未 capture の `Value` は `ShimException`
- `using static MiniMockito.Shims.Experimental.ShimArg;` の利用例
- static method mocking は対象外
- BCL type 差し替えは対象外
- production assembly in-place rewrite は対象外
- parallel test safety は保証しない

## MSTest

以下のテストを追加または更新してください。

### static using tests

- static using で `Any<T>()` が compile して動く
- static using で `Eq<T>()` が compile して動く
- static using で `Is<T>()` が compile して動く
- static using で `Captor<T>()` が compile して動く
- rewritten assembly 実行時にも static using で作成した matcher が動く

### diagnostics tests

- mismatch diagnostics に expected / actual が含まれる
- no match 時に fallback 仕様が確認できる
- tried rule list が確認できる範囲で含まれる
- matcher `Describe()` が診断に反映される
- captor partial capture の挙動がテストで固定される

### regression tests

- Phase 7 constructor arguments tests が壊れていない
- Phase 8 matcher tests が壊れていない
- Phase 9 captor tests が壊れていない
- existing v1 / v2 tests が壊れていない

## 重要な仕様

以下の仕様を変えないでください。

```text
WithArguments なし:
  catch-all rule

WithArguments() 空配列:
  argument count 0 のみ一致

複数 rule 一致:
  last stub wins

no match:
  実 constructor fallback

captor:
  partial capture を許容

ShimCaptor<T>.Value:
  最後に capture した値を返す

未 capture の Value:
  ShimException
```

## 検証

最後に必ず以下を実行してください。

```bash
dotnet build
dotnet test
```

失敗した場合は修正してください。

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- API polish の内容
- diagnostics hardening の内容
- static using 対応の確認結果
- XML docs の更新内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
