# PROMPT.shims.phase9.shim-captor.md

# MiniMockito.Shims.Experimental Phase 9: ShimCaptor

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 9 として、`ShimCaptor` を実装してください。

この Phase の目的は、Phase 8 で実装した `WithArguments` matcher API に、constructor arguments を capture できる matcher を追加することです。

Phase 8 では以下のように constructor arguments に matcher を適用できるようになっています。

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Eq("prod"))
    .Returns(fakeRepository);
```

Phase 9 では、実際に渡された constructor argument をテスト側から検証できるようにします。

```csharp
var connectionString = ShimCaptor.For<string>();

Shim.New<UserRepository>()
    .WithArguments(connectionString)
    .Returns(fakeRepository);

var service = harness.Create<UserService>();

service.GetDisplayName(1);

Assert.AreEqual("prod", connectionString.Value);
```

## 目標 API

### 単一 capture

```csharp
var connectionString = ShimCaptor.For<string>();

using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .WithArguments(connectionString)
        .Returns(fakeRepository);

    var service = harness.Create<UserService>();

    service.GetDisplayName(1);
}

Assert.AreEqual("prod", connectionString.Value);
```

### 複数回 capture

```csharp
var captured = ShimCaptor.For<string>();

using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .WithArguments(captured)
        .Returns(fakeRepository);

    var service = harness.Create<UserService>();

    service.GetDisplayName(1);
    service.GetDisplayName(2);
}

CollectionAssert.AreEqual(
    new[] { "prod", "prod" },
    captured.Values.ToArray());
```

### matcher との混在

```csharp
var name = ShimCaptor.For<string>();

Shim.New<UserRepository>()
    .WithArguments(
        ShimArg.Eq("prod"),
        name,
        ShimArg.Any<bool>())
    .Returns(fakeRepository);
```

## 実装対象

以下を実装してください。

### 1. ShimCaptor<T>

`IShimArgumentMatcher` を実装する capture matcher を追加してください。

候補 API:

```csharp
public sealed class ShimCaptor<T> : IShimArgumentMatcher
{
    public Type? ExpectedType { get; }

    public T? Value { get; }

    public IReadOnlyList<T?> Values { get; }

    public bool HasValue { get; }

    public void Clear();

    public bool Matches(object? value);

    public string Describe();
}
```

仕様:

- `T` に代入可能な actual argument のみ一致する
- reference type / nullable type の場合は null を capture できる
- non-nullable value type の場合は null に一致しない
- 一致した場合のみ capture する
- mismatch の場合は capture しない
- 複数回一致した場合は `Values` にすべて保存する
- `Value` は最後に capture した値を返す
- まだ capture されていない状態で `Value` を読むと分かりやすい例外を投げる
- `Values` は読み取り専用として公開する
- `Clear()` は captured values を消す

### 2. factory API

以下のどちらか、または両方を実装してください。

推奨:

```csharp
var captor = ShimCaptor.For<string>();
```

追加候補:

```csharp
var captor = ShimArg.Captor<string>();
```

`ShimCaptor.For<T>()` を主 API としてください。  
`ShimArg.Captor<T>()` は便利 API として実装できるなら実装してください。

### 3. type matching

`ShimCaptor<T>` の型判定は、Phase 8 の `ShimArg.Any<T>()` と同じ考え方に寄せてください。

推奨仕様:

```text
reference type:
  null に一致し、capture する

nullable value type:
  null に一致し、capture する

non-nullable value type:
  null に一致しない

boxed value type:
  T と一致すれば capture する
```

### 4. capture timing

以下の仕様で実装してください。

```text
matcher が型として一致した場合:
  capture する

matcher が型として一致しない場合:
  capture しない

複数 matcher を含む WithArguments で、後続 matcher が失敗した場合:
  原則として既に一致した captor は capture 済みになる
