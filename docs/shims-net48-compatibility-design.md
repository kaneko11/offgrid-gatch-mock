# Phase 15: .NET Framework 4.8 / C# 7.3 Compatibility Design

> **Phase 15 はデザイン調査フェーズです。実装は行いません。**  
> 成果物は本ドキュメント(`docs/shims-net48-compatibility-design.md`)のみです。  
> Phase 16 で net48 向け MSTest project を新規作成します。

---

## 1. 目的

`MiniMockito.Shims.Experimental` を `.NET Framework 4.8 かつ LangVersion=7.3` の
テストプロジェクトから使えるようにするための設計調査を行います。

### 1.1 対応したい範囲

| 機能 | 対応 |
|------|------|
| .NET Framework 4.8 MSTest テストプロジェクトからの利用 | ✅ 対応 |
| LangVersion=7.3 のコードでの API 呼び出し | ✅ 対応 |
| user-defined class の `new` 差し替え（parameterless ctor） | ✅ 対応 |
| constructor arguments 付き `new` 差し替え | ✅ 対応 |
| `WithArguments` matcher API | ✅ 対応 |
| `ShimCaptor<T>` | ✅ 対応 |
| original assembly は上書きしない | ✅ 対応（既実装） |

### 1.2 初期対応で対象外にするもの

| 機能 | 理由 |
|------|------|
| AssemblyLoadContext isolation | net48 では ALC が存在しない |
| collectible unload | net48 では assembly unload は AppDomain 再起動のみ |
| BCL static method mocking (`DateTime.Now` 等) | BCL assembly rewrite が必要 |
| CLR Profiling API | native component が必要 |
| detour / method patching | process crash リスク、sandbox 非互換 |
| production assembly in-place rewrite | 安全ポリシーにより禁止 |
| .NET 8 専用 API の直接呼び出し | net48 では実行時エラー |

---

## 2. 現状確認

### 2.1 csproj / TargetFramework / パッケージ依存

`src/MiniMockito.Shims.Experimental/MiniMockito.Shims.Experimental.csproj` の現在の設定:

```xml
<TargetFrameworks>net8.0;net48</TargetFrameworks>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<NoWarn>$(NoWarn);CS1574;CS8604</NoWarn>
```

**重要:** `LangVersion: latest` は、コンパイラ（.NET 8 SDK 同梱の C# 12）をビルド時に使うことを意味します。  
net48 ターゲットでも C# 12 の言語機能（ファイルスコープ名前空間、init、nullable 等）を使った  
ソースが net48 IL としてコンパイルされます。これは**純粋にコンパイラ機能**です。  
実行時 API の差異（ALC、Range、新 BCL メソッド等）のみ `#if` 分岐が必要です。

パッケージ依存:

```
Mono.Cecil 0.11.6
  → net48 で動作確認済み (.NET Framework 4.0 以降をサポート)
```

### 2.2 Phase 14.5（前フェーズ）で実施済みの互換対応

Phase 14.5 で以下を実施しました。本ドキュメントはその延長として設計調査を行います。

#### 実施済み: Polyfill 追加

| ファイル | 内容 |
|---------|------|
| `Polyfills/ThrowHelper.cs` | `ArgumentNullException.ThrowIfNull`（.NET 6+）、`ArgumentException.ThrowIfNullOrWhiteSpace`（.NET 7+）、`ObjectDisposedException.ThrowIf`（.NET 7+）の代替 |
| `Polyfills/IsExternalInit.cs` | `init` setters を net48 でコンパイル可能にする（`#if !NET5_0_OR_GREATER`） |
| `Polyfills/CallerArgumentExpression.cs` | `[CallerArgumentExpression]` 属性を net48 で使えるようにする（`#if !NET6_0_OR_GREATER`） |

#### 実施済み: #if NETFRAMEWORK 分岐

| ファイル | 変更 |
|---------|------|
| `ShimAssemblyLoadContext.cs` | `#if !NETFRAMEWORK` で全体を囲み、net48 ではクラス定義ごと除外 |
| `RewrittenAssemblyLoader.cs` | net8.0 側は `ShimAssemblyLoadContext`、net48 側は `Assembly.LoadFrom` + `AppDomain.AssemblyResolve` |

#### 実施済み: API 互換修正

| 変更前 | 変更後 | 理由 |
|--------|--------|------|
| `name[..tickIndex]` | `name.Substring(0, tickIndex)` | `System.Index`/`System.Range` は net48 では未定義 |
| `name.IndexOf('`', StringComparison.Ordinal)` | `name.IndexOf('`')` | `IndexOf(char, StringComparison)` は .NET 5+ API |
| `_key.GetHashCode(StringComparison.Ordinal)` | `#if NET5_0_OR_GREATER` + `StringComparer.Ordinal.GetHashCode(_key)` | `string.GetHashCode(StringComparison)` は .NET 5+ API |
| 全ファイルの `ArgumentNullException.ThrowIfNull(` | `ThrowHelper.ThrowIfNull(` | .NET 6+ API |
| 全ファイルの `ArgumentException.ThrowIfNullOrWhiteSpace(` | `ThrowHelper.ThrowIfNullOrWhiteSpace(` | .NET 7+ API |
| 全ファイルの `ObjectDisposedException.ThrowIf(` | `ThrowHelper.ThrowIfDisposed(` | .NET 7+ API |

