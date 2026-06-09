# Shims AssemblyLoadContext Isolation Design

## 1. 目的

このドキュメントは `MiniMockito.Shims.Experimental` における rewritten assembly 実行方式に対して、
assembly 汚染・型衝突・ファイルロック・unload 不可・test 間干渉を減らすための
`AssemblyLoadContext` (ALC) 分離方針を設計します。

**Phase 11 では本格実装を行いません。** 設計・リスク・API・テスト方針の整理のみを行います。

---

## 2. 現状の assembly loading 問題

### 2.1 現在の実装概要

```
AssemblyRewriter.RewriteNewObj(inputPath, outputPath, options)
  → temp ディレクトリへ rewritten assembly を書き出す

RewrittenAssemblyLoader(rewrittenPath)
  → new RewrittenAssemblyLoadContext(rewrittenPath) { isCollectible: true }
  → context.LoadFromAssemblyPath(rewrittenPath)

RewrittenAssemblyLoadContext.Load(assemblyName):
  1. ShimDispatcher の assembly → default ALC の assembly を返す（共有）
  2. AssemblyDependencyResolver.ResolveAssemblyToPath(assemblyName)
     → null（temp dir に deps.json がない）
  3. null を返す → .NET が parent（default ALC）へ fallback
```

### 2.2 現状の問題一覧

| 問題 | 詳細 | 深刻度 |
|------|------|--------|
| ファイルロック | `LoadFromAssemblyPath` は Windows でファイルをロックする。`Unload()` 後も GC 完了まで解放されない | 中 |
| unload 非決定性 | `Unload()` は GC ベース。finalization が完了するまで ALC と assembly は生き続ける | 中 |
| unload 確認手段がない | unload 成功・失敗を検出する仕組みがない | 中 |
| AssemblyDependencyResolver の不完全な機能 | temp dir に `.deps.json` がないため resolver は機能せず、すべて parent ALC fallback に頼る | 低 |
| 型 identity 問題 | isolated ALC の `UserRepository` と default ALC の `UserRepository` は別 `Type` オブジェクト | 高 |
| fake instance 型不一致 | default ALC 側で作った fake を isolated ALC のメソッドに型安全に渡せない | 高 |
| ShimRuleRegistry キー不一致リスク | `Shim.New<UserRepository>()` は default ALC の型でキー登録するが、rewritten assembly から呼ばれる dispatcher は isolated ALC の型を参照する | 高 |
| test 間 state 残留 | ALC が GC されるまで temp ファイルが残る。次の test が同 path に書こうとするとファイルロック衝突 | 低（GUID 回避済み）|
| parallel test での干渉 | `ShimRuleRegistry` は process-wide。parallel test で複数 ALC が存在するとルール衝突のリスク | 高（既定無効化で緩和済み）|
| coverage / PDB ずれ | rewritten assembly は元の PDB と IL が一致しないため、coverage と debugger でソース行がずれる | 低（テスト限定のため許容）|

### 2.3 現在の型 identity 問題の詳細

```text
default ALC:
  MiniMockito.Shims.Experimental.Sample.dll（元の test output）
    UserRepository (Type A)
    UserService (Type A)

isolated ALC (RewrittenAssemblyLoadContext):
  /tmp/MiniMockito.Shims.Experimental/.../MiniMockito.Shims.Experimental.Sample.dll
    UserRepository (Type B)  ← Type A とは別オブジェクト
    UserService (Type B)
  MiniMockito.Shims.Experimental.dll → default ALC の同 assembly を共有
    ShimDispatcher (Type A == Type B)  ← 共有されている
    ShimRuleRegistry (Type A == Type B)  ← 共有されている
```

**重要:** `ShimDispatcher.New<T>()` は rewritten assembly から呼ばれるため、`T` は isolated ALC の型 (Type B)。

`Shim.New<UserRepository>().Returns(fake)` は default ALC の `typeof(UserRepository)` (Type A) でルールを登録する。

Type A ≠ Type B なので、dispatcher がルールを見つけられない。

**現状の回避策 (`NewInterceptionHarness`):**

