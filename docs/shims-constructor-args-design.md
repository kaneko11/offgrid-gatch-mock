# Shims Constructor Arguments Design

## 1. 目的

このドキュメントは `MiniMockito.Shims.Experimental` における constructor arguments を持つ
`new` の差し替え対応を、将来の実装フェーズ（Phase 7 以降）のために設計調査したものです。

**Phase 6 では実装しません。** 設計・リスク・API・テスト方針の整理のみを行います。

## 2. 対象例

```csharp
public class UserRepository
{
    public UserRepository(string connectionString) { ... }
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

IL 上では次のようになります。

```text
IL_0000: ldstr      "prod"
IL_0005: newobj     instance void UserRepository::.ctor(string)
IL_000a: stloc.0
```

Phase 4 / 5 の rewriter はこの `newobj` をスキップし、診断に `ConstructorArgumentsNotSupported` を
記録します。Phase 7 以降でこの制限を解除することが目標です。

## 3. IL stack 上の constructor arguments の扱い

### 3.1 問題の本質

`newobj` 命令はスタック上に引数を消費してインスタンスをプッシュします。

```
; new UserRepository("prod")
ldstr "prod"                ; stack: ["prod"]
newobj .ctor(string)        ; stack: [UserRepository]  ← "prod" を消費してインスタンスを返す
```

parameterless の場合は単純な置換で済みました。

```
; 変換前
newobj .ctor()              ; stack: [] → [UserRepository]
; 変換後
call ShimDispatcher.New<UserRepository>()  ; 同じスタック効果
```

引数ありの場合はスタック上の引数を保持したまま `object[]` に束ねて dispatcher へ渡す必要があり、
同様の単純置換はできません。

### 3.2 スタック変換の方針

主な方式を 3 つ比較します。

#### 方式 A: 直接 IL 変換（Stack Manipulation）

`newobj` の直前にある引数プッシュ命令をすべて特定し、それらを `object[]` の構築命令に
書き換えて dispatcher を呼び出す。

```text
; 変換前
ldstr "prod"
newobj .ctor(string)