#### 実施済み: ビルド・テスト確認

```
dotnet build → 0 エラー、0 警告（net8.0 / net48 両方）
dotnet test  → 318 件 PASS
```

---

## 3. net8.0 専用 API の洗い出し

### 3.1 完全に対処済み

| API | カテゴリ | 対処 |
|-----|---------|------|
| `System.Runtime.Loader.AssemblyLoadContext` | ALC | `#if !NETFRAMEWORK` で除外 |
| `System.Runtime.Loader.AssemblyDependencyResolver` | ALC | `#if !NETFRAMEWORK` で除外 |
| `AssemblyLoadContext.Unload()` | ALC | net48 では dead `WeakReference` を返す |
| `string.GetHashCode(StringComparison)` | BCL .NET 5+ | `#if NET5_0_OR_GREATER` 分岐 |
| `string.IndexOf(char, StringComparison)` | BCL .NET 5+ | `IndexOf(char)` に変更 |
| `string[..n]` (Range indexer) | C# 8 + .NET | `.Substring(0, n)` に変更 |
| `ArgumentNullException.ThrowIfNull` | BCL .NET 6+ | ThrowHelper polyfill |
| `ArgumentException.ThrowIfNullOrWhiteSpace` | BCL .NET 7+ | ThrowHelper polyfill |
| `ObjectDisposedException.ThrowIf` | BCL .NET 7+ | ThrowHelper polyfill |
| `System.Diagnostics.CodeAnalysis.NotNullAttribute` | BCL .NET | パラメータ属性を削除 |
| `init` setter | C# 9 コンパイラ | IsExternalInit polyfill |
| `[CallerArgumentExpression]` | C# 10 | CallerArgumentExpression polyfill |

### 3.2 ライブラリコードに残存するが net48 でも動作確認済み

| 言語機能 | 理由 |
|---------|------|
| ファイルスコープ名前空間 (`namespace X;`) | C# 10 コンパイラ機能、net48 IL として正しくコンパイルされる |
| Nullable reference types (`?` 注釈) | C# 8+ コンパイラ機能、runtime に影響しない |
| `record` 型 | ライブラリ内では使用していない（`StaticMethodKey` は `class`） |
| コレクション式 (`[]`) | C# 12、net48 IL（`new List<T>(0)` 相当）にコンパイルされる |
| `AsyncLocal<T>` | .NET Framework 4.6 以降に含まれる ✅ |
| `Interlocked.Increment` | 全 TFM 共通 ✅ |
| `Volatile.Read` | 全 TFM 共通 ✅ |

### 3.3 今後の実装で注意が必要な API

| API | net48 対応 | 注意事項 |
|-----|-----------|---------|
| `GC.AllocateUninitializedArray<T>` | ❌ | net6.0+ のみ。使用していないが今後追加時は注意 |
| `MemoryMarshal.*` | 部分的 | Span<T> 系は net48 では使わない |
| `System.Text.Json` | ❌ | net48 には含まれない。将来 JSON 診断を追加する場合は条件分岐が必要 |

---

## 4. AssemblyLoadContext と net48 での代替案

### 4.1 ALC の net48 非対応整理

`System.Runtime.Loader.AssemblyLoadContext` は .NET Core 1.0+ (.NET Standard 1.5+) で導入されました。
**.NET Framework 4.8 には存在しません。**

非対応になる機能:

| 機能 | net8.0 実装 | net48 での状況 |
|------|-----------|--------------|
| 名前付き collectible ALC | `new ShimAssemblyLoadContext(...)` | ❌ 利用不可 |
| assembly の unload | `AssemblyLoadContext.Unload()` | ❌ 利用不可 |
| ALC ごとの型分離 | Load() オーバーライド | ❌ 利用不可 |
| `AssemblyDependencyResolver` | deps.json ベース解決 | ❌ 利用不可 |

### 4.2 AppDomain isolation（採用しない）

.NET Framework 4.x では `AppDomain.CreateDomain()` で新しいアプリケーションドメインを作れます。  
隔離された AppDomain に rewritten assembly をロードすれば型 identity を分離できます。

**不採用の理由:**

- AppDomain のセットアップが重い（数十〜数百ミリ秒）
- AppDomain 間のマーシャリングには `MarshalByRefObject` が必要
- `ShimDispatcher` / `ShimContext` の共有が複雑になる
- `AppDomain.CreateDomain()` は .NET Core/.NET 5+ では常に `NotSupportedException`
- multi-target (net8.0;net48) での統一 API が作れない
- テストコードが大幅に複雑になる

### 4.3 Assembly.LoadFrom（採用案）

**net48 では `Assembly.LoadFrom(path)` を採用します。**

```csharp
// RewrittenAssemblyLoader.cs の net48 パス（実装済み）
#if NETFRAMEWORK
    _resolveHandler = OnAssemblyResolve;
    AppDomain.CurrentDomain.AssemblyResolve += _resolveHandler;
    _netFxAssembly = Assembly.LoadFrom(AssemblyPath);
    return _netFxAssembly;
#endif
```