```csharp
public void RegisterShim<TTarget>(object fakeInstance) where TTarget : class
{
    var rewrittenType = GetRewrittenType(typeof(TTarget)); // isolated ALC の型 (Type B)
    context.Registry.RegisterNewRule(rewrittenType, () => fakeInstance, context.ContextId);
}
```

`GetRewrittenType` で isolated ALC の型を取得してルール登録することで回避している。

---

## 3. collectible AssemblyLoadContext 案

### 3.1 基本設計

```csharp
/// <summary>
/// Isolated, collectible AssemblyLoadContext for rewritten assembly execution.
/// </summary>
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _originalAssemblyDirectory;

    public ShimAssemblyLoadContext(string rewrittenAssemblyPath, string originalAssemblyDirectory)
        : base(name: $"ShimIsolated-{Path.GetFileNameWithoutExtension(rewrittenAssemblyPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(rewrittenAssemblyPath);
        _originalAssemblyDirectory = originalAssemblyDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 1. ShimDispatcher など experimental 本体は常に default ALC を共有
        var shimAssembly = typeof(ShimDispatcher).Assembly;
        if (AssemblyName.ReferenceMatchesDefinition(shimAssembly.GetName(), assemblyName))
            return shimAssembly;

        // 2. rewritten output dir の deps.json から解決を試みる
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is not null)
            return LoadFromAssemblyPath(path);

        // 3. original test output directory から解決を試みる（shadow copy 方式）
        var candidate = Path.Combine(_originalAssemblyDirectory, assemblyName.Name + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        // 4. null を返して parent (default ALC) への fallback を許可
        return null;
    }
}
```

### 3.2 dependency resolution 戦略

#### 方式 A: parent ALC fallback（現状）

**方針:** `Load()` で `null` を返して default ALC へ fallback する。

**問題:**
- sample assembly の依存ライブラリが isolated ALC からロードされるが、
  それらの型が default ALC の型と同一になる（Type A == Type B）
- これは望ましい挙動だが、意図的な設計でないためわかりにくい

**適用範囲:** 依存ライブラリを isolated ALC で重複ロードしたくない場合に推奨

#### 方式 B: shadow copy + original directory

**方針:**
1. rewritten assembly を temp ディレクトリに出力する際、deps.json と依存 DLL もコピーする
2. `AssemblyDependencyResolver` が temp dir の deps.json を正しく読める

**問題:**
- コピーするファイル数が増える（test output dir のすべての DLL）
- コピー漏れで `TypeLoadException` が発生するリスク
- shadow copy が完了するまで時間がかかる

**適用範囲:** 依存ライブラリも isolated ALC でロードしたい場合（型分離を徹底したい場合）

#### 方式 C: original directory を probing path に追加（推奨）

**方針:**
```csharp
protected override Assembly? Load(AssemblyName assemblyName)
{
    // shimDispatcher は常に共有
    // ...

    // rewritten assembly dir を試みる
    var path = _resolver.ResolveAssemblyToPath(assemblyName);
    if (path is not null) return LoadFromAssemblyPath(path);

    // original test output dir を試みる（コピー不要）
    var candidate = Path.Combine(_originalAssemblyDirectory, assemblyName.Name + ".dll");
    if (File.Exists(candidate)) return LoadFromAssemblyPath(candidate);

    return null; // parent ALC fallback
}
```

**利点:**
- ファイルコピー不要
- test output dir の DLL をそのまま参照
- `ShimDispatcher` assembly は必ず default ALC のものを共有

**注意:**
- original dir の DLL をロードすると、それらの型は isolated ALC の型になる
- sample assembly と同じ dir の他の DLL が意図せず isolated ALC にロードされる可能性

### 3.3 ShimDispatcher assembly の共有

**原則:** `MiniMockito.Shims.Experimental.dll` は常に default ALC から共有する。

理由:
- `ShimDispatcher` が dispatch するとき、`ShimContext` / `ShimRuleRegistry` は default ALC の型
- isolated ALC でも default ALC でも `ShimDispatcher.New<T>()` の内部は同じ instance を参照する必要がある
- 別ロードすると `ShimRuleRegistry` が 2 つになり、ルールが見つからない

