# PROMPT.shims.phase7.constructor-args-mvp.md

# MiniMockito.Shims.Experimental Phase 7: constructor arguments MVP

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md を読んでください。

## この Phase の目的

MiniMockito.Shims.Experimental Phase 7 として、constructor arguments を持つ `new` の最小実装を行ってください。

この Phase の目的は、parameterless constructor の `new` 差し替え PoC を拡張し、単純な constructor arguments を持つ `newobj` を `ShimDispatcher` 経由に差し替えられるようにすることです。

## 対象例

```csharp
public class UserRepository
{
    public UserRepository(string connectionString)
    {
    }

    public virtual string GetName(int id)
    {
        return "real:" + id;
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

この `new UserRepository("prod")` を、test assembly rewrite によって `ShimDispatcher` 経由に差し替えられるようにしてください。

## 目指すテスト側 API

第一候補:

```csharp
using (ShimContext.Create())
{
    var fakeRepository = Mock.Class<UserRepository>();

    Shim.New<UserRepository>()
        .WithArguments(Any<string>())
        .Returns(fakeRepository);

    var service = harness.Create<UserService>();

    var result = service.GetDisplayName(1);
}
```

ただし、既存 matcher の再利用が難しい場合は、まず以下の API でも構いません。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(args =>
        {
            var connectionString = (string?)args[0];
            return fakeRepository;
        });

    var service = harness.Create<UserService>();

    var result = service.GetDisplayName(1);
}
```

または、専用 context を使う API でも構いません。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(ctx =>
        {
            var connectionString = ctx.GetArgument<string>(0);
            return fakeRepository;
        });

    var service = harness.Create<UserService>();

    var result = service.GetDisplayName(1);
}
```

最初は `Returns(args => ...)` または `Returns(ctx => ...)` を優先してください。  
`WithArguments(Any<string>())` は可能なら実装してください。難しければ、この Phase では設計だけ残してください。

## 実装対象

### 1. constructor arguments model

以下のいずれか、または同等の model を追加してください。

- `ShimConstructorContext`
- `ShimNewContext`
- `ShimInvocationContext`

保持する情報の候補:

- target type
- constructor
- arguments
- argument types
- context id
- calling assembly / method が取得できる場合は diagnostics 用に保持

例:

```csharp
public sealed class ShimConstructorContext
{
    public Type TargetType { get; }
    public ConstructorInfo? Constructor { get; }
    public IReadOnlyList<object?> Arguments { get; }

    public T? GetArgument<T>(int index)
    {
        return (T?)Arguments[index];
    }
}
```

### 2. ShimDispatcher constructor arguments support

以下のどちらかを実装してください。

```csharp
public static T New<T>(params object?[] args)
```

または:

```csharp
public static T New<T>(ShimConstructorContext context)
```

必要であれば internal overload を追加してください。

要件:

- rule がある場合は、constructor arguments を factory に渡す
- rule がない場合は、arguments に一致する public constructor を呼んで実インスタンスを作る
- parameterless constructor の既存挙動を壊さない
- value type argument は boxing されても正しく扱う
- null argument を保持する

### 3. NewShimBuilder<T> の拡張

以下のどれか、または同等の API を追加してください。

```csharp
Shim.New<UserRepository>()
    .Returns(args => fakeRepository);
```

```csharp
Shim.New<UserRepository>()
    .Returns(ctx => fakeRepository);
```

可能なら以下も追加してください。

```csharp
Shim.New<UserRepository>()
    .WithArguments(Any<string>())
    .Returns(fakeRepository);