`Assembly.LoadFrom` の挙動:

- CLR の "LoadFrom context" にアセンブリをロードする
- デフォルトの "Load context" とは分離されている
- **同じパスのアセンブリを2回 LoadFrom しても CLR は同じ Assembly オブジェクトを返す**
- 型 identity は独立する: LoadFrom で得た `UserRepository` ≠ test project の `UserRepository`

`MiniMockito.Shims.Experimental` のシングルトン保証:

```csharp
// AppDomain.AssemblyResolve ハンドラで既ロード済みアセンブリを返す（実装済み）
private Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
{
    var requestedName = new AssemblyName(args.Name);
    foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
    {
        if (string.Equals(loaded.GetName().Name, requestedName.Name,
                StringComparison.OrdinalIgnoreCase))
            return loaded; // 既ロード済みを返す → ShimDispatcher はシングルトン
    }
    // ...
}
```

これにより、rewritten assembly が `ShimDispatcher.New<T>()` を呼ぶとき、
すでにロード済みの `MiniMockito.Shims.Experimental` アセンブリが使われます。  
`ShimContext`・`ShimRuleRegistry`・`ShimDispatcher` はプロセス全体でシングルトンとして機能します。

**型 identity 問題（net48 でも同様）:**

```
LoadFrom context:
  /temp/.../SampleAssembly.dll
    UserRepository (Type B)  ← test project の UserRepository (Type A) とは別

Load context (デフォルト):
  SampleAssembly.dll
    UserRepository (Type A)
  MiniMockito.Shims.Experimental.dll
    ShimDispatcher (shared) ← AssemblyResolve で保証
```

回避策（net8.0 と同一パターン）:

```csharp
// GetRewrittenType() で isolated context の型を取得してからルール登録
harness.RegisterShim<UserRepository>(fakeInstance);
// 内部: var t = assembly.GetType(typeof(UserRepository).FullName); で Type B を取得
```

### 4.4 isolation なしの rewrite harness（採用しない）

型分離を捨てて、元のアセンブリのパスに rewritten assembly を上書きするか、
`Assembly.Load(byte[])` でメモリロードする方式。

**不採用の理由:**
- `original assembly は上書きしない` の安全ポリシーに反する
- `Assembly.Load(byte[])` は Load context から分離されないため、同名型が2つ存在すると `FileLoadException` が発生する可能性がある

### 4.5 shadow copy（採用しない）

`AppDomain.CurrentDomain.SetShadowCopyFiles()` でアセンブリを影コピーしてロックを避ける方式。

**不採用の理由:**
- すでに一時ディレクトリへの書き出し方式で同じ効果が得られている
- AppDomain 設定変更は副作用が大きい

### 4.6 temporary output directory（既採用）

**現在の実装方式（net8.0 / net48 共通）:**

```
入力: test output の SampleAssembly.dll
Mono.Cecil でコピー書き換え
出力: %TEMP%/MiniMockito.Shims.Experimental/{guid}/SampleAssembly.dll
```

- original assembly を変更しない ✅
- 一時ファイルなのでファイルロック競合を避けられる ✅
- net48 でも動作する ✅
- `[assembly: DoNotParallelize]` で並列 rewrite を防ぐ ✅

---

## 5. C# 7.3 消費側の制約

### 5.1 語法制約

以下の C# 言語機能は C# 7.3 では使えません。  
消費側（net48 テストプロジェクト）のサンプルコードには使用しないでください。

| 言語機能 | 最低バージョン | C# 7.3 での回避方法 |
|---------|-------------|-----------------|
| `using var x = ...` | C# 8.0 | `using (var x = ...) { }` に書き換える |
| Nullable annotations (`string?`) | C# 8.0 | 型注釈なし、または `// nullable disabled` |
| switch expression (`x switch { ... }`) | C# 8.0 | `if / switch statement` |
| `??=` 演算子 | C# 8.0 | `if (x == null) x = ...` |
| `is not null` パターン | C# 9.0 | `x != null` |
| `record` 型 | C# 9.0 | `class` を使う |
| `init` setters をオブジェクト初期化子で設定 | C# 9.0 | 読み取りのみ（設定は不要） |
| `with` 式 | C# 9.0 | 使わない |
| ファイルスコープ名前空間 | C# 10.0 | `namespace X { }` ブロック形式 |
| コレクション式 `[1, 2, 3]` | C# 12.0 | `new T[] { 1, 2, 3 }` |
| Primary constructor | C# 12.0 | 明示的 constructor |

### 5.2 public API の C# 7.3 互換性評価