実装:
```csharp
// experimentalAssembly 名を whitelist しておく
private static readonly AssemblyName[] _sharedAssemblies =
[
    typeof(ShimDispatcher).Assembly.GetName(),
    typeof(object).Assembly.GetName(), // System.Runtime
];

protected override Assembly? Load(AssemblyName assemblyName)
{
    foreach (var shared in _sharedAssemblies)
    {
        if (AssemblyName.ReferenceMatchesDefinition(shared, assemblyName))
            return null; // null = parent (default ALC) にまかせる
    }
    // ...
}
```

`null` を返すと parent ALC に fallback するため、`return shimAssembly` と `return null` は
default ALC への解決という点で同じだが、`null` 方式がより一般的なパターン。

### 3.4 PDB loading

rewritten assembly には元の PDB が対応しないため、PDB は読み込まない方針とする。

```csharp
var readerParams = new ReaderParameters { ReadSymbols = false };
using var module = ModuleDefinition.ReadModule(inputPath, readerParams);
// ...
module.Write(outputPath, new WriterParameters { WriteSymbols = false });
```

Phase 7 以降の rewriter はすでに `ReadSymbols = false` でこれを行っている。
ALC 側でも PDB を明示的にロードする必要はない。

---

## 4. Harness API 案

### 4.1 現状の NewInterceptionHarness

現状の `NewInterceptionHarness` は以下の責務を持つ:

- allowlist 管理 (`WithTarget<T>`)
- assembly rewrite + load (`RewriteTargetTypeAssembly`, `RewriteAssembly`)
- isolated ALC の `Type` 取得 (`GetRewrittenType`)
- isolated ALC からの instance 作成 (`Create<T>`, `CreateFake<T>`)
- rule 登録 (`RegisterShim<T>`)
- reflection 経由の method invoke (`Invoke<TResult>`)
- Dispose 時の ALC unload

### 4.2 改善すべき問題

1. unload 成否の確認手段がない
2. `AssemblyDependencyResolver` が temp dir の deps.json なしで機能しない
3. unload 後も GC 前はファイルロックが残る
4. original assembly directory の dep 解決が不安定

### 4.3 Phase 12 で検討する API 案

#### Option A: ALC isolation をデフォルト化（推奨）

```csharp
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly()
    .LoadInIsolatedContext(); // 新 API: ALC を明示的に分離

using (ShimContext.Create())
{
    var fake = harness.CreateFake<UserRepository>("prefix");
    harness.RegisterShim<UserRepository>(fake);

    var service = harness.Create<UserService>();
    var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);
}
// harness.Dispose() → ALC unload 開始
// harness.VerifyUnloaded() → WeakReference で unload 確認（オプション）
```

#### Option B: オプションで有効化

```csharp
var harness = NewInterceptionHarness.Create(options =>
{
    options.IsolateAssemblyLoadContext = true;
    options.OriginalAssemblyDirectory = typeof(UserService).Assembly.Location;
});
```

#### Option C: 現状維持（ALC は内部実装）

harness の外部 API を変えず、内部で dependency resolution を改善する。

**推奨:** Option A / Option C の組み合わせ。内部で ALC isolation は既にしており、
`LoadInIsolatedContext()` は optional fluent step として追加し、dependency resolution 方式を
Phase 12 で改善する。

### 4.4 harness lifetime とリソース管理

```csharp
// harness は IDisposable
// Dispose():
//   1. _loader.Dispose() → context.Unload()
//   2. _assembly = null
//   3. _loader = null

// 注意: Dispose() 後も GC が完了するまで ALC は生きている
// WeakReference を保持して unload 確認する（詳細は Section 6）
```

### 4.5 weakly typed harness API

型 identity 問題のため、strongly typed API は制限がある。
現状の `Invoke<TResult>` に加えて以下を検討:

```csharp
// 既存: 型安全でないが実用的
TResult Invoke<TResult>(object instance, string methodName, params object[] args)

// 追加案: Type 取得 API
Type GetRewrittenType(Type originalType) // 既存

// 追加案: 型名文字列ベース API（型 identity を完全に回避）
object CreateByName(string typeFullName, params object[] args);
TResult InvokeByName<TResult>(object instance, string methodName, params object[] args);
```