; 変換後（概念）
ldc.i4.1
newarr object
dup
ldc.i4.0
ldstr "prod"    ; ← 元の push 命令を再配置
stelem.ref
call ShimDispatcher.NewWithArgs<UserRepository>(object[])
```

**問題点:**
- `newobj` の前の引数がどのプッシュ命令から来るかは、静的な制御フロー解析（スタック追跡）が必要
- 引数が `ldarg` / `ldloc` のような単純命令の場合は追跡可能
- `call` / `callvirt`（サブ式）や `dup` が絡む場合は複雑
- 例外ハンドラブロック境界をまたぐ場合は不正 IL になりやすい
- 実装コスト: 非常に高い

#### 方式 B: Wrapper Method 生成（推奨）

コンストラクタのシグネチャごとに、入力 assembly 内に静的ラッパーメソッドを生成し、
`newobj` をそのラッパーへの `call` に置き換える。

```csharp
// 生成されるラッパー（Mono.Cecil で assembly に注入）
private static UserRepository __ShimWrap_UserRepository_String(string arg0)
{
    return ShimDispatcher.NewWithArgs<UserRepository>(new object[] { arg0 });
}
```

```text
; 変換後
ldstr "prod"
call static UserRepository __ShimWrap_UserRepository_String(string)
```

**利点:**
- スタック効果が元の `newobj` と同一（`string → UserRepository`）
- 引数プッシュ命令を一切変更しない
- value type の boxing はラッパー内部で実施できる
- Mono.Cecil による新メソッド追加は既に Phase 4 で検証済み
- 実装コスト: 中程度

**欠点:**
- 入力 assembly に生成メソッドが追加される（アーティファクト）
- コンストラクタシグネチャ × コールサイト数分のメソッドが生成される可能性

#### 方式 C: 専用 Assembly に Wrapper を配置

ラッパーを入力 assembly ではなく専用 helper assembly に生成し、入力 assembly から参照させる。
実装コストがさらに増すため、初期 PoC には向かない。

### 3.3 推奨方式

**方式 B（Wrapper Method 生成）** を Phase 7 の初期実装ターゲットとする。

理由：
- Phase 4 の newobj rewriter を自然に拡張できる
- スタック追跡が不要で、コントロールフロー上のリスクが低い
- value type の boxing をラッパー内部に封じ込められる
- 診断とテストを書きやすい

## 4. ShimDispatcher の拡張設計

### 4.1 `NewWithArgs<T>(object?[] args)` の追加

```csharp
/// <summary>
/// Creates an instance of T through the active shim rule, matching on argument values.
/// Called by generated wrapper methods for constructors with arguments.
/// </summary>
public static T NewWithArgs<T>(object?[] args)
{
    var targetType = typeof(T);
    var context = ShimContext.Current;

    if (context is { IsDisposed: false } &&
        context.Registry.TryFindNewRuleWithArgs(targetType, args, out var rule) &&
        rule is not null)
    {
        return (T)rule.CreateInstance(args)!;
    }

    // Fallback: invoke real constructor via Activator
    return (T)Activator.CreateInstance(targetType, args)!;
}
```

### 4.2 Wrapper Method の IL テンプレート（例：引数 1 個、string）

```text
.method private hidebysig static class UserRepository
    __ShimWrap_UserRepository_String(string arg0) cil managed
{
    .maxstack 5
    ldc.i4.1
    newarr [System.Runtime]System.Object
    dup
    ldc.i4.0
    ldarg.0          ; arg0 (string, 参照型なので boxing 不要)
    stelem.ref
    call !!0 [MiniMockito.Shims.Experimental]ShimDispatcher::NewWithArgs<UserRepository>(object[])
    ret
}
```

value type 引数（例: `int`）の場合は `ldarg.0` の後に `box int32` を挿入する。

## 5. ShimRuleRegistry の拡張設計

### 5.1 引数付きルールのマッチング

現在の `NewShimRule` はターゲット型のみでマッチングします。引数対応のために、
オプションの引数マッチャーリストを追加します。

```csharp
public sealed class NewShimRule
{
    // 既存フィールド
    public Type TargetType { get; }
    public Guid ContextId { get; }
    public long RegistrationOrder { get; }

    // Phase 7 追加フィールド
    public IReadOnlyList<IShimArgumentMatcher>? ArgumentMatchers { get; }
    // null = 引数を問わず一致（後方互換）

    internal bool MatchesArgs(object?[] args)
    {
        if (ArgumentMatchers is null) return true;
        if (args.Length != ArgumentMatchers.Count) return false;
        return args.Zip(ArgumentMatchers)
                   .All(pair => pair.Second.Matches(pair.First));
    }
}
```

### 5.2 `TryFindNewRuleWithArgs` の追加

```csharp
public bool TryFindNewRuleWithArgs(Type targetType, object?[] args, out NewShimRule? rule)
{
    lock (_syncRoot)
    {
        if (!_newRules.TryGetValue(targetType, out var rules))
        {
            rule = null;
            return false;
        }
        // 登録順の逆順（後から登録したルールが優先）でマッチングを試みる
        rule = rules.OrderByDescending(r => r.RegistrationOrder)
                    .FirstOrDefault(r => r.MatchesArgs(args));
        return rule is not null;
    }
}
```

現在 `_newRules` は `Dictionary<Type, NewShimRule>`（1 型 1 ルール）。引数対応では
1 型に複数ルール（overload ごと）を持てるよう `Dictionary<Type, List<NewShimRule>>` へ変更が必要。

これは **既存 public API の破壊的変更にはならない**（`ShimRuleRegistry` の内部実装変更のみ）。
ただし、複数ルール登録の優先順位ルールを明確にする必要があります。

## 6. argument matcher の設計

### 6.1 MiniMockito core の matcher との関係

`MiniMockito.Shims.Experimental` は `MiniMockito` の public API を使用できます
（逆方向は禁止）。したがって、shim 側で `MiniMockito` の `ArgumentMatcher<T>` を
そのまま使うことは技術的には可能です。

しかし以下の理由から **shim 専用の matcher インターフェース** を定義することを推奨します：

- `MiniMockito` の matcher は invocation 記録・検証フローと結びついており、shim の
  コンストラクタ引数マッチングとは責務が異なる
- shim パッケージを `MiniMockito` 本体への依存なしで使えるようにしておきたい
  （将来の分離可能性）
- shim の API は experimental であり、matcher の仕様を shim 側で制御したい

### 6.2 IShimArgumentMatcher インターフェース案

```csharp
namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Matches a single constructor argument in a shim rule.
/// </summary>
public interface IShimArgumentMatcher
{
    bool Matches(object? actual);
}