| API | C# 7.3 互換 | 備考 |
|-----|------------|------|
| `ShimContext.Create()` | ✅ | 普通の static method |
| `using (ShimContext.Create()) { }` | ✅ | IDisposable の using statement |
| `Shim.New<T>().Returns(fake)` | ✅ | ジェネリックメソッド、C# 7.3 対応 |
| `.WithArguments(ShimArg.Any<string>())` | ✅ | |
| `.WithArguments(ShimArg.Eq("prod"))` | ✅ | |
| `.WithArguments(ShimArg.Is<string>(s => s.StartsWith("prod")))` | ✅ | ラムダ式は C# 3+ |
| `ShimCaptor.For<string>()` | ✅ | |
| `captor.Value` | ✅ | プロパティ読み取り |
| `captor.Values` | ✅ | `IReadOnlyList<T>` |
| `harness.Invoke<string>(obj, "Method", arg)` | ✅ | |
| `harness.GetAlcDiagnostics().Format()` | ✅ | 診断は読み取りのみ |
| `ShimAlcDiagnostics.AlcName` | ✅ | `init` プロパティは読み取りのみ使用 |

### 5.3 nullable reference types を public API に出さない方針

ライブラリの public API はヘッダー(XML doc)含め nullable 注釈を持ちますが、
C# 7.3 の消費者には透過的です。

**理由:** C# 8+ の nullable context が有効でないプロジェクトでは、
nullable 注釈（`?` や `[NotNull]`）は単なるメタデータとして無視されます。
型チェック上は非 nullable と同じに扱われます。

**ガイドライン（Phase 16 実装時）:**

```csharp
// net48 MSTest プロジェクトの冒頭（Nullable 無効が推奨）
// <Nullable>disable</Nullable> を csproj に設定する

// test code 内では型注釈なし
var captor = ShimCaptor.For<string>();   // string? ではなく ShimCaptor<string>
string result = harness.Invoke<string>(service, "GetName", 1);  // nullable 無し
```

**サンプルには `#nullable enable/disable` を混在させないこと。**  
C# 7.3 は nullable context を認識しないためコンパイルエラーになります。

### 5.4 using statement（ブロック形式）をサンプルにする方針

net48 / C# 7.3 向けサンプルでは `using declaration` ではなく `using statement` を使います。

```csharp
// ❌ C# 8+ using declaration（使わない）
using var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly();

// ✅ C# 7.3 using statement（これを使う）
using (var harness = NewInterceptionHarness.Create()
    .WithTarget<UserRepository>()
    .RewriteTargetTypeAssembly())
{
    using (var ctx = ShimContext.Create())
    {
        harness.RegisterShim<UserRepository>(fakeRepo);
        var service = harness.Create<UserService>();
        var result = harness.Invoke<string>(service, "GetDisplayName", 1);
        Assert.AreEqual("fake-1", result);
    }
}
```

**`using` ブロックのネスト順序:** harness は outermost、ShimContext は harness の内側に置く。  
これは net8.0 サンプルでも同様ですが、C# 7.3 では using declaration を使えないため明示的な
ブロック形式が特に重要です。

---

## 6. Mono.Cecil の net48 対応

| 項目 | 状況 |
|------|------|
| Mono.Cecil 0.11.6 の net48 サポート | ✅ `.NET Framework 4.0` 以降をサポート |
| `ModuleDefinition.ReadModule(path)` | ✅ net48 で動作 |
| `module.Write(outputPath)` | ✅ net48 で動作 |
| `MethodDefinition.Body.Instructions` | ✅ net48 で動作 |
| generic instance type の解決 | ✅ net48 で動作 |
| PDB (WriteSymbols = false) | ✅ PDB 不要方式で動作 |

**結論:** Mono.Cecil は net48 での assembly rewrite に完全対応しています。  
`AssemblyRewriter`、`NewObjRewriter`、`StaticCallRewriter` は net48 でも同じコードが動作します。

---

## 7. MSTest on .NET Framework 4.8

### 7.1 パッケージ構成

| パッケージ | バージョン | net48 対応 |
|-----------|----------|-----------|
| `MSTest.TestFramework` | 3.x | ✅ net462+ 対応 |
| `MSTest.TestAdapter` | 3.x | ✅ net462+ 対応 |
| `MSTest.Analyzers` | 3.x | ✅ Roslyn analyzer（ビルド時のみ） |

### 7.2 テスト実行方式

```powershell
# dotnet test で net48 テストを実行（推奨）
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/ -f net48

# または全 framework
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/
```

`dotnet test` は net48 テストプロジェクトを VSTest runner
（`vstest.console.exe` 相当）経由で実行します。

### 7.3 並列化の無効化

```csharp
// net48 テストプロジェクトの AssemblyInfo.cs または test class
[assembly: DoNotParallelize]

[TestClass]
[DoNotParallelize]
public sealed class Net48NewObjShimTests
{
    // ...
}
```

`ShimDispatcher` / `ShimContext` はプロセス全体で process-wide state を持つため、
net8.0 と同様に並列実行は危険です。

### 7.4 `[DoNotParallelize]` 属性の利用可否

`Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelizeAttribute` は
`MSTest.TestFramework` に含まれており、net48 でも使用可能です。

---

## 8. Visual Studio 2022 + .NET Framework 4.8 test project

### 8.1 プロジェクト作成手順

```
Visual Studio 2022 → 新しいプロジェクト → MSTest テスト プロジェクト (.NET Framework)
または
Visual Studio 2022 → 追加 → 新しいプロジェクト → クラス ライブラリ (.NET Framework) → MSTest 手動設定
```

### 8.2 Test Explorer での実行

