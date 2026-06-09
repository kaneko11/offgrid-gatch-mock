# PROMPT.shims.phase8.with-arguments-matcher-api.md

# MiniMockito.Shims.Experimental Phase 8: WithArguments matcher API

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 8 として、`WithArguments` matcher API を実装してください。

この Phase の目的は、Phase 7 で実装した constructor arguments support に対して、引数条件付きで `new` shim rule を選択できる API を追加することです。

Phase 7 では以下のような API が使えるようになっています。

```csharp
Shim.New<UserRepository>()
    .Returns(args =>
    {
        var connectionString = (string?)args[0];
        return fakeRepository;
    });
```

Phase 8 では、これをより読みやすく、Mockito 風に書けるようにします。

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Any<string>())
    .Returns(fakeRepository);
```

または:

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Eq("prod"))
    .Returns(fakeRepository);
```

## 目標 API

最小目標:

```csharp
using (ShimContext.Create())
{
    var fakeRepository = Mock.Class<UserRepository>();

    Shim.New<UserRepository>()
        .WithArguments(ShimArg.Any<string>())
        .Returns(fakeRepository);

    var service = harness.Create<UserService>();

    var result = service.GetDisplayName(1);
}
```

追加目標:

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Eq("prod"))
    .Returns(fakeRepository);
```

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Is<string>(s => s.StartsWith("prod")))
    .Returns(fakeRepository);
```

複数引数:

```csharp
Shim.New<UserRepository>()
    .WithArguments(
        ShimArg.Eq("prod"),
        ShimArg.Eq(3),
        ShimArg.Any<bool>())
    .Returns(fakeRepository);
```

null:

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Eq<string?>(null))
    .Returns(fakeRepository);
```

## API 名の方針

`Any<T>()` をトップレベル関数のように使うのが難しい場合は、まず `ShimArg.Any<T>()` 形式で実装してください。

推奨:

```csharp
ShimArg.Any<T>()
ShimArg.Eq<T>(T? value)
ShimArg.Is<T>(Predicate<T?> predicate)
```

将来的に static using で以下のように書ける設計にしても構いません。

```csharp
using static MiniMockito.Shims.Experimental.ShimArg;

Shim.New<UserRepository>()
    .WithArguments(Any<string>())
    .Returns(fakeRepository);
```

ただし、この Phase では `ShimArg.Any<T>()` 形式で十分です。

## 実装対象

### 1. matcher interface

以下、または同等の interface を追加してください。

```csharp
public interface IShimArgumentMatcher
{
    Type? ExpectedType { get; }

    bool Matches(object? value);

    string Describe();
}
```

必要に応じて generic 版を内部で持っても構いません。

```csharp
public interface IShimArgumentMatcher<in T> : IShimArgumentMatcher
{
    bool Matches(T? value);
}
```

ただし、public API が複雑になりすぎる場合は non-generic interface に寄せてください。

### 2. Any matcher

```csharp
ShimArg.Any<T>()
```

要件:

- `T` に代入可能な値に一致する
- reference type の場合、null を許すかどうかを仕様として決めて docs に明記する
- 最初は `Any<T>()` は null にも一致してよい
- value type の boxed value に一致する
- mismatch の diagnostics に期待型を出す

### 3. Eq matcher

```csharp
ShimArg.Eq<T>(T? value)
```

要件:

- `EqualityComparer<T>.Default` 相当で比較する
- null を扱える
- boxed value type を扱える
- expected value を diagnostics に出す

### 4. Predicate matcher

```csharp
ShimArg.Is<T>(Predicate<T?> predicate)
```

または:

```csharp
ShimArg.Is<T>(Func<T?, bool> predicate)
```

要件:

- predicate が true を返したら一致
- predicate が例外を投げた場合は matcher failure として扱い、分かりやすい例外または diagnostics を出す
- diagnostics に predicate matcher であることを出す
- null の扱いを docs に明記する

### 5. WithArguments API

`NewShimBuilder<T>` に以下、または同等の API を追加してください。

```csharp
public NewShimBuilder<T> WithArguments(params IShimArgumentMatcher[] matchers)
```

要件:

- `WithArguments(...)` の後に `Returns(...)` が呼べる
- matcher 数と actual arguments 数が一致しない場合は一致しない扱いにする
- mismatched reason を diagnostics に残す
- `WithArguments` を複数回呼んだ場合の挙動を決める
  - 推奨: 後から呼んだものが上書き
  - または例外
  - docs に明記する

### 6. NewShimRule matcher support

`NewShimRule` に matchers を保持できるようにしてください。

保持候補:

- target type
- constructor signature
- argument matchers
- factory delegate
- context id
- registration sequence number

要件:

- matchers が null / empty の rule は catch-all として扱うか、明確に仕様化する
- 推奨: `WithArguments` なしの rule は catch-all
- `WithArguments()` の空配列は parameterless constructor only に一致させるか、catch-all にするかを明確にする
  - 推奨: 空配列は argument count 0 に一致
  - `WithArguments` なしは catch-all

### 7. ShimRuleRegistry matching support

`ShimRuleRegistry` が constructor arguments を使って最適な rule を選べるようにしてください。

推奨仕様:

- 現在の `ShimContext` 内の rule のみ対象
- target type が一致する rule のみ対象
- `WithArguments` あり rule は matcher がすべて一致した場合のみ対象
- `WithArguments` なし rule は catch-all rule として扱う
- 複数 rule が一致する場合は、後から登録した rule を優先する
- どの rule にも一致しない場合は、既存の実 constructor fallback を使う
- no match diagnostics を `RewriteReport` または runtime exception message に含められる範囲で含める

後から登録した rule を優先する理由を docs に明記してください。  
Mockito 風の「後から書いた stub が上書きする」感覚に近いためです。

### 8. ShimDispatcher.NewWithArgs<T>() との統合

Phase 7 の `ShimDispatcher.NewWithArgs<T>(object?[] args)` が matcher-based rule selection を使うようにしてください。

要件:

- args を `ShimConstructorContext` に入れる
- `ShimRuleRegistry` に target type と args を渡して rule を探す
- rule が見つかったらその factory を実行する
- rule が見つからない場合は実 constructor fallback
- existing `Returns(args => ...)` API を壊さない
- existing `Returns(ctx => ...)` API を壊さない
- parameterless constructor shim を壊さない

### 9. mismatch diagnostics

可能な範囲で、argument mismatch の理由を出してください。

候補:

- expected matcher count
- actual argument count
- argument index
- expected matcher description
- actual value
- actual type
- target type
- constructor signature

例:

```text
No matching new shim rule was found.