```

ただし、matcher 連携が大きくなる場合は `Returns(args => ...)` を優先してください。

### 4. NewShimRule の拡張

`NewShimRule` が constructor arguments を扱えるようにしてください。

候補:

- target type
- constructor signature
- argument matchers
- factory delegate
- context id

最初は constructor signature / matchers が不完全でも構いません。  
ただし、overload constructor を区別できる設計にしてください。

### 5. NewObjRewriter の constructor arguments 対応

`newobj .ctor(arg1, arg2, ...)` を dispatcher call に置き換えてください。

概念的には以下です。

Before:

```csharp
var repository = new UserRepository("prod");
```

After:

```csharp
var repository = ShimDispatcher.New<UserRepository>("prod");
```

IL 上では以下に注意してください。

- `newobj` の直前に stack 上へ積まれた constructor arguments を失わない
- dispatcher call の引数として `object?[]` を作る場合、value type は boxing する
- reference type はそのまま扱う
- null を保持する
- argument の順序を保持する
- `newobj` 命令を `call ShimDispatcher.New<T>(...)` へ置換する
- stack balance を壊さない

実装が難しい場合は、まず string 引数 1 個の constructor だけに縮小して構いません。

### 6. RewriteReport / diagnostics の拡張

constructor arguments 対応の report を追加してください。

含める候補:

- target type
- constructor signature
- argument count
- argument types
- calling type
- calling method
- IL offset
- rewritten / skipped
- unsupported reason

unsupported reason の例:

- constructor has unsupported argument type
- constructor is generic
- declaring type is generic
- target type is not allowlisted
- constructor is not public
- by-ref argument is not supported
- params / optional parameters are not supported
- unable to resolve constructor metadata
- unable to preserve stack balance

### 7. docs 更新

以下を必要に応じて更新してください。

- `docs/shims-constructor-args-design.md`
- `docs/shims-new-interception-design.md`
- experimental README がある場合はそれも更新

必ず以下を明記してください。

- constructor arguments support は experimental
- 最初は simple argument のみ対応
- BCL type 差し替えは対象外
- static method mocking は対象外
- generic は対象外
- production assembly の in-place rewrite は対象外
- parallel test safety は保証しない

## 最初の対応範囲

この Phase の最初の対応範囲は以下です。

- user-defined public class
- non-generic class
- public constructor
- string argument
- int argument
- bool argument
- reference type argument
- null argument
- constructor overload の識別
- allowlist で指定された target type
- dedicated sample assembly
- original assembly は上書きしない

## この Phase では対応しないこと

以下は実装しないでください。

- static method mocking
- BCL type 差し替え
- generic class
- generic constructor
- ref / out constructor arguments
- params / optional parameter の高度対応
- expression tree 内の new
- async state machine 内の複雑な new
- iterator 内の new
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- production assembly in-place rewrite
- Visual Studio Test Explorer への完全統合

## MSTest

以下のテストを追加してください。

### Dispatcher tests

- `ShimDispatcher.New<T>("prod")` が rule の factory に `"prod"` を渡す
- `Returns(args => ...)` で arguments を読んで fake を返せる
- string argument を検証できる
- int argument を検証できる
- bool argument を検証できる
- null argument を検証できる
- rule がない場合、arguments に一致する public constructor で実インスタンスを作る
- parameterless constructor の既存テストが壊れていない

### Rewriter tests

- `new UserRepository("prod")` を rewrite できる
- rewritten assembly の実行時に `ShimDispatcher.New<UserRepository>("prod")` 経由になる
- fake が返る
- constructor arguments の順序が保持される
- value type arguments が boxing されても読める
- null argument が保持される
- constructor overload を区別できる
- unsupported constructor pattern は rewrite せず reason を report する
- original assembly は変更されない

### Regression tests

- existing parameterless constructor rewrite tests が壊れていない
- existing Phase 2 / Phase 3 / Phase 4 / Phase 5 tests が壊れていない
- 既存 v1 / v2 tests が壊れていない

## 重要な実装注意

IL stack 上の constructor arguments を正しい順序で dispatcher に渡してください。

`newobj .ctor(arg1, arg2)` を差し替える場合、元の constructor arguments を `object?[]` または専用 context に詰めて `ShimDispatcher.New<T>(...)` に渡す方針で実装してください。

value type 引数は boxing が必要です。  
null 引数も失わないでください。  
argument order を壊さないでください。  
stack balance を壊さないでください。

難しい場合は、まず string 引数 1 個の constructor だけを通す最小 PoC に縮小して構いません。  
ただし、中途半端な壊れた実装は残さないでください。

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
- 実装した constructor arguments support
- 実装した API
- 対応した constructor argument pattern
- 対応していない pattern
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