- Visual Studio 2022 の Test Explorer から net48 MSTest テストを実行できます
- `MiniMockito.Shims.Experimental` を ProjectReference で参照すれば IDE から直接デバッグ可能です
- ただし ALC unload を WeakReference で検証するテストは Test Explorer でも
  `Assert.Inconclusive` になる場合があります（net48 では unload 未対応のため常に `IsAlive = true`）

### 8.3 LangVersion=7.3 の設定

```xml
<!-- net48 テストプロジェクト -->
<PropertyGroup>
  <TargetFramework>net48</TargetFramework>
  <LangVersion>7.3</LangVersion>
  <Nullable>disable</Nullable>
  <ImplicitUsings>disable</ImplicitUsings>
</PropertyGroup>
```

`LangVersion=7.3` 設定時、以下のコンパイルエラーが発生することを確認してください:

```csharp
// これがコンパイルエラーになることで、C# 7.3 制約を検証できる
using var x = new object(); // CS8652: LangVersion 7.3 ではサポートされていない
```

---

## 9. multi-target 方針

### 9.1 採用方針: net8.0;net48（変更なし）

```xml
<TargetFrameworks>net8.0;net48</TargetFrameworks>
```

**netstandard2.0 を追加しない理由:**

| 観点 | net8.0;net48 | netstandard2.0 追加 |
|------|-------------|------------------|
| ALC 利用 | ✅ net8.0 で利用 | ❌ netstandard2.0 では使えない |
| API 明示性 | ✅ TFM ごとに明確 | ❌ 共通化により制約が増える |
| 将来の net9.0 追加 | ✅ 容易 | ⚠️ 不要な中間層が残る |
| net462〜net47x のサポート | 必要なら追加可能 | netstandard2.0 で代替可能 |

**netstandard2.0 が必要になるケース（将来）:**

```
→ net462 / net471 / net472 のサポートが必要になった場合
→ その時点で <TargetFrameworks>net8.0;net48;netstandard2.0</TargetFrameworks> に変更する
```

### 9.2 テストプロジェクトの方針

| プロジェクト | TargetFramework | 用途 |
|------------|----------------|------|
| `MiniMockito.Shims.Experimental.Tests` | `net8.0` | 既存 235 テスト、ALC isolation テスト含む |
| `MiniMockito.Shims.Experimental.Net48Tests` | `net48` | **Phase 16 で新規作成** |

2 つのプロジェクトは独立させ、既存テストを壊さない設計にします。

---

## 10. #if NETFRAMEWORK 分岐箇所

### 10.1 実装済みの分岐

| ファイル | 分岐 | 内容 |
|---------|------|------|
| `ShimAssemblyLoadContext.cs` | `#if !NETFRAMEWORK` | クラス全体（ALC 非対応） |
| `RewrittenAssemblyLoader.cs` | `#if !NETFRAMEWORK / #else` | コンストラクタ、`Load()`、`GetUnloadReference()`、`GetDiagnostics()`、`Dispose()` |
| `StaticMethodKey.cs` | `#if NET5_0_OR_GREATER` | `GetHashCode(StringComparison.Ordinal)` |
| `Polyfills/IsExternalInit.cs` | `#if !NET5_0_OR_GREATER` | polyfill 定義 |
| `Polyfills/CallerArgumentExpression.cs` | `#if !NET6_0_OR_GREATER` | polyfill 定義 |

### 10.2 追加分岐が不要な理由

以下のファイルは修正済みで net48 でも単一コードパスで動作します:

| ファイル | 対処 |
|---------|------|
| `AssemblyRewriteScanner.cs` | Range indexer / IndexOf 修正済み |
| `NewObjRewriter.cs` | 同上 |
| `StaticCallRewriter.cs` | 同上 |
| `ShimContext.cs` | `AsyncLocal<T>` は .NET 4.6 以降に含まれる |
| `ShimDispatcher.cs` | 標準 CLR API のみ |
| `StaticShimDispatcher.cs` | 標準 CLR API のみ |
| `NewInterceptionHarness.cs` | 型操作は Reflection のみ |
| `AssemblyRewriter.cs` | Mono.Cecil のみ |

### 10.3 net48 での `GetUnloadReference()` の仕様

```csharp
// RewrittenAssemblyLoader.cs の net48 パス（実装済み）
public WeakReference GetUnloadReference()
{
#if !NETFRAMEWORK
    return new WeakReference(_context, trackResurrection: true);
#else
    // net48 では assembly unload がサポートされない。常に死んだ参照を返す。
    return new WeakReference(null, trackResurrection: true);
#endif
}
```

**影響:** net48 テストで `GetUnloadReference()` を呼ぶと常に `IsAlive = false` の参照が返ります。  
ALC unload 検証テスト（`Phase12AlcIsolationTests.cs`）は net48 では `Assert.Inconclusive` で  
安全にスキップすることを推奨します。

---

## 11. net48 で対応する機能 / 対応しない機能

### 11.1 net48 で対応する機能