/// <summary>
/// Matches any value of type T.
/// </summary>
public sealed class ShimAnyMatcher<T> : IShimArgumentMatcher
{
    public bool Matches(object? actual)
        => actual is T or null;
}

/// <summary>
/// Matches a specific value using Equals.
/// </summary>
public sealed class ShimEqMatcher<T> : IShimArgumentMatcher
{
    private readonly T? _expected;
    public ShimEqMatcher(T? expected) => _expected = expected;
    public bool Matches(object? actual)
        => EqualityComparer<T>.Default.Equals(_expected, actual is T t ? t : default);
}

/// <summary>
/// Matches using a custom predicate.
/// </summary>
public sealed class ShimPredicateMatcher<T> : IShimArgumentMatcher
{
    private readonly Func<T?, bool> _predicate;
    public ShimPredicateMatcher(Func<T?, bool> predicate) => _predicate = predicate;
    public bool Matches(object? actual)
        => actual is T t ? _predicate(t) : _predicate(default);
}
```

### 6.3 ShimMatch ファクトリ（API の入口）

```csharp
public static class ShimMatch
{
    public static IShimArgumentMatcher Any<T>()
        => new ShimAnyMatcher<T>();

    public static IShimArgumentMatcher Eq<T>(T value)
        => new ShimEqMatcher<T>(value);

    public static IShimArgumentMatcher Is<T>(Func<T?, bool> predicate)
        => new ShimPredicateMatcher<T>(predicate);
}
```

## 7. Captor の設計

### 7.1 ShimCaptor（コンストラクタ引数のキャプチャ）

```csharp
public sealed class ShimCaptor<T> : IShimArgumentMatcher
{
    private readonly List<T?> _captured = new();

    public bool Matches(object? actual)
    {
        _captured.Add(actual is T t ? t : default);
        return true; // キャプチャは常に一致とみなす
    }

    /// <summary>
    /// Gets the last captured value.
    /// </summary>
    public T? Value => _captured.Count > 0 ? _captured[^1] : default;

    /// <summary>
    /// Gets all captured values in order.
    /// </summary>
    public IReadOnlyList<T?> AllValues => _captured.AsReadOnly();
}

public static class ShimCaptor
{
    public static ShimCaptor<T> Of<T>() => new();
}
```

## 8. 候補 API 案

### 8.1 Option A: Fluent with argument matchers

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .WithArguments(ShimMatch.Any<string>())
        .Returns(fakeRepository);
}
```

`WithArguments()` は `IShimArgumentMatcher[]` を受け取り、`NewShimRule` に
`ArgumentMatchers` を設定した後、`Returns()` を呼べる builder を返す。

**利点:** 型安全、既存の fluent 構文と整合的
**欠点:** 引数の型が静的にわからないと使いにくい

### 8.2 Option B: Factory with NewCreationContext

```csharp
Shim.New<UserRepository>()
    .Returns(ctx =>
    {
        var connectionString = ctx.Arguments.Get<string>(0);
        return new FakeUserRepository(connectionString);
    });
```

`NewCreationContext` は引数を型付きで取り出すアクセサを持つ。

```csharp
public sealed class NewCreationContext
{
    public NewCreationContext(object?[] args) => Arguments = new ShimArgumentList(args);
    public ShimArgumentList Arguments { get; }
}

public sealed class ShimArgumentList
{
    private readonly object?[] _args;
    public ShimArgumentList(object?[] args) => _args = args;

    public T? Get<T>(int index)
        => _args[index] is T t ? t : default;
}
```

**利点:** 引数の型を実行時に取り出せる、factory 内で柔軟な分岐が可能
**欠点:** 静的型安全性がない