---

## 5. ShimContext との関係

### 5.1 ShimContext の現状

`ShimContext` は `AsyncLocal<ShimContext?>` を使い、async-flow 単位でアクティブな context を管理する。
`ShimRuleRegistry` は各 `ShimContext` インスタンスに属するが、**registry のキーは `Type` オブジェクト**。

### 5.2 ShimRuleRegistry は process-wide か？

`ShimRuleRegistry` は各 `ShimContext` インスタンスが独自に持つ（process-wide ではない）。
ただし、`ShimContext` は `AsyncLocal` で管理されており、`ShimDispatcher` は `ShimContext.Current` を参照する。

→ `ShimDispatcher` は常に default ALC の assembly からロードされており、
  `ShimContext.Current` (AsyncLocal) も default ALC の `ShimContext` を参照する。
  **process-wide な state** は `ShimContext._currentContext` (AsyncLocal の backing store) のみ。

### 5.3 rewritten assembly から呼ばれる ShimDispatcher

```
rewritten assembly (isolated ALC):
  new UserRepository("prod")
  → call ShimDispatcher.NewWithArgs<UserRepository>(["prod"])
     ↑ この ShimDispatcher は default ALC の assembly から解決される
       （RewrittenAssemblyLoadContext.Load で明示的に return shimAssembly）
```

**結果:**
- `ShimDispatcher.New<UserRepository>()` の `typeof(T)` = isolated ALC の UserRepository 型
- `ShimContext.Current.Registry.TryFindNewRuleWithArgs(isolatedType, ...)` で検索
- ルールが `isolatedType` でキー登録されていれば match する

### 5.4 ALC を増やした場合の ShimContext 分離案

複数の isolated ALC を同時に使う場合（並列テスト）、
`ShimContext` を ALC ごとに分離すれば干渉を防げる可能性がある。

```csharp
// 案: ALC スコープ付き ShimContext（Phase 12 以降の課題）
using var alcContext = ShimAlcContext.Create(alcInstance);
using (ShimContext.Create(alcContext))
{
    // このコンテキストは alcInstance 内のルールのみに適用
}
```

**Phase 11 では設計のみ。** Phase 12 は `[DoNotParallelize]` 前提でこの分離は不要。

### 5.5 MiniMockito.Shims.Experimental assembly のロード先

**原則:** default ALC のみ。isolated ALC には別ロードしない。

理由:
- 別ロードすると `ShimContext` / `ShimRuleRegistry` / `ShimDispatcher` が二重になる
- AsyncLocal の backing store が ALC ごとに分離されてしまう
- ルール登録と dispatch が別の registry を見ることになる

実現方法:
```csharp
protected override Assembly? Load(AssemblyName assemblyName)
{
    if (assemblyName.Name == "MiniMockito.Shims.Experimental")
        return null; // parent (default ALC) fallback
    // ...
}
```

---

## 6. 型 identity 問題

### 6.1 問題の本質

```text
default ALC で typeof(UserRepository) → Type A
isolated ALC で typeof(UserRepository) → Type B

Type A != Type B（同じ full name だが異なる CLR Type）

registry key = Type B のルールを
Shim.New<UserRepository>().Returns(fake) で Type A として登録すると
dispatcher が見つけられない
```

### 6.2 現状の対処: NewInterceptionHarness.RegisterShim

```csharp
// 正しい登録（harness 経由）
var rewrittenType = GetRewrittenType(typeof(UserRepository)); // Type B
context.Registry.RegisterNewRule(rewrittenType, () => fakeInstance, contextId);

// 誤った登録（直接 API）
// Shim.New<UserRepository>().Returns(fakeInstance);
// → typeof(UserRepository) = Type A でキー登録 → dispatcher が見つけられない
```

### 6.3 fake instance の型 identity

**fake instance が rewritten assembly のインスタンスである場合：**
```csharp
var fake = harness.CreateFake<UserRepository>("prefix");
// fake.GetType() = isolated ALC の UserRepository (Type B)
```

**fake instance が default ALC のインスタンスである場合：**
```csharp
var fake = new UserRepository("prefix"); // default ALC のコンストラクタを呼ぶ
// fake.GetType() = default ALC の UserRepository (Type A)
```