| 機能 | 実装方式 | 備考 |
|------|---------|------|
| `AssemblyRewriter.RewriteNewObj()` | Mono.Cecil（共通） | net48/net8.0 共通コード |
| `AssemblyRewriter.RewriteStaticCalls()` | Mono.Cecil（共通） | net48/net8.0 共通コード |
| `NewInterceptionHarness.WithTarget<T>()` | 共通 | |
| `NewInterceptionHarness.RewriteTargetTypeAssembly()` | 共通 | |
| `Assembly` ロード（rewritten） | `Assembly.LoadFrom()` | net48 専用パス |
| `ShimDispatcher.New<T>()` | 共通 | |
| `ShimDispatcher.NewWithArgs<T>(args)` | 共通 | |
| `StaticShimDispatcher.TryInvoke<T>()` | 共通 | |
| `ShimContext.Create()` | 共通 | `AsyncLocal<T>` は net48 対応 |
| `Shim.New<T>().Returns(fake)` | 共通 | |
| `.WithArguments(...)` / matcher | 共通 | |
| `ShimCaptor<T>` | 共通 | |
| `Shim.Static<T>(...)` | 共通 | string-based key、ALC 型 identity 問題なし |
| 診断 API (`GetDiagnostics()`, `Format()`) | 共通（net48 は簡略版） | |

### 11.2 net48 で対応しない機能

| 機能 | 理由 | 代替 |
|------|------|------|
| isolated AssemblyLoadContext | ALC は net48 未対応 | `Assembly.LoadFrom` + `AppDomain.AssemblyResolve` |
| collectible unload | ALC の機能 | `GetUnloadReference()` は死参照を返す |
| `WeakReference` によるunload確認 | unload しないため常に IsAlive | `Assert.Inconclusive` |
| `ShimAlcDiagnostics.AlcName` の具体的情報 | "NetFx-LoadFrom" の固定文字列のみ | 利用可能だが情報は限定的 |
| BCL static method mocking | BCL assembly rewrite が必要 | 対象外 |
| `DateTime.Now` mocking | BCL static | 対象外 |
| production assembly in-place rewrite | 安全ポリシー | 対象外 |

---

## 12. net48 用 test project 構成案

### 12.1 csproj

```xml
<!-- tests/MiniMockito.Shims.Experimental.Net48Tests/
     MiniMockito.Shims.Experimental.Net48Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- MSTest v3 は net462+ をサポート -->
    <PackageReference Include="MSTest.TestFramework" Version="3.8.3" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.8.3" />
  </ItemGroup>

  <ItemGroup>
    <!-- ライブラリを ProjectReference で参照 → net48 ターゲットが自動選択される -->
    <ProjectReference
      Include="..\..\src\MiniMockito.Shims.Experimental\
               MiniMockito.Shims.Experimental.csproj" />
  </ItemGroup>
</Project>
```

### 12.2 AssemblyInfo.cs（並列化無効化）

```csharp
// tests/MiniMockito.Shims.Experimental.Net48Tests/Properties/AssemblyInfo.cs
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// parallel test は process-wide state に対して危険
[assembly: DoNotParallelize]
```

### 12.3 sample test code（C# 7.3 — parameterless ctor）

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48NewObjShimTests
    {
        // ---- parameterless constructor ----

        [TestMethod]
        public void Test_ParameterlessCtor_Net48_ReturnsShimmedInstance()
        {
            // C# 7.3: using statement（using declaration は使えない）
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                var fakeRepo = harness.CreateFake<UserRepository>("net48-fake");

                using (var ctx = ShimContext.Create())
                {
                    harness.RegisterShim<UserRepository>(fakeRepo);

                    var service = harness.Create<UserService>();
                    var result = harness.Invoke<string>(service, "GetDisplayName", 1);

                    Assert.AreEqual("net48-fake-1", result);
                }
            }
        }

        [TestMethod]
        public void Test_OriginalAssembly_IsNotModified()
        {
            var originalPath = typeof(UserRepository).Assembly.Location;
            var originalWriteTime = System.IO.File.GetLastWriteTimeUtc(originalPath);

            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                Assert.AreNotEqual(originalPath, harness.OutputAssemblyPath,
                    "rewritten assembly must be a copy, not the original");
            }

            var afterWriteTime = System.IO.File.GetLastWriteTimeUtc(originalPath);
            Assert.AreEqual(originalWriteTime, afterWriteTime,
                "original assembly must not be modified");
        }
    }
}
```

### 12.4 sample test code（C# 7.3 — constructor arguments）

```csharp
[TestClass]
[DoNotParallelize]
public sealed class Net48ConstructorArgsTests
{
    [TestMethod]
    public void Test_CtorArgs_WithEqMatcher_Net48()
    {
        using (var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly())
        {
            var fakeRepo = harness.CreateFake<UserRepository>("connection-fake");

            using (var ctx = ShimContext.Create())
            {
                // C# 7.3: ShimArg.Eq<T>() は使える
                harness.RegisterShimWithMatchers<UserRepository>(
                    fakeRepo,
                    ShimArg.Eq<string>("prod"));

                var service = harness.Create<UserServiceWithStringCtor>();
                var result = harness.Invoke<string>(service, "GetDisplayName", 1);

                Assert.AreEqual("connection-fake-1", result);
            }
        }
    }