```

ただし、後続 matcher 失敗時に partial capture を避けたい場合は、two-pass matching を設計してから実装してください。  
この Phase ではシンプルさを優先して、上記の partial capture 許容で構いません。  
その場合は docs に明記してください。

### 5. diagnostics

`Describe()` は分かりやすい文字列を返してください。

候補:

```text
Capture<String>()
Capture<Int32>()
Capture<Boolean>()
```

`Value` 未取得時の例外メッセージには以下を含めてください。

- captor type
- captured count
- hint

例:

```text
No value has been captured for ShimCaptor<String>.
Captured count: 0.
Hint: Ensure the captor is used in WithArguments(...) and the shim rule actually matches.
```

### 6. docs 更新

以下を必要に応じて更新してください。

- `docs/shims-constructor-args-design.md`
- `docs/shims-new-interception-design.md`
- experimental README がある場合はそれも更新

必ず以下を明記してください。

- `ShimCaptor` は experimental
- `ShimCaptor.For<T>()`
- `ShimArg.Captor<T>()` を実装した場合はそれも記載
- `Value`
- `Values`
- `HasValue`
- `Clear()`
- null capture の仕様
- value type boxing の扱い
- mismatch 時に capture しないこと
- partial capture を許容する場合はその仕様
- static method mocking は対象外
- BCL type 差し替えは対象外
- production assembly in-place rewrite は対象外

## 最初の対応範囲

この Phase の最初の対応範囲は以下です。

- string capture
- int capture
- bool capture
- reference type capture
- null capture
- multiple captures
- `Value`
- `Values`
- `HasValue`
- `Clear()`
- `Describe()`
- `WithArguments(captor)` integration
- `Eq` / `Any` / `Is` matcher との混在

## この Phase では対応しないこと

以下は実装しないでください。

- static method mocking
- BCL type 差し替え
- generic class shim
- generic constructor shim
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

### ShimCaptor unit tests

- `ShimCaptor.For<string>()` が string を capture できる
- `ShimCaptor.For<string>()` が null を capture できる
- `ShimCaptor.For<int>()` が boxed int を capture できる
- `ShimCaptor.For<bool>()` が boxed bool を capture できる
- mismatch の場合 capture されない
- 複数回 capture した場合 `Values` にすべて保存される
- `Value` は最後の値を返す
- `HasValue` は capture 前 false、capture 後 true
- `Clear()` で captured values が消える
- 未 capture 状態で `Value` を読むと分かりやすい例外になる
- `Describe()` が分かりやすい文字列を返す

### Registry / dispatcher tests

- `WithArguments(captor)` で argument を capture できる
- `WithArguments(ShimArg.Eq("prod"), captor)` で一部引数を capture できる
- `WithArguments(captor, ShimArg.Any<int>())` で複数 matcher と混在できる
- `ShimArg.Captor<T>()` を実装した場合はそれもテストする
- mismatch の場合 capture されない
- 複数 rule がある場合、実際に選ばれた rule の captor だけが capture する
- catch-all rule と captor rule の優先順位が Phase 8 仕様どおりになる
- `Returns(args => ...)` API が壊れていない
- `Returns(ctx => ...)` API が壊れていない
- parameterless constructor shim が壊れていない

### Rewriter integration tests

- rewritten assembly 実行時に constructor argument を capture できる
- `new UserRepository("prod")` の `"prod"` を capture できる
- 複数回 service method を呼ぶと `Values` に複数保存される
- 複数引数 constructor の一部を capture できる
- `Eq` / `Any` / `Is` matcher と captor を混在できる
- original assembly は変更されない

### Regression tests

- existing Phase 2 / Phase 3 / Phase 4 / Phase 5 / Phase 7 / Phase 8 tests が壊れていない
- existing v1 / v2 tests が壊れていない

## 重要な仕様決定

以下の仕様で実装してください。既存設計と矛盾する場合は、より安全な方を選び、理由を docs に書いてください。

```text
ShimCaptor<T>:
  IShimArgumentMatcher を実装する

Value:
  最後に capture した値を返す

Values:
  capture した値を順序どおりすべて返す

HasValue:
  capture count > 0

Clear:
  capture history を消す

null:
  reference type / nullable type では capture する
  non-nullable value type では一致しない

mismatch:
  capture しない

partial capture:
  この Phase では許容してよい
  許容する場合は docs に明記する
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
- 実装した ShimCaptor API
- null capture の仕様
- mismatch 時の capture 仕様
- partial capture の扱い
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