両者は型 identity が異なるが、**rewritten assembly 内でオブジェクトとして使うだけなら問題ない**。

問題が生じるのは:
- `fake is UserRepository` チェック（Type A vs Type B で false になる）
- cast (`(UserRepository)fake`) で `InvalidCastException`
- reflection 経由のメソッド呼び出し (`method.Invoke(fake, ...)`) は問題なし

### 6.4 fake instance の推奨作成方法

| シナリオ | 推奨方法 | 注意 |
|---------|---------|------|
| fake がシンプルな値オブジェクト | `harness.CreateFake<UserRepository>("prefix")` | isolated ALC の型になる |
| fake が interface を実装 | `Mock.Of<IUserRepository>()` で interface mock を使う | interface は共有できる可能性あり |
| fake が ShimConstructorContext factory | `Returns((ShimConstructorContext ctx) => ...)` | factory 内で isolated ALC の型を返す |
| strongly typed fake が必要 | default ALC に interface / abstract を抽出する | 設計変更が必要 |

### 6.5 strongly typed API を維持できるか

**限定的に可能。** 以下の条件を満たす場合:

- `IUserRepository` などの interface を定義し、`UserRepository` が実装する
- `Mock.Of<IUserRepository>()` で interface mock を作成
- shim rule の factory が interface 型を返す
- rewritten assembly のメソッドが interface 型で変数を受け取る（`var repo = new UserRepository()` は `UserRepository` で受け取るため不可）

**現実的なアプローチ:** `NewInterceptionHarness` の harness API を通じた weakly typed スタイル。
strongly typed API は interface-based の設計に移行した場合にのみ可能。

### 6.6 test code 側からの型取得

```csharp
// 方式 1: harness 経由（推奨）
var serviceType = harness.GetRewrittenType(typeof(UserService));
var service = Activator.CreateInstance(serviceType)!;

// 方式 2: assembly から直接
var assembly = loader.Load();
var serviceType = assembly.GetType("MiniMockito.Shims.Experimental.Sample.UserService")!;
var service = Activator.CreateInstance(serviceType)!;

// 方式 3: reflection-only style（型を保持しない）
var result = harness.Invoke<string>(service, "GetDisplayName", 1);
```

---

## 7. unload 戦略

### 7.1 現状の unload 実装

```csharp
// RewrittenAssemblyLoader.Dispose()
context.Unload(); // 非同期 GC ベース。即時 unload されない
```

`Unload()` は unload を「開始」するだけ。実際の unload は:
1. ALC への強参照がなくなる
2. ALC 内のすべての Assembly への強参照がなくなる
3. GC が実行される

その後、ALC が GC されてファイルロックが解放される。

### 7.2 WeakReference による unload 確認

```csharp
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    // ...

    public static bool TryWaitForUnload(WeakReference alcWeakRef, int maxGcCycles = 5)
    {
        for (int i = 0; i < maxGcCycles; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (!alcWeakRef.IsAlive)
                return true;
        }
        return false;
    }
}

// 使用例（テスト）
WeakReference? alcRef = null;

[MethodImpl(MethodImplOptions.NoInlining)]
void LoadAndUnload()
{
    var loader = new RewrittenAssemblyLoader(path);
    alcRef = new WeakReference(loader, trackResurrection: true);
    loader.Load();
    loader.Dispose();
}

LoadAndUnload();
Assert.IsTrue(ShimAssemblyLoadContext.TryWaitForUnload(alcRef));
```

**重要:** unload を妨げる強参照:
- `Type` オブジェクト（isolated ALC の型）
- `Assembly` オブジェクト
- `MethodInfo`, `FieldInfo` などの `MemberInfo`
- これらをフィールドやスタックに保持している場合、ALC は GC されない

### 7.3 GC.Collect の注意事項

test code で `GC.Collect()` を使うのは一般に推奨されないが、
ALC unload の検証目的では許容される。ただし:

- `[MethodImpl(MethodImplOptions.NoInlining)]` を付けたメソッド内で行う
  （JIT が参照をスタックに保持しないようにするため）