    [TestMethod]
    public void Test_CtorArgs_WithAnyMatcher_Net48()
    {
        using (var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly())
        {
            var fakeRepo = harness.CreateFake<UserRepository>("any-fake");

            using (var ctx = ShimContext.Create())
            {
                // C# 7.3: ShimArg.Any<T>() は使える
                harness.RegisterShimWithMatchers<UserRepository>(
                    fakeRepo,
                    ShimArg.Any<string>());

                var service = harness.Create<UserServiceWithStringCtor>();
                var result = harness.Invoke<string>(service, "GetDisplayName", 1);

                Assert.AreEqual("any-fake-1", result);
            }
        }
    }
}
```

### 12.5 sample test code（C# 7.3 — ShimCaptor）

```csharp
[TestClass]
[DoNotParallelize]
public sealed class Net48CaptorTests
{
    [TestMethod]
    public void Test_ShimCaptor_CapturesArg_Net48()
    {
        using (var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly())
        {
            var captor = ShimCaptor.For<string>();  // C# 7.3 互換
            var fakeRepo = harness.CreateFake<UserRepository>("captor-fake");

            using (var ctx = ShimContext.Create())
            {
                harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, captor);

                var service = harness.Create<UserServiceWithStringCtor>();
                harness.Invoke<string>(service, "GetDisplayName", 1);

                Assert.IsTrue(captor.HasValue);
                Assert.AreEqual("prod", captor.Value);
            }
        }
    }
}
```

### 12.6 sample test code（C# 7.3 — static method shim）

```csharp
[TestClass]
[DoNotParallelize]
public sealed class Net48StaticShimTests
{
    [TestMethod]
    public void Test_StaticClock_Now_Net48()
    {
        var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly())
        {
            using (var ctx = ShimContext.Create())
            {
                // C# 7.3: 型引数明示、typeof() 使用
                Shim.Static<DateTime>(typeof(StaticClock).FullName, "Now")
                    .Returns(fixedTime);

                var service = harness.Create<TimedService>();
                var result = harness.Invoke<string>(service, "GetTimedName", 1);

                Assert.AreEqual("1-20250101", result);
            }
        }
    }
}
```

### 12.7 対象 sample クラス（既存 or 新規）

net48 テストは既存の sample クラス（test output に含まれる）を再利用します:

```
参照先アセンブリ: MiniMockito.Shims.Experimental.Tests.dll
                  （net8.0 テストプロジェクトの sample クラス群）
```

ただし、net48 テストプロジェクト自体のアセンブリに sample を置く設計も可能です。  
**Phase 16 では net48 テストプロジェクト専用の sample クラスを用意することを推奨します。**

```
tests/
  MiniMockito.Shims.Experimental.Net48Tests/
    Samples/
      UserRepository.cs       ← parameterless ctor
      UserRepositoryWithArg.cs ← string ctor
      UserService.cs
      UserServiceWithStringCtor.cs
      StaticClock.cs           ← static method sample
      TimedService.cs
    Properties/
      AssemblyInfo.cs
    Net48NewObjShimTests.cs
    Net48ConstructorArgsTests.cs
    Net48CaptorTests.cs
    Net48StaticShimTests.cs
```

---

## 13. 既知の制約（net48 特有）

| 制約 | 内容 |
|------|------|
| ALC 分離なし | `Assembly.LoadFrom` は LoadFrom context にロードされるが、ALC ほど厳密な分離ではない |
| 型 identity は回避策が必要 | net8.0 と同様に `harness.RegisterShim<T>()` / `harness.GetRewrittenType()` 経由で登録すること |
| assembly unload 不可 | net48 では assembly をアンロードできない。ファイルロックが残る場合がある |
| `WeakReference` による unload 確認 | 常に dead reference を返す。unload 確認テストは net48 では `Assert.Inconclusive` |
| `[DoNotParallelize]` 必須 | net8.0 と同様、process-wide state があるため並列実行は危険 |
| coverage ずれ | rewritten assembly の PDB は元と一致しない（テスト限定の許容事項） |
| LangVersion=7.3 の消費側制限 | `using var`、nullable annotations、switch expression は使えない |

---

## 14. Phase 16 実装プロンプト

```markdown
AGENTS.md、AGENTS.shims-experimental.md、docs/v2-shims-experimental-design.md、
docs/shims-new-interception-design.md、docs/shims-constructor-args-design.md、
docs/shims-assemblyloadcontext-isolation-design.md、
docs/shims-static-method-mocking-design.md、
docs/shims-experimental-phase14-milestone.md、
docs/shims-net48-compatibility-design.md を読んでください。

MiniMockito.Shims.Experimental Phase 16 の範囲を実装してください。

## この Phase の目的

Phase 15 の設計に基づき、.NET Framework 4.8 / LangVersion=7.3 のテストプロジェクトを
新規作成し、net48 環境での newobj interception / static method shim が動作することを確認します。

## 前提

- MiniMockito.Shims.Experimental.csproj はすでに net8.0;net48 を
  ターゲットにしています（Phase 14.5 で実施済み）。
- ライブラリ側のソースコードは変更しないでください。
- 既存テスト（net8.0、318 件）を壊さないでください。

## 実装対象