Target type: UserRepository
Arguments:
  [0] actual: "dev" (System.String)

Tried rules:
  Rule #2:
    [0] expected: Eq("prod")
    result: mismatch

Fallback: real constructor
```

runtime fallback にする場合でも、debug 用に report / diagnostics を取得できる形が望ましいです。  
ただし、この Phase では過度に複雑にしないでください。

## 最初の対応範囲

この Phase の最初の対応範囲は以下です。

- string
- int
- bool
- reference type
- null
- multiple arguments
- constructor overload
- multiple rule matching
- catch-all rule
- mismatch diagnostics
- existing Phase 7 constructor arguments support との統合

## この Phase では対応しないこと

以下は実装しないでください。

- static method mocking
- BCL type 差し替え
- generic class
- generic constructor
- ref / out constructor arguments
- params / optional parameter の高度対応
- expression tree matcher
- async state machine 内の複雑な new
- iterator 内の new
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- Visual Studio Test Explorer への完全統合
- Microsoft Fakes Shim 完全互換

## MSTest

以下のテストを追加してください。

### Matcher unit tests

- `ShimArg.Any<string>()` が string に一致する
- `ShimArg.Any<string>()` が null に一致する、または一致しない仕様をテストする
- `ShimArg.Any<int>()` が boxed int に一致する
- `ShimArg.Eq("prod")` が `"prod"` に一致する
- `ShimArg.Eq("prod")` が `"dev"` に一致しない
- `ShimArg.Eq<int>(1)` が boxed int 1 に一致する
- `ShimArg.Eq<bool>(true)` が boxed bool true に一致する
- `ShimArg.Eq<string?>(null)` が null に一致する
- `ShimArg.Is<string>(predicate)` が一致する
- `ShimArg.Is<string>(predicate)` が不一致になる
- predicate が例外を投げた場合の挙動をテストする

### Registry / dispatcher tests

- `WithArguments(ShimArg.Any<string>())` が string argument に一致する
- `WithArguments(ShimArg.Eq("prod"))` が `"prod"` に一致する
- `WithArguments(ShimArg.Eq("prod"))` が `"dev"` に一致しない
- `WithArguments(ShimArg.Is<string>(...))` が一致する
- int argument に `Eq<int>(1)` が一致する
- bool argument に `Eq<bool>(true)` が一致する
- null argument に `Eq<string?>(null)` が一致する
- 複数引数の順序が正しく matching される
- matcher 数と actual argument 数が違う場合は一致しない
- 複数 rule が一致した場合、後から登録した rule が優先される
- `WithArguments` なし rule が catch-all として働く
- `WithArguments` あり rule が catch-all より優先されるか、登録順仕様どおりに動く
- no match 時は実 constructor fallback になる
- `Returns(args => ...)` API が壊れていない
- `Returns(ctx => ...)` API が壊れていない
- parameterless constructor shim が壊れていない

### Rewriter integration tests

- rewritten assembly 実行時に `WithArguments(ShimArg.Eq("prod"))` が使われる
- rewritten assembly 実行時に `WithArguments(ShimArg.Any<string>())` が使われる
- rewritten assembly 実行時に mismatch の場合 fallback する
- 複数引数 constructor で matcher が順序通り一致する
- original assembly は変更されない

### Regression tests

- existing Phase 2 / Phase 3 / Phase 4 / Phase 5 / Phase 7 tests が壊れていない
- existing v1 / v2 tests が壊れていない

## docs 更新

以下を必要に応じて更新してください。

- `docs/shims-constructor-args-design.md`
- `docs/shims-new-interception-design.md`
- experimental README がある場合はそれも更新

必ず以下を明記してください。

- `WithArguments` matcher API は experimental
- `ShimArg.Any<T>()`
- `ShimArg.Eq<T>(value)`
- `ShimArg.Is<T>(predicate)`
- `WithArguments` なし rule の扱い
- 複数 rule が一致した場合の優先順位
- no match 時の fallback 仕様
- null matching の仕様
- value type boxing の扱い
- static method mocking は対象外
- BCL type 差し替えは対象外
- production assembly in-place rewrite は対象外

## 重要な仕様決定

以下の仕様で実装してください。既存設計と矛盾する場合は、より安全な方を選び、理由を docs に書いてください。

```text
WithArguments なし:
  catch-all rule

WithArguments() 空配列:
  argument count 0 に一致

複数 rule が一致:
  後から登録した rule を優先

WithArguments あり rule と catch-all rule が両方一致:
  登録順に従う。ただし、後から登録した rule を優先

no match:
  実 constructor fallback

Any<T>() の null:
  reference type / nullable type では null に一致
  non-nullable value type では null に一致しない
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
- 実装した matcher API
- 実装した matching rule
- null matching の仕様
- 複数 rule 優先順位の仕様
- no match 時の仕様
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