- test field に Type/Assembly を保持しないよう注意

### 7.4 unload 失敗時の診断

```csharp
public sealed class UnloadDiagnostics
{
    public bool UnloadAttempted { get; init; }
    public bool UnloadSucceeded { get; init; }
    public int GcCyclesWaited { get; init; }
    public string AlcName { get; init; } = string.Empty;
    public bool IsCollectible { get; init; }

    // unload 失敗の可能性のあるヒント
    public IReadOnlyList<string> Hints { get; init; } = [];

    public string Format() { ... }
}
```

unload 失敗時のヒント例:
```text
Unload failed after 5 GC cycles.
ALC name: ShimIsolated-MiniMockito.Shims.Experimental.Sample
Possible causes:
  - A Type from this ALC is stored in a field or local variable.
  - An Assembly reference is still live.
  - A MethodInfo or FieldInfo from this ALC is still referenced.
  - A delegate captured a closure that holds a reference to an ALC type.
Hint: Ensure that test class fields do not hold isolated ALC types.
Hint: Use harness.Invoke<T>() instead of storing MethodInfo references.
```

### 7.5 test class field に保持した場合の注意

```csharp
// 危険: ALC の型を test class field に保持すると unload できない
[TestClass]
public class DangerousTests
{
    private Type? _repoType; // isolated ALC の型 → ALC が GC されない！
    private Assembly? _assembly; // isolated ALC の assembly → GC されない！
}

// 安全: harness のみ保持
[TestClass]
public class SafeTests
{
    private NewInterceptionHarness? _harness; // harness のみ保持（内部は WeakReference で管理）
}
```

---

## 8. diagnostics

### 8.1 ALC 診断情報

Phase 12 で実装する `ShimAlcDiagnostics`:

```csharp
public sealed class ShimAlcDiagnostics
{
    public string AlcName { get; init; } = string.Empty;
    public bool IsCollectible { get; init; }
    public string RewrittenAssemblyPath { get; init; } = string.Empty;
    public string OriginalAssemblyPath { get; init; } = string.Empty;
    public IReadOnlyList<string> LoadedAssemblyNames { get; init; } = [];
    public IReadOnlyList<string> ResolvedDependencies { get; init; } = [];
    public IReadOnlyList<string> UnresolvedDependencies { get; init; } = [];
    public bool UnloadAttempted { get; init; }
    public bool UnloadSucceeded { get; init; }
    public string Format() { ... }
}
```

### 8.2 診断情報の取得タイミング

| タイミング | 取得できる情報 |
|-----------|--------------|
| Load 直後 | LoadedAssemblyNames, ResolvedDependencies, UnresolvedDependencies |
| rewrite 後 | RewrittenAssemblyPath |
| Dispose 後 | UnloadAttempted, UnloadSucceeded |
| GC 後 | 最終的な unload 成否 |

---

## 9. 推奨実装アーキテクチャ（Phase 12 向け）

### 9.1 全体構成

```text
NewInterceptionHarness (public API)
  ├── ShimAssemblyLoadContext (改善版 ALC、依存解決強化)
  │     ├── AssemblyDependencyResolver (original dir 対応)
  │     ├── 明示的な SharedAssemblies whitelist
  │     └── WeakReference による unload 確認
  ├── UnloadDiagnostics (unload 状態の報告)
  └── ShimAlcDiagnostics (ロード状態の詳細)

ShimDispatcher (default ALC、不変)
ShimContext / ShimRuleRegistry (default ALC、不変)
```

### 9.2 型 identity 問題の根本解決方針

Phase 12 では型 identity 問題を「完全解決」するのではなく、
**現状の回避策（`GetRewrittenType` 経由の rule 登録）を明文化し、
失敗しやすいパターンを使いやすいエラーメッセージで早期検出する** 方針とする。

根本解決（interface 化、dynamic dispatch など）は API 設計に影響するため Phase 13 以降に回す。

### 9.3 Phase 12 では実装しないこと

- ALC ごとの ShimContext 分離
- strongly typed fake instance の型安全な cast
- interface abstraction による型 identity 解消
- parallel test 安全性の保証
- production assembly in-place rewrite
- BCL type 差し替え
- static method mocking