### 1. net48 テストプロジェクトの新規作成

`tests/MiniMockito.Shims.Experimental.Net48Tests/` を新規作成してください。

csproj:
- `<TargetFramework>net48</TargetFramework>`
- `<LangVersion>7.3</LangVersion>`
- `<Nullable>disable</Nullable>`
- `<ImplicitUsings>disable</ImplicitUsings>`
- MSTest.TestFramework / MSTest.TestAdapter（最新安定版）
- MiniMockito.Shims.Experimental への ProjectReference

### 2. sample クラスの追加（プロジェクト内）

以下のクラスをテストプロジェクト内の Samples/ フォルダに追加してください。

- `UserRepository` — parameterless constructor
- `UserRepositoryWithArg` — `UserRepositoryWithArg(string connectionString)`
- `UserService` — `GetDisplayName(int id)` が `new UserRepository()` を使う
- `UserServiceWithStringCtor` — `GetDisplayName(int id)` が
  `new UserRepositoryWithArg("prod")` を使う
- `StaticClock` — `static DateTime Now()`, `static string GetName(int id)`
- `TimedService` — `GetTimedName(int id)` が `StaticClock.Now()` を使う

すべてのクラスは public、non-generic、C# 7.3 で記述してください。

### 3. テストの追加

C# 7.3 構文で以下のテストを追加してください。

**必須:**
- `using` statement（`using declaration` 不使用）
- `[assembly: DoNotParallelize]` と `[DoNotParallelize]` を全クラスに付ける
- nullable annotations（`?`）を型名に使用しない

**テスト内容:**

#### Net48NewObjShimTests
- `Test_ParameterlessCtor_Shimmed` — `new UserRepository()` が shim される
- `Test_OriginalAssembly_NotModified` — original assembly が変更されない
- `Test_NoRule_FallsBackToRealCtor` — ルールなし時は実 constructor が呼ばれる

#### Net48ConstructorArgsTests
- `Test_CtorArgs_EqMatcher_Matches` — `ShimArg.Eq<string>("prod")` でマッチする
- `Test_CtorArgs_AnyMatcher_Matches` — `ShimArg.Any<string>()` でマッチする
- `Test_CtorArgs_NoMatch_FallsBack` — マッチしない場合は実 constructor が呼ばれる

#### Net48CaptorTests
- `Test_Captor_CapturesConstructorArg` — ShimCaptor が引数をキャプチャする
- `Test_Captor_HasValue_AfterCapture` — `captor.HasValue` が true になる
- `Test_Captor_Values_Multiple` — 複数回の capture を `Values` で取得できる

#### Net48StaticShimTests
- `Test_StaticMethod_Shimmed` — static method が差し替えられる
- `Test_StaticMethod_NoRule_FallsBack` — ルールなし時は実 method が呼ばれる
- `Test_Static_And_Newobj_Coexist` — static shim と newobj shim を同一 context 内で使う

#### Net48RegressionTests
- `Test_Net48_ShimContext_Create_Dispose` — ShimContext の生成・破棄が機能する
- `Test_Net48_ActiveContextCount` — Dispose 後に ActiveContextCount が戻る

### 4. ソリューションへの追加

`MiniMockito.sln` に新しいプロジェクトを追加してください。

### 5. 検証

以下を必ず実行してください。

```powershell
dotnet build
dotnet test
```

両方が成功することを確認してください。

## 実装しないこと

- ライブラリ側（MiniMockito.Shims.Experimental）のソースコード変更
- ALC isolation テスト（net48 では ALC 未対応）
- unload 確認テスト（net48 では unload 不可）
- BCL static method mocking
- generic class / generic constructor のテスト
- production assembly in-place rewrite

## 安全ルール（既存）

- MiniMockito 本体に shim 実装を混ぜない
- original assembly は上書きしない
- `ShimContext.Dispose()` で確実に cleanup する
- 既存 v1 / v2 テストを壊さない
- Phase 5 以降の public API を破壊的変更しない

## 完了時の報告（日本語）

- 変更・追加ファイル一覧
- net48 テストで確認した機能
- C# 7.3 互換性の確認事項
- `dotnet build` の結果（net8.0 / net48 両方）
- `dotnet test` の結果（全件 PASS 件数）
- 既知の制約（net48 特有のもの）
- 次に推奨する Phase
```

---

## 15. フェーズ間の関係

| Phase | 内容 |
|-------|------|
| 4〜6 | parameterless newobj rewrite PoC |
| 7 | constructor arguments 対応 |
| 8 | WithArguments matcher API |
| 9 | ShimCaptor |
| 10 | API polish / diagnostics |
| 11 | ALC isolation 設計 |
| 12 | ALC isolation PoC |
| 13 | static method mocking 設計 |
| 14 | StaticCallRewriter / static shim 完成 |
| 14.5 | Stabilization / Docs / NuGet準備 |
| **15（本ドキュメント）** | **.NET Framework 4.8 / C# 7.3 compatibility 設計** |
| **16（次フェーズ）** | **net48 MSTest テストプロジェクトの実装** |
| 17 以降 | BCL call site rewrite 調査 / expression-based API / generic static method 等 |