### 8.3 Option C: Typed generic overloads（シグネチャ固定）

```csharp
Shim.New<UserRepository, string>()      // <TTarget, TArg1>
    .WithArgument(s => s != null)       // TArg1 に対する predicate
    .Returns(fakeRepository);
```

**利点:** 型安全、引数型をコンパイル時に明示できる
**欠点:** arity ごとに別メソッド/クラスが必要（組み合わせ爆発）

### 8.4 推奨

**Phase 7 最初の実装は Option B（NewCreationContext）から始める。**

理由：
- 引数の個数や型が実行時に決まるため、柔軟性が最優先
- matchers の設計が固まった後、Option A（fluent matchers）を追加できる
- Option C（typed generic overloads）はユーザーフィードバック次第で将来追加

また、Captor は Option A / B の両方から使用できる形にする：

```csharp
var captor = ShimCaptor.Of<string>();

Shim.New<UserRepository>()
    .WithArguments(captor)     // captor は IShimArgumentMatcher を実装
    .Returns(fakeRepository);

// 呼び出し後
Assert.AreEqual("prod", captor.Value);
```

## 9. overload constructor の扱い

複数の constructor を持つ型に対して複数のルールを登録できる。

```csharp
Shim.New<UserRepository>()                         // .ctor()
    .Returns(fakeDefault);

Shim.New<UserRepository>()
    .WithArguments(ShimMatch.Any<string>())        // .ctor(string)
    .Returns(fakeWithConnectionString);

Shim.New<UserRepository>()
    .WithArguments(
        ShimMatch.Any<string>(),
        ShimMatch.Any<int>())                       // .ctor(string, int)
    .Returns(fakeWithTimeout);
```

**マッチング優先順位の設計案:**
- 登録順の逆順（後から登録したルールが優先）
- または、引数の特異性スコア（specific matcher > Any matcher）
- Phase 7 初期: 登録順逆優先で実装し、フィードバック後に調整

**rewriter 側の設計:**
- コンストラクタシグネチャごとに別のラッパーメソッドを生成
- dispatcher は `(TargetType, args.Length, argTypes)` でルールを探す
  → ただし引数型での絞り込みは複雑なため、初期は `(TargetType, args)` で matcher を逐次評価

## 10. value type / reference type 引数

### value type

```csharp
public UserRepository(int timeout) { }
```

IL: `ldc.i4 5` (int を push) → `newobj .ctor(int32)` でスタックから消費

ラッパーメソッドで boxing を行う：

```text
; ラッパーメソッド内
ldarg.0          ; int32
box int32        ; → object
stelem.ref
```

`ShimDispatcher.NewWithArgs<T>(object?[])` は `object?[]` で受け取るため、
boxing はラッパー内でのみ必要で dispatcher は型に依存しない。

### reference type

Boxing 不要。参照をそのまま `stelem.ref` で配列に格納する。

### Nullable<T>

`Nullable<int>` は値型として扱われる。`box Nullable<int>` で boxing すると
`null` の場合は `null` オブジェクトになる（CLR の仕様）。dispatcher 側でそのまま
`object?[]` に含められる。

## 11. null 引数

```csharp
new UserRepository(null)      // string? 引数に null を渡す
new UserRepository((string?)null)
```

IL: `ldnull`, `newobj .ctor(string)`

null 参照は `ldnull` でスタックにプッシュされ、boxing は不要。ラッパー側でも
単純に `stelem.ref` で格納できる。matcher 側で null チェックが必要：

```csharp
// ShimEqMatcher<T>.Matches の場合
EqualityComparer<T>.Default.Equals(_expected, actual is T t ? t : default)
// null の場合 actual is T t が false になるため、default (null for reference types) を使う
```

`ShimAnyMatcher<T>` は `null` も一致させる（`actual is T or null`）。

## 12. params / optional parameter

### params

```csharp
public UserRepository(string name, params string[] tags) { }
```

IL 上では `params` は展開されている。call site によって引数個数が異なる。

**Phase 7 では対象外とする。** 理由：
- 配列の `newarr` + `stelem` が `newobj` の前に多数挿入され、引数境界が不明確
- ラッパーメソッドのシグネチャが変動し、型引数の特定が複雑
- unsupported pattern として診断に記録する