---

## 10. フェーズ間の関係

| Phase | 内容 |
|-------|------|
| 4〜6 | parameterless newobj rewrite PoC |
| 7 | constructor arguments 対応 |
| 8 | WithArguments matcher API |
| 9 | ShimCaptor |
| 10 | API polish / diagnostics |
| 11 | ALC isolation 設計（本ドキュメント） |
| **12（実装済み）** | **ALC isolation PoC（本ドキュメント Section 12 参照）** |
| 13 | static method mocking 設計調査 |

---

## 12. Phase 12 実装ノート

### 12.1 実装した内容

Phase 12 で以下を実装した。

#### 新規ファイル

| ファイル | 内容 |
|---------|------|
| `src/MiniMockito.Shims.Experimental/ShimAssemblyLoadContext.cs` | 名前付き collectible ALC。dependency resolution diagnostics を記録。 |
| `src/MiniMockito.Shims.Experimental/ShimAlcDiagnostics.cs` | ALC ロード状態の診断スナップショット。`Format()` で人可読出力。 |
| `tests/MiniMockito.Shims.Experimental.Tests/Phase12AlcIsolationTests.cs` | 21 件の MSTest（ALC loading / shim integration / unload / regression）。 |

#### 更新ファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/MiniMockito.Shims.Experimental/Rewrite/RewrittenAssemblyLoader.cs` | `ShimAssemblyLoadContext` に切り替え。`_context` を非 readonly に変更し Dispose 時に null 化（GC unload のため）。`GetUnloadReference()` / `GetDiagnostics()` を追加。 |
| `src/MiniMockito.Shims.Experimental/NewInterceptionHarness.cs` | `RewriteAssembly()` で original directory を `RewrittenAssemblyLoader` に渡す。`GetUnloadReference()` / `GetAlcDiagnostics()` / `RegisterShimWithMatchers<T>()` を追加。 |

### 12.2 実装した ShimAssemblyLoadContext

```csharp
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    // isCollectible: true, named: "ShimIsolated-{assemblyFileName}"
    // Load() の解決順:
    //   1. MiniMockito.Shims.Experimental → null (parent ALC fallback、registry 共有のため)
    //   2. AssemblyDependencyResolver → path (temp dir に deps.json がある場合)
    //   3. original directory probing → path (original dir が指定されている場合)
    //   4. null → parent ALC fallback (BCL, System.Runtime 等)
    //
    // 診断: _resolvedPaths / _parentFallbacks を Load() 内で記録
    public ShimAlcDiagnostics GetDiagnostics();
}
```

### 12.3 実装した harness API

```csharp
// 既存 API（変更なし）
harness.Create<TService>()
harness.CreateFake<TTarget>(params object[] ctorArgs)
harness.RegisterShim<TTarget>(object fakeInstance)       // catch-all
harness.Invoke<TResult>(object instance, string method, params object[] args)
harness.GetRewrittenType(Type originalType)

// Phase 12 追加 API
harness.RegisterShimWithMatchers<TTarget>(object fake, params IShimArgumentMatcher[] matchers)
harness.GetUnloadReference()          // WeakReference to isolated ALC (call before Dispose)
harness.GetAlcDiagnostics()           // ShimAlcDiagnostics snapshot
```

### 12.4 確認された制約

| 制約 | 状態 |
|-----|------|
| ALC isolation は experimental | ドキュメントに明記 |
| 型 identity 問題 | 未解決。`GetRewrittenType` 経由の回避策を維持 |
| strongly typed API | 制限あり。reflection-based API (`Invoke<TResult>`) を推奨 |
| unload check の非決定性 | GC ベース。`Inconclusive` で安全に処理 |
| dependency resolution | temp dir に deps.json がないため resolver は機能しない。original dir probing + parent fallback で補完 |
| Visual Studio Test Explorer 完全統合 | 未対応 |
| parallel test safety | 保証しない。`[DoNotParallelize]` 必須 |
| AssemblyDependencyResolver | temp dir の deps.json がないため実質的には機能しない。将来 shadow copy で改善可能 |