### optional parameter

```csharp
public UserRepository(string name = "default") { }
```

call site で引数を省略すると、コンパイラがデフォルト値を埋め込む。
IL 上では省略されず `ldstr "default"` が挿入されるため、実質 1 引数と同じ。
**対応可能**（ただし、省略された場合でもコンパイル済みのデフォルト値が使われるため
runtime での "省略" は検出できない）。

## 13. generic argument

### 非ジェネリッククラスのジェネリック引数型

```csharp
public UserRepository(IEnumerable<string> items) { }
```

`IEnumerable<string>` は閉じた generic 型。IL のメタデータトークンで解決可能。
ラッパーメソッドのシグネチャに `IEnumerable<string>` を使えばよく、**対応可能**。

### ジェネリッククラス

```csharp
public class Repository<T>
{
    public Repository(T item) { }
}
```

ジェネリッククラス自体は Phase 4 以来の対象外。引き続き `GenericTypeNotSupported`
として skip する。

### ジェネリックメソッド引数

```csharp
public UserRepository(Action<T> handler) where T : ... // 型パラメータが含まれる場合
```

open generic parameter が絡む場合は現時点では対象外。

## 14. unsupported pattern 一覧

| Pattern | 理由 | 診断コード |
|---------|------|-----------|
| generic target class | ジェネリック型の rewrite は未対応 | `GenericTypeNotSupported` |
| `params` parameter | call site のシグネチャが不定 | `ParamsParameterNotSupported` |
| `ref` / `out` / `in` parameter | stack の方向が逆転する / 値返しが必要 | `RefParameterNotSupported` |
| `__arglist` | 可変長引数リスト（古い方式） | `ArglistParameterNotSupported` |
| unsafe pointer parameter | `void*` 等はマネージドで扱えない | `UnsafePointerParameterNotSupported` |
| non-public constructor | 外部から参照できない | `ConstructorIsNotPublic` |
| BCL / .NET runtime type | 対象外 | `BclTypeNotSupported` |
| abstract / interface type | インスタンス化不可 | `AbstractTypeNotSupported` |
| open generic parameter in arg | 型が確定しない | `OpenGenericArgumentNotSupported` |

## 15. diagnostics 設計

### 15.1 スキャン時のメッセージ強化

現在の Phase 4/5 では `ConstructorArgumentsNotSupported` のみ。Phase 7 では細分化する。

```text
New interception target has unsupported constructor.
Target type: Sample.UserRepository
Constructor: .ctor(System.String)
Calling assembly: Sample.Tests
Calling method: Sample.UserService.GetDisplayName
Rewrite mode: TestOutputAssemblyRewrite
Reason: ConstructorArgumentsNotSupported
Supported patterns:
  public non-generic class
  public parameterless constructor
  public constructor with value/reference type arguments (Phase 7+)
Unsupported patterns:
  params parameter
  ref / out / in parameter
  __arglist
  unsafe pointer parameter
Hint: Use ShimMatch.Any<string>() or Returns(ctx => ...) to match constructor arguments.
```

### 15.2 Wrapper Method 生成の diagnostic ログ

rewriter が wrapper method を生成した場合：
```text
Generated wrapper method __ShimWrap_UserRepository_String in UserService for .ctor(string).
Rewrote Sample.UserService.GetDisplayName IL_0005:
  new UserRepository("prod") -> ShimDispatcher.NewWithArgs<UserRepository>(["prod"])
```

## 16. テスト方針

### 16.1 Phase 7 実装前に追加するテスト（設計検証）

現在の「`ConstructorArgumentsNotSupported` が診断に記録される」テストは既にあります。
Phase 6 では以下を dry-run scanner の範囲で追加します：

```csharp
[TestMethod]
public void Scan_ReportsConstructorArgsDetailedReason()
{
    // UserRepository.ctor(string) が "ConstructorArgumentsNotSupported" として報告される
    var report = AssemblyRewriteScanner.Scan(
        typeof(UserService).Assembly.Location,
        new NewObjScanOptions { TargetTypes = [typeof(UserRepository)] });

    var callSite = report.UnsupportedCallSites
        .First(cs => cs.TargetTypeName.Contains("UserRepository"));

    Assert.AreEqual("ConstructorArgumentsNotSupported", callSite.UnsupportedReason);
    StringAssert.Contains(callSite.TargetConstructor, "String");
}
```

### 16.2 Phase 7 実装後に追加するテスト

**単体テスト（ShimRuleRegistry）:**
- `TryFindNewRuleWithArgs` が `ArgumentMatchers` なしのルールに一致する
- `TryFindNewRuleWithArgs` が `ShimAnyMatcher<string>` に `"prod"` が一致する
- `TryFindNewRuleWithArgs` が `ShimEqMatcher<string>("prod")` に `"other"` が一致しない
- overload ルールが引数数で区別される

**統合テスト（AssemblyRewriter + ShimDispatcher）:**
- `new UserRepository("prod")` が rewrite され、fake instance が返る
- `new UserRepository("prod")` が rewrite され、`NewCreationContext` で `"prod"` を取り出せる
- ShimCaptor が constructor 引数をキャプチャする
- value type 引数（`int`）が正しく boxing されて dispatcher に渡る
- null 引数が正しく扱われる
- `[DoNotParallelize]` が適用されている

**エラーケースのテスト:**
- `params` 引数は `ParamsParameterNotSupported` として skip される
- `ref` 引数は `RefParameterNotSupported` として skip される
- overload のうち対応できるコンストラクタのみが rewrite され、非対応は skip される

## 17. 実装難易度評価

| 項目 | 難易度 | 備考 |
|------|--------|------|
| Wrapper method 生成（Mono.Cecil） | 中 | 既存 `NewObjRewriter` の拡張 |
| `ShimDispatcher.NewWithArgs<T>(object?[])` | 低 | `New<T>()` と構造が同じ |
| `ShimRuleRegistry` の multi-rule 対応 | 中 | `Dictionary<Type, List<...>>` への変更 |
| `NewShimRule` への `ArgumentMatchers` 追加 | 低 | フィールド追加のみ |
| `IShimArgumentMatcher` / `ShimMatch` | 低〜中 | 独立した新 API |
| `ShimCaptor<T>` | 低 | 単純な wrapper |
| value type boxing（rewriter 内） | 低〜中 | `box` opcode 挿入 |
| `NewCreationContext` / factory overload | 低 | `Returns(Func<NewCreationContext, T>)` の追加 |
| overload 優先順位ロジック | 中 | 優先順位ルールの設計・テストが必要 |
| `params` 対応 | 高 | 初期 Phase 7 では対象外を推奨 |
| `ref` / `out` / `in` 対応 | 高 | spill/restore が必要、Phase 7 では対象外 |

**全体難易度:** parameterless の時よりも大幅に高い。
Phase 7 の最小実装（value / reference 型の単純引数のみ）であれば「中程度」。
`params` / `ref` 等を含む全対応は「高い」。

## 18. 最小対応スコープ（Phase 7 推奨）

Phase 7 で対応すること（最小 PoC）：

- public non-generic class
- public constructor with 1〜3 個の引数
- 引数の型が value type または reference type（boxing 対応）
- `ShimDispatcher.NewWithArgs<T>(object?[])`
- ラッパーメソッドの Mono.Cecil 生成
- `NewCreationContext` による factory API
- `ShimMatch.Any<T>()`, `ShimMatch.Eq<T>()` の最小 matcher
- `ShimCaptor<T>` の最小実装
- `[DoNotParallelize]` 付き MSTest

Phase 7 で対応しないこと：

- `params` 引数
- `ref` / `out` / `in` 引数
- `__arglist`
- unsafe pointer
- optional parameter のデフォルト値追跡
- generic target class
- BCL type
- runtime IL rewrite / CLR Profiling / detour

## 19. 対応しないほうがよい範囲

**永続的に対応しない（本パッケージの設計方針外）:**

- BCL / .NET runtime type のコンストラクタ差し替え
- `sealed` / `static` class のコンストラクタ
- runtime IL rewrite（JIT 後のパッチ）
- CLR Profiling API ベースの差し替え
- detour / method patching
- production assembly の in-place rewrite
- unsafe pointer 引数（セキュリティリスク大）