### 12.5 unload WeakReference パターン（Phase 12 確立）

```csharp
// テスト内での正しいパターン
[MethodImpl(MethodImplOptions.NoInlining)]
private static WeakReference CreateHarnessGetWeakRefAndDispose()
{
    var harness = NewInterceptionHarness.Create()
        .WithTarget<UserRepository>()
        .RewriteTargetTypeAssembly();

    var weakRef = harness.GetUnloadReference(); // Dispose 前に取得
    harness.Dispose();  // Unload() + _context = null + _assembly = null + _loader = null
    return weakRef;     // harness goes out of scope after method returns
}

// 呼び出し側
var weakRef = CreateHarnessGetWeakRefAndDispose();
for (int i = 0; i < 10; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    if (!weakRef.IsAlive) break;
}
Assert.IsFalse(weakRef.IsAlive);  // 通常は成功。不確定な場合は Inconclusive
```

**unload を妨げる強参照（テスト設計上の注意）:**
- `Type` オブジェクト（isolated ALC の型）をフィールドに保持しない
- `Assembly` オブジェクトをフィールドに保持しない
- `MethodInfo`, `FieldInfo` をフィールドに保持しない
- `[MethodImpl(MethodImplOptions.NoInlining)]` で JIT スタック保持を防ぐ

---

## 11. Phase 12 PoC 実装プロンプト

```markdown
# PROMPT.shims.phase12.assemblyloadcontext-isolation-poc.md

# MiniMockito.Shims.Experimental Phase 12: AssemblyLoadContext isolation PoC

AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、
docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md、
docs/shims-assemblyloadcontext-isolation-design.md を読んでください。

## この Phase の目的

Phase 11 の設計を元に、AssemblyLoadContext isolation の PoC を実装してください。

**本格実装ではありません。** Phase 11 で整理した問題のうち以下を優先実装します。

- dependency resolution の改善（original directory probing）
- WeakReference による unload 確認
- unload 失敗時の diagnostics
- ShimAssemblyLoadContext の改善

## 実装対象

### 1. ShimAssemblyLoadContext の改善

以下を実装してください。

- `isCollectible: true` は維持する
- original assembly directory を probing path として追加する
- `MiniMockito.Shims.Experimental` は常に default ALC から共有する（`return null`）
- `AssemblyDependencyResolver` を original dir の assembly で初期化する案も検討する

### 2. WeakReference による unload 確認

以下を実装してください。

- `RewrittenAssemblyLoader` に WeakReference を保持するフィールドを追加
- `WaitForUnload(int maxGcCycles)` メソッドを追加
- GC.Collect + GC.WaitForPendingFinalizers で unload を確認
- unload 結果を `UnloadDiagnostics` として返す

### 3. 診断の改善

以下を実装してください。

- ロードした assembly 名の一覧
- 解決できなかった dependency の報告
- unload 成否の報告

### 4. テスト

以下のテストを追加してください。

- `ShimAssemblyLoadContext` が collectible であることを確認する
- dependency が original directory から正しく解決されることを確認する
- Dispose 後に unload されることを WeakReference で確認する
- unload 失敗時のヒントが診断に含まれることを確認する
- 型 identity 問題（isolated ALC の型と default ALC の型が別 object）をテストで固定する

## 実装しないこと

- ALC ごとの ShimContext 分離
- parallel test 安全性の保証
- strongly typed fake instance の型安全 cast
- interface abstraction による型 identity 解消
- static method mocking
- BCL type 差し替え
- production assembly in-place rewrite

## 重要な仕様

- `ShimDispatcher` assembly は常に default ALC から共有する
- `ShimRuleRegistry` は型 identity が一致するキーでのみ rule を返す
- harness 経由の `RegisterShim<T>` は `GetRewrittenType` を経由して正しい型キーで登録する
- weakly typed API (`Invoke<TResult>`) は Phase 12 以降も維持する

## 検証

最後に必ず以下を実行してください。

```bash
dotnet build
dotnet test
```

失敗した場合は修正してください。

## 完了時の報告

- 変更ファイル一覧
- ALC dependency resolution の改善内容
- WeakReference unload 確認の実装内容
- 診断の改善内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
```