**将来検討（現時点では実装しない）:**

- `params` 引数（複雑度が高く、初期 PoC の範囲外）
- `ref` / `out` / `in` 引数（スタック変換が逆方向で複雑）
- generic class のコンストラクタ
- 3 引数超のコンストラクタ（PoC 段階では 1〜3 引数に絞る）

## 20. 依存関係と境界

```
MiniMockito.Shims.Experimental
  ├── ShimDispatcher.NewWithArgs<T>()
  ├── ShimRuleRegistry（multi-rule 対応）
  ├── IShimArgumentMatcher
  ├── ShimMatch
  ├── ShimCaptor<T>
  └── NewCreationContext
      └── ShimArgumentList

Rewrite/
  ├── NewObjRewriter（ラッパー生成追加）
  ├── AssemblyRewriter（多引数対応）
  └── AssemblyRewriteScanner（細分化された unsupported reason）
```

`MiniMockito.Shims.Experimental` は `MiniMockito` 本体の `ArgumentMatcher<T>` を
直接使用しません。shim 専用の `IShimArgumentMatcher` を定義します。

---

## Phase 8 実装ノート（WithArguments matcher API）

> **Experimental.** このセクションの API は experimental です。将来のフェーズで変更される可能性があります。

### 実装済み API

#### `IShimArgumentMatcher` インターフェース

```csharp
public interface IShimArgumentMatcher
{
    Type? ExpectedType { get; }
    bool Matches(object? value);
    string Describe();
}
```

#### `ShimArg` ファクトリ（フルネーム必須の現時点）

```csharp
// 任意の T 型値に一致
ShimArg.Any<T>()

// EqualityComparer<T>.Default で一致
ShimArg.Eq<T>(T? value)

// Predicate が true を返したら一致
ShimArg.Is<T>(Func<T?, bool> predicate)
```

#### `NewShimBuilder<T>.WithArguments`

```csharp
Shim.New<UserRepository>()
    .WithArguments(ShimArg.Any<string>())
    .Returns(fakeRepository);

Shim.New<UserRepository>()
    .WithArguments(ShimArg.Eq("prod"))
    .Returns(fakeRepository);

Shim.New<UserRepository>()
    .WithArguments(
        ShimArg.Eq("prod"),
        ShimArg.Any<int>())
    .Returns(fakeRepository);
```

### 仕様一覧

| 仕様項目 | 内容 |
|---------|------|
| `WithArguments` なし | catch-all rule。任意の引数リストに一致する |
| `WithArguments()` 空配列 | 引数 count = 0 の場合のみ一致（parameterless constructor）|
| 複数 rule が一致した場合 | 後から登録した rule を優先（Mockito 風「last stub wins」）|
| `WithArguments` あり rule と catch-all rule が両方一致 | 登録順に従う。後から登録した rule を優先 |
| no match 時 | 実 constructor fallback（`Activator.CreateInstance`）|
| `Any<T>()` の null | reference type / `Nullable<T>` は null に一致。non-nullable value type は null に一致しない |
| value type boxing | 生成ラッパーメソッド内で box してから dispatcher に渡す。matcher は `actual is T` / `EqualityComparer<T>` で unbox する |

### 後から登録した rule が優先される理由

Mockito の「後から書いた stub が前の stub を上書きする」感覚に合わせています。
テストコードの後半に書かれた設定が「より具体的・最新の意図」を表すため、
後から登録した rule が優先される方が読みやすいテストコードになります。

### `WithArguments` を複数回呼んだ場合

同一 builder オブジェクトに対して `WithArguments` を複数回呼んだ場合は、
最後の呼び出しで上書きされます。

```csharp
Shim.New<Foo>()
    .WithArguments(ShimArg.Eq("a"))  // 上書きされる
    .WithArguments(ShimArg.Eq("b"))  // こちらが有効
    .Returns(fake);
```

### 対象外

- static method mocking
- BCL type 差し替え
- generic class / generic constructor
- ref / out constructor arguments
- params / optional parameter の高度対応
- expression tree matcher
- production assembly in-place rewrite
