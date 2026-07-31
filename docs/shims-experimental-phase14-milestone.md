# MiniMockito.Shims.Experimental — Phase 4〜14 マイルストーン

> このドキュメントは Phase 14.5 時点でのまとめです。

---

## Phase 一覧と到達点

### Phase 4 — 実験パッケージ基盤の確立

**目標:** `MiniMockito.Shims.Experimental` プロジェクトを新規作成し、  
本体 (`MiniMockito`) から完全に分離した experimental namespace を構築する。

**到達点:**
- `MiniMockito.Shims.Experimental` プロジェクト作成（`net8.0`）
- `MiniMockito` 本体は `MiniMockito.Shims.Experimental` を参照しない
- experimental package は本体 release の安定性に影響しない
- CI / ソリューション統合（`MiniMockito.sln` に追加）

---

### Phase 5 — `ShimContext` と `ShimDispatcher` の基盤

**目標:** 差し替え rule を管理する context と、  
shim を呼び出す dispatcher の基盤を実装する。

**到達点:**
- `ShimContext.Create()` — 差し替え rule の lifetime を管理
- `ShimContext.Current` — `AsyncLocal<ShimContext?>` ベース（テスト間分離）
- `ShimContext.Dispose()` — rule を確実にクリア
- `ShimDispatcher.New<T>()` — parameterless constructor shim の呼び出し
- `Shim.New<T>().Returns(instance)` — rule 登録 API
- `[assembly: DoNotParallelize]` — process-wide state の衝突を防ぐ
- **既存 public API の破壊的変更は Phase 5 以降禁止**

---

### Phase 6 — `AssemblyRewriter` と `NewInterceptionHarness`

**目標:** Mono.Cecil で `newobj` call site を差し替え用 wrapper メソッドへリライトし、  
isolated AssemblyLoadContext (ALC) でロードするハーネスを実装する。

**到達点:**
- `AssemblyRewriter.RewriteNewObj(assemblyPath, options, outputPath)` — Mono.Cecil ベース
- リライトは **一時ディレクトリへの書き出し** — 元アセンブリは変更しない
- `NewInterceptionHarness.WithTarget<T>().RewriteTargetTypeAssembly()` — ハーネス API
- `harness.Create<T>()` — isolated ALC から型を生成
- `harness.Invoke<T>(obj, method, args)` — リライト済みアセンブリのメソッドを呼び出す
- `ShimAssemblyLoadContext` — collectible ALC + `MiniMockito.Shims.Experimental` を parent へ fallback

---

### Phase 7 — Constructor Arguments Shim

**目標:** コンストラクタ引数を持つ型の `newobj` を差し替える。

**到達点:**
- `Shim.New<T>().WithArguments(matchers...).Returns(instance)` — 引数マッチャー付き登録
- `ShimArg.Any<T>()` / `ShimArg.Eq<T>(value)` / `ShimArg.Is<T>(predicate)` — 3 種のマッチャー
- `ShimDispatcher.NewWithArgs<T>(args)` — 引数つき dispatch
- `NewShimRule.CreateInstanceWithArgs(args)` — マッチ時のインスタンス生成
- rewriter: 引数を `object[]` にボックス化してから wrapper を呼ぶ IL 生成
- `ByRef` / pointer パラメータはスキップ（`UnsupportedReason = "ByRefOrPointerArgNotSupported"`）
- generic type はスキップ（`UnsupportedReason = "GenericTypeNotSupported"`）

---

### Phase 8 — `ShimCaptor`

**目標:** コンストラクタ引数をキャプチャして後から検証できる API を追加する。

**到達点:**
- `ShimCaptor<T>` — `IShimArgumentMatcher` を実装
- `ShimCaptor.For<T>()` / `ShimArg.Captor<T>()` — 生成 API
- キャプチャ後 `captor.Value` で取得
- 複数のキャプターを同じ `WithArguments()` に渡せる
- 空の場合 `captor.Value` は `default` ではなく `InvalidOperationException`

---

### Phase 9 — `AssemblyRewriteScanner` / Dry-Run Report

**目標:** リライト可否を事前にレポートできる dry-run スキャンを実装する。

**到達点:**
- `AssemblyRewriteScanner.Scan(assemblyPath, options)` → `RewriteReport`
- `RewriteReport.CallSites` — `NewObjCallSite` のリスト
- `NewObjCallSite.IsSupported` / `UnsupportedReason` / `CallingTypeName` / `ILOffset` 等
- `RewriteReport.SupportedCallSites` / `UnsupportedCallSites`

---

### Phase 10 — `ShimRuleRegistry` Diagnostics

**目標:** マッチしなかった理由をテストから確認できる diagnostics を追加する。

**到達点:**
- `ShimContext.LastDispatchDiagnostics` — `DispatchDiagnostics?`
- `DispatchDiagnostics.Format()` — 人間可読な diagnostics 文字列を生成
- "Target: ...", "Tried rules: ...", "Fallback: ..." セクション
- no-match / fallback の理由を per-rule で記録

---

### Phase 11 — Last-Stub-Wins とエラーメッセージ整備

**目標:** 複数 stub を登録した場合に最後に登録したものが勝つ動作を確立し、  
エラーメッセージを人間可読にする。

**到達点:**
- `ShimRuleRegistry` は last-registered-wins（後勝ち）
- `ShimException` のメッセージに `Supported patterns:` / `Hint:` 付き
- "No active ShimContext" / "PublicParameterlessConstructorNotFound" 等

---

### Phase 12 — ALC Isolation & Unload

**目標:** isolated ALC が不要になった時点で GC で収集されることを確認する。

**到達点:**
- `ShimAssemblyLoadContext.Unload()` を Dispose 時に呼ぶ
- `WeakReference` ベースの unload 確認（GC 非決定的 → `Assert.Inconclusive` で安全に処理）
- ALC diagnostics API: `harness.GetAlcDiagnostics().Format()`
- `ParallelizationSettingsTests` — `[assembly: DoNotParallelize]` の検証

---

### Phase 13 — `StaticMethodKey` と `StaticShimDispatcher` 基盤

**目標:** static method のキー管理と dispatch 基盤を設計する。

**到達点:**
- `StaticMethodKey` — `"TypeFull::Method(p1,p2)"` 形式の string-based record
- `StaticShimRegistry` — Dictionary keyed by `StaticMethodKey.ToKeyString()`
- `StaticShimDispatcher.TryInvoke<TResult>(...)` — non-void dispatch
- `StaticShimDispatcher.TryInvokeVoid(...)` — void dispatch
- `ShimContext.StaticRegistry` (internal) — `Dispose()` 時にクリア
- `ShimContext.LastStaticDispatchDiagnostics` — `StaticDispatchDiagnostics?`
- `Shim.Static<T>(typeFullName, method, paramTypes...)` — non-void 登録 API
- `Shim.Static(typeFullName, method, paramTypes...)` — void 登録 API
- `Shim.Static<T>(Type, method, paramTypes...)` — 型ベース登録 API（`FullName` に変換）

---

### Phase 14 — `StaticCallRewriter` と user-defined static shim 完成

**目標:** Mono.Cecil で `call` instruction を wrapper メソッドにリライトし、  
ALC 越しの static method shim を完成させる。

**到達点:**
- `StaticCallRewriter` — Mono.Cecil ベース。call site を `<ShimsStaticWrappers>` クラス内の wrapper メソッドに差し替える
- BCL 検出 — `Scope.Name` が `System.Private.CoreLib` / `mscorlib` 等 → `Skipped BCL static call at ...` を diagnostics に追加してスキップ
- generic type / by-ref パラメータ → スキップ
- non-void wrapper パターン: `TryInvoke<T>(..., out T result)` + `brfalse FALLBACK` + real call
- void wrapper パターン: `TryInvokeVoid(...)` + `brtrue RETURN` + real call
- `NewInterceptionHarness.WithStaticTarget(Type)` — static allowlist に追加。assembly location 解決にも使用
- ALC type identity 問題: static shim は string-based key で解決
- static shim + newobj shim の同一コンテキスト内共存
- 30 test methods (Phase 14 専用) PASS

---

## Phase 14.5 — Experimental Stabilization / Docs / Samples / Cleanup

**目標:** Phase 14 までの実装を stabilize し、ドキュメント・サンプル・テスト整備を行う。

**到達点:**
- `[DoNotParallelize]` を全テストクラスに追加（`ShimProjectSkeletonTests`, `NewObjRewritePocTests`, `RewriteDryRunTests`）
- `MiniMockito.Shims.Experimental.csproj` version: `0.1.0-alpha.3`、description 更新（BCL 未対応・parallel 注意 を明記）
- `ShimArg.cs` XML doc 更新（`Shim.Static` の例を追加）
- `Phase145SamplesTests.cs` — 21 test methods（全パターンのサンプルテスト＋regression）
- `docs/shims-experimental-quickstart.md` — ユーザー向けクイックスタートガイド
- `docs/shims-experimental-phase14-milestone.md` — 本ドキュメント（Phase 4〜14 到達点まとめ）

---

## Phase 16 — .NET Framework 4.8 / C# 7.3 テストプロジェクト

**目標:** Phase 15 設計に基づき net48 / LangVersion=7.3 のテストプロジェクトを新規作成し、
net48 環境での newobj / static shim 動作を確認する。

**到達点:**
- `tests/MiniMockito.Shims.Experimental.Net48Tests/`（net48, LangVersion 7.3）
- net48 専用サンプル（`Net48UserRepository` / `Net48UserService` / `Net48StaticClock` / `Net48TimedService`）
- newobj / constructor args / captor / static shim を C# 7.3（using statement）で検証

---

## Phase 17 — high-level scenario facade (`Shims`)

**目標:** 既存の `NewInterceptionHarness` / `ShimContext` / `RegisterShim` / reflection Invoke を
利用者が直接意識しなくても `new` / user-defined static method 差し替えを書ける facade を追加する。
新しい interception 機能は追加しない（既存 low-level API はそのまま維持）。

**到達点:**
- `Shims` facade（`IDisposable`）— harness + rewrite + ALC + `ShimContext` を 1 つの session に集約
- `Shims.For<TAnchor>()` — anchor 型の所属アセンブリを rewrite 対象にする
- `WithNew<TTarget>()` / `WithStatic(Type)` — rewrite target を登録（確定後の追加は例外）
- `New<TTarget>()` — 既存 `RegisterShim` / `RegisterShimWithMatchers` に委譲（`WithArguments` / `ShimCaptor` 対応）
- `Static<TResult>(...)` / `Static(...)` — 既存 `Shim.Static` builder に委譲（非 void / void）
- `CreateFake<TTarget>(...)` — rewrite 済み identity の fake を生成
- `Create<T>()` — **共有 contract（`IShimCreatable`）のときだけ strongly-typed で成功**。
  concrete 型は型 identity 問題で安全に返せないため `InvalidOperationException`（`CreateObject` / `Invoke` を案内）
- `CreateObject(typeFullName)` + `Invoke<TResult>(...)` / `Invoke(...)` — 推奨 fallback
- diagnostics 転送（`LastNewDispatchDiagnostics` / `LastStaticDispatchDiagnostics` / `GetAlcDiagnostics()`）
- rewrite は初回利用時に lazy 確定。`Dispose` で `ShimContext` → harness を cleanup
- `IShimCreatable` — load context をまたいで identity を共有する contract（本アセンブリ内 interface）
- net8 テスト 15 件（`Phase17HighLevelApiTests`）、net48 テスト 11 件（`Net48HighLevelApiTests`）追加
- **既存 low-level / v1 / v2 テストは無変更で全て PASS**

| 検証項目 | net8 | net48 |
|---------|------|-------|
| parameterless new shim | ✅ | ✅ |
| constructor args new shim | ✅ | ✅ |
| `WithArguments(Eq(...))` | ✅ | ✅ |
| `ShimCaptor` | ✅ | ✅ |
| user-defined static method shim | ✅ | ✅ |
| void static method shim | ✅ | ✅ |
| new + static 共存 | ✅ | ✅ |
| `Create<IShimCreatable>()` 成功 | ✅ | ✅ |
| 型 identity 例外メッセージ | ✅ | ✅ |
| `CreateObject` / `Invoke` fallback | ✅ | ✅ |
| 確定後 `WithNew` / `WithStatic` 例外 | ✅ | ✅ |

---

## 対応済み機能

| 機能 | Phase |
|------|-------|
| ShimContext / ShimDispatcher 基盤 | 5 |
| AssemblyRewriter (newobj) | 6 |
| NewInterceptionHarness | 6 |
| isolated ALC | 6 |
| constructor args shim | 7 |
| Any / Eq / Is matchers | 7 |
| ShimCaptor | 8 |
| AssemblyRewriteScanner / dry-run | 9 |
| Dispatch diagnostics | 10 |
| last-stub-wins | 11 |
| エラーメッセージ整備 | 11 |
| ALC unload 確認 | 12 |
| ALC diagnostics | 12 |
| StaticMethodKey / StaticShimDispatcher | 13 |
| Shim.Static API | 13 |
| StaticCallRewriter | 14 |
| BCL skip diagnostics | 14 |
| static + new shim 共存 | 14 |
| Docs / Samples / Cleanup | 14.5 |
| net48 / C# 7.3 互換設計 | 15 |
| net48 テストプロジェクト | 16 |
| high-level facade (`Shims`) | 17 |
| `Create` / `CreateObject` / `Invoke` | 17 |
| `IShimCreatable` 共有 contract | 17 |

---

## 未対応事項（Next Phase 候補）

### BCL static method mocking

- `DateTime.Now`, `File.ReadAllText`, `Guid.NewGuid()` 等
- BCL は `System.Private.CoreLib` に定義されており、call site のリライトに加えて BCL アセンブリ自体の書き換えが必要
- CLR Profiling API または runtime method patching が必要
- **Phase 15 候補:** CLR Profiling API を用いた BCL interceptor PoC

### Expression-based static API

```csharp
// 未実装
Shim.Static(() => Clock.Now()).Returns(fixedTime);
```

- コンパイル時型安全性を提供する
- Expression<Func<T>> の解析が必要
- **Phase 15 候補**

### Generic static method

```csharp
// スキップされる
Enumerable.Empty<string>()
```

- Mono.Cecil での generic instance method / generic type argument の扱いが複雑
- **Phase 15 候補**

### async static method

- `async static Task<T>` の wrapper 生成
- 現在: wrapper が同期的に TryInvoke を呼ぶため非同期フローと不整合
- **Phase 15 候補**

### by-ref / out パラメータを持つ static method

```csharp
// スキップされる
int.TryParse("1", out int result)
```

- `object[]` への boxing ができないため dispatch できない
- **Phase 15 候補:** out param ネイティブ対応

### Visual Studio Test Explorer 完全統合

- 現時点では `dotnet test` での実行を推奨
- VS Test Explorer では ALC isolation テストが不安定になる場合がある
- **Phase 15 候補:** VS Test Explorer adapter との統合

---

## 制約事項（Phase 14.5 時点）

| 制約 | 理由 |
|------|------|
| `[assembly: DoNotParallelize]` 必須 | `ShimDispatcher` が process-wide state を持つ |
| original assembly は変更しない | テスト限定の一時ディレクトリへの書き出しのみ |
| production assembly の in-place rewrite は行わない | 安全性のため明示的に禁止 |
| ALC unload タイミングは GC 依存 | 決定的な unload タイミングを保証しない |
| BCL static method は差し替え不可 | BCL アセンブリの書き換えが必要 |
| coverage / PDB はリライト済みアセンブリと一致しない | テスト限定の許容事項 |
| `ShimContext.Dispose()` が呼ばれないと rule が残る | `using` ブロックを必ず使う |

---

## 推奨利用パターン

### 基本パターン

```csharp
[TestClass]
[DoNotParallelize]
public sealed class MyTests
{
    [TestMethod]
    public void Test_Repository_Returns_ShimmedInstance()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fakeRepo = harness.CreateFake<UserRepository>("fake");

        using (ShimContext.Create())
        {
            harness.RegisterShim<UserRepository>(fakeRepo);

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);

            Assert.AreEqual("fake-1", result);
        }
    }
}
```

### static method shim パターン

```csharp
[TestMethod]
public void Test_StaticClock_Now_IsShimmed()
{
    var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    using var harness = NewInterceptionHarness.Create()
        .WithStaticTarget(typeof(StaticClock))
        .RewriteTargetTypeAssembly();

    using (ShimContext.Create())
    {
        Shim.Static<DateTime>(typeof(StaticClock).FullName!, "Now")
            .Returns(fixedTime);

        var service = harness.Create<TimedService>();
        var result = harness.Invoke<string>(service, nameof(TimedService.GetTimedName), 1);

        Assert.AreEqual($"1-{fixedTime:yyyyMMdd}", result);
    }
}
```

---

## Phase 15 候補

1. BCL static method mocking PoC（CLR Profiling API または runtime method patching）
2. Expression-based static API (`Shim.Static(() => Clock.Now())`)
3. Generic static method 対応
4. async static method wrapper
5. by-ref / out パラメータ対応
6. VS Test Explorer adapter 統合
7. NuGet パッケージ公開（alpha / preview）

---

## テスト統計（Phase 14.5 時点）

| テストファイル | テスト数 | 対象 Phase |
|--------------|---------|-----------|
| `ShimProjectSkeletonTests.cs` | 10 | 4〜5 |
| `NewObjRewritePocTests.cs` | 15 | 6 |
| `RewriteDryRunTests.cs` | 6 | 9 |
| `Phase12AlcIsolationTests.cs` | 12 | 12 |
| `ParallelizationSettingsTests.cs` | 3 | 12 |
| `Phase14StaticShimTests.cs` | 30 | 14 |
| `Phase145SamplesTests.cs` | 21 | 14.5 |
| (その他) | 〜117 | 5〜11 |
| **合計** | **214** | |

全テスト PASS (Phase 14.5 時点)。

---

## Phase 20 — cross-assembly new interception PoC（追記）

### 目標

リライト対象アセンブリ内で呼ばれている **外部アセンブリ型** の `newobj` を shim に差し替える。
これまでは「リライト対象アセンブリ自身に定義された型」の `newobj` だけが対象だった。

### 追加 API

| API | 内容 |
|-----|------|
| `NewInterceptionHarness.WithExternalTarget<TExternal>()` | 外部アセンブリ型を newobj 差し替え対象に登録 |
| `NewInterceptionHarness.WithExternalTarget(Type)` | 同上（Type 指定版） |
| `NewInterceptionHarness.RegisterShim(Type externalType, object fake)` | 外部型に fake を登録（非ジェネリック版） |
| `NewInterceptionHarness.RegisterShim<TExternal>(fake)` | 外部型にも対応（FullName ベースで登録） |
| `NewInterceptionHarness.CreateObject(string typeName)` | rewritten assembly から型名で生成 |
| `RewriteOptions.ExternalTargetTypes` | 外部 target 型一覧 |
| `ShimDispatchDiagnostics.ResolvedByFullNameFallback` / `DuplicateFullNameRisk` | 外部型ルックアップ診断 |

### 方式

- **rewrite**: 外部型 `newobj` も内部型と同じく `ShimDispatcher.New<T>()` 経由に置換。
  外部型の `TypeReference` / `AssemblyReference` は `module.ImportReference` でそのまま維持され、
  rewritten assembly は外部アセンブリを引き続き参照する（外部アセンブリ自体は書き換えない）。
- **型 identity の共有**: 外部 target アセンブリは isolated ALC ではなく parent (default) ALC から
  共有する（net8: `ShimAssemblyLoadContext.Load` が `null` を返して親へ委譲、net48:
  `AppDomain.AssemblyResolve` が既ロードを返す）。これにより、テスト側 fake と rewritten code の
  期待型が一致する。
- **shim key**: 外部型は `Type.FullName`（+ assembly simple name）ベースで照合する。
  完全な runtime `Type` 一致に依存しない。
- **CreateFake<T>()**: 外部型は未対応。`NotSupportedException` を投げ、手動 fake + `RegisterShim` を案内。

### 追加テスト

- `tests/MiniMockito.Shims.Experimental.Tests/CrossAssemblyNewObjTests.cs`（net8, 9 件）
- `tests/MiniMockito.Shims.Experimental.Net48Tests/Net48CrossAssemblyTests.cs`（net48, 5 件）
- テスト用アセット: `tests/MiniMockito.Shims.Experimental.ExternalLib`（`ExternalLib.ExternalDbContext` /
  `ExternalLib.ExternalOtherContext`）、`tests/MiniMockito.Shims.Experimental.CrossAssemblySample`
  （`CrossAssemblySample.CrossAssemblyUserService`）。いずれも `net48;net8.0` マルチターゲット。

### 制約

- 外部型は FullName ベース照合のため、同一 FullName の型が複数アセンブリにあると曖昧（`DuplicateFullNameRisk`）。
- 外部型に `CreateFake<T>()` は使えない（手動 fake が第一推奨）。
- `DbContext` 系などコンストラクタ／`Dispose` に副作用がある型は、実生成に依存しない fake を用意する。
- BCL static method は引き続き未対応。
- `[assembly: DoNotParallelize]` 必須。

---

## Phase 21 — external type API / string-based target / diagnostics（追記）

### 目標

Phase 20 の cross-assembly new interception を発展させ、テストプロジェクトが外部型を
**コンパイル時参照できない**場合でも使える string-based API を追加し、失敗理由を diagnostics で
追えるようにする。

### 追加 API

| API | 内容 |
|-----|------|
| `WithExternalTarget(string assemblyPath, string typeFullName)` | path + FullName で外部 target を解決・登録 |
| `ResolveExternalType(string assemblyPath, string typeFullName)` | 外部型を `Type` に解決（失敗時 `ShimExternalTargetException`） |
| `RegisterShim(string typeFullName, object fake)` | FullName ベースで fake 登録 |
| `RegisterShim(string typeFullName, string assemblySimpleName, object fake)` | FullName + assembly simple name で登録 |
| `CreateFakeExternal(Type targetType, params object[] args)` | 外部型の素のインスタンス生成（public/non-sealed/parameterless のみ） |
| `CreateFakeExternal(string typeFullName, params object[] args)` | 登録済み外部型 FullName から素のインスタンス生成 |
| `NewInterceptionHarness.Diagnostics` | harness レベル diagnostics ログ（`IReadOnlyList<string>`） |
| `ShimExternalTargetException` | 外部型解決失敗の専用例外 |

### string-based external target の方式

- `ResolveExternalType` は `Assembly.LoadFrom(assemblyPath)` で外部アセンブリを **default load context**
  にロードし、`asm.GetType(typeFullName)` で型を解決する。default context にロードすることで、
  rewrite 後の ALC（外部 target アセンブリは parent から共有）と同一 identity になる。
- 解決した `Type` から `ExternalNewTarget`（FullName + assembly simple name）を作り、Phase 20 と
  同じ FullName ベースの registry キーで登録・照合する。
- 解決失敗（ファイルなし / 型なし / ロード失敗）は `ShimExternalTargetException` を投げ、
  searched path・candidate assembly・type full name・reason を含む。

### diagnostics の追加内容

- harness レベル（`Diagnostics`）: external assembly path / external type full name /
  candidate assembly loaded / type resolution success・failure / external target registered /
  target assembly being rewritten / registry key used / duplicate FullName risk /
  external type fake creation supported・unsupported。
- rewrite レベル（`LastRewriteResult.Diagnostics`）: external newobj detected / rewritten /
  **skipped + skipped reason**（Phase 21 で追加）。
- dispatch レベル（`ShimContext.LastDispatchDiagnostics`）: `ResolvedByFullNameFallback` /
  `DuplicateFullNameRisk`。

### CreateFakeExternal の対応範囲

- `public` かつ `non-sealed`・`non-abstract` な class のみ。引数なしのときは public parameterless ctor 必須。
- proxy / 挙動 override は生成しない（素のインスタンスのみ）。
- 対応外は `NotSupportedException`（reason: `SealedTypeNotSupported` /
  `PublicParameterlessConstructorNotFound` 等）。挙動を変えたい場合は手書き subclass /
  `Mock.Class<T>()` を `RegisterShim(...)`。

### 追加テスト

- `tests/MiniMockito.Shims.Experimental.Tests/CrossAssemblyStringTargetTests.cs`（net8, 14 件）
- `tests/MiniMockito.Shims.Experimental.Net48Tests/Net48CrossAssemblyStringTargetTests.cs`（net48, 7 件）
- テスト用アセット `ExternalLib` に `SealedExternalContext` / `NoDefaultCtorContext` /
  `ExternalByRefContext` を追加、`CrossAssemblySample` に by-ref ctor を呼ぶ `CreateByRefSeed` を追加。

### 制約

- 外部型は FullName ベース照合（同一 FullName が複数アセンブリにあると曖昧 → `Duplicate FullName risk`）。
- `CreateFakeExternal` は public・non-sealed・non-abstract・parameterless ctor のみ。
- `DbContext` 系は実生成に依存しない手動 fake を推奨。
- BCL static method は引き続き未対応。external assembly 自体は rewrite しない。
- `[assembly: DoNotParallelize]` 必須。

---

## Phase 23 — Easy Shims API / ReplaceNew facade（追記）

### 目標

新しい interception 機能は追加せず、Phase 20 / 21 の cross-assembly new interception を
`NewInterceptionHarness` / `ShimContext` / `WithExternalTarget` / `RegisterShim` を直接書かずに使える
high-level facade（Easy API）を追加する。

### 追加 Easy API

| API | 内容 |
|-----|------|
| `Shims.ForAssembly(string targetAssemblyPath)` | target assembly を rewrite 対象に持つ session を作成 |
| `ReplaceNew<T>(T fake)` | internal/external を自動判定し target 宣言 + fake 登録を予約 |
| `ReplaceNew<T>(Func<Shims, object> fakeFactory)` | finalize 時に fake を生成（internal の ALC fake 用） |
| `ReplaceNew(Type targetType, object fake)` | Type 指定版 |
| `ReplaceNew(string externalAssemblyPath, string typeFullName, object fake)` | 文字列指定の external 版 |
| `Shims.CreateObject` / `Create<T>` / `Invoke<TResult>` | 既存 facade メソッド（finalize トリガ） |
| `Shims.Diagnostics` / `LastDispatchDiagnostics` / `GetAlcDiagnostics()` | diagnostics forwarding |

補助として `NewInterceptionHarness.WithTarget(Type)` と internal な `LoadedAssembly` を追加。

### `ReplaceNew(...)` の内部動作

- target type が rewrite 対象 assembly 内なら `WithTarget` 相当、外部アセンブリ型なら
  `WithExternalTarget` 相当を **即時**に呼び、fake 登録（`RegisterShim` 相当）は **finalize まで遅延**する。
- internal / external 判定は `type.Assembly.Location` と target assembly path の比較。
- 文字列版は `WithExternalTarget(assemblyPath, typeFullName)` で解決し、`RegisterShim(typeFullName, fake)` を遅延。

### 複数 replacement / last stub wins

- 1 session 内で `ReplaceNew(...)` を何度でも登録でき、internal と external を混在できる。
- finalize 時に登録順で遅延登録を実行するため、同じ target type への複数 `ReplaceNew` は
  既存 `ShimRuleRegistry` の **last stub wins** に従う。

### rewrite 確定タイミング / ShimContext 管理

- rewrite は初回 `CreateObject` / `Create<T>` / `Invoke<TResult>` で確定（finalize）。
  finalize 時に `ShimContext` を生成し、遅延登録を反映する。
- 確定後の `ReplaceNew(...)` は `InvalidOperationException`
  （`rewrite already completed` / `target cannot be added after rewrite` / `create a new Shims session`）。
- `Dispose()`（`using` 終了）で `ShimContext` と `NewInterceptionHarness`（loader / ALC / temp）を cleanup。
  利用者は `ShimContext.Create()` を書かない。

### internal target の扱い

- internal の fake は rewrite 済み ALC の型 identity を要するため、手作りインスタンスは不可。
  `ReplaceNew<T>(s => s.CreateFake<T>(...))` の factory 形式で ALC fake を生成して登録する。
- 引数条件で fake を分けたい場合は低レベルの `New<T>().WithArguments(...).Returns(...)` を使う。

### 追加テスト

- `tests/MiniMockito.Shims.Experimental.Tests/EasyShimsApiTests.cs`（net8, 11 件）
- `tests/MiniMockito.Shims.Experimental.Net48Tests/Net48EasyShimsApiTests.cs`（net48, 7 件）
- テスト用アセット `ExternalLib` に `ExternalLogger`、`CrossAssemblySample` に internal `InternalGreeter` と
  internal+external を混在構築する `Run(int)` を追加。

### 非対象（Phase 23 で実装しない）

- BCL static method / `DateTime.Now` / `File.ReadAllText` の mocking
- external assembly 自体の rewrite、production in-place rewrite、runtime IL rewrite、CLR Profiling、detour
- expression-based API、public API の破壊的変更

---

## Phase 22 — NuGet package update / release validation（追記）

### 目標

新機能追加や public API の破壊的変更を行わず、NuGet パッケージを更新できる状態にする
（Release build / Release test / `dotnet pack` / nupkg 内容確認まで。nuget.org への push はしない）。

### バージョン更新

| パッケージ | 旧 | 新 |
|-----------|----|----|
| `MiniMockito.Net` | 0.2.0-preview.6 | **0.2.0-preview.7** |
| `MiniMockito.Shims.Experimental` | 0.1.0-alpha.4 | **0.1.0-alpha.6** |

> Phase 22 は 2 回実施しています。1 回目で `0.1.0-alpha.5`（Phase 20 / 21 / 23 まで）を検証し、
> Phase 24（inspection API）追加後の 2 回目で **`0.1.0-alpha.6`**（Phase 20 / 21 / 23 / 24 を含む alpha）を
> 検証しました。`MiniMockito.Net` は Phase 20〜24 で変更がないため `0.2.0-preview.7` を維持します。

### csproj metadata

- `MiniMockito.Net`: `Version` のみ更新（PackageId / Authors / Description / PackageTags /
  RepositoryUrl / PackageLicenseExpression / GenerateDocumentationFile は確認のみ・変更なし）。
- `MiniMockito.Shims.Experimental`: `Version` 更新 + `Description` を Phase 20/21/23/24
  （cross-assembly new interception + Easy API `ReplaceNew` + inspection API
  `GetValue<T>`/`GetCollection`/`ShimsObject`）反映に更新し、experimental / test-only /
  API may change / `[DoNotParallelize]` 必須 / BCL static 未対応 / production in-place rewrite はしない
  を明記。

### docs / README

- README の `PackageReference` / `dotnet pack` 出力例のバージョンを新版に更新。
- 実験パッケージ同梱 README（`src/MiniMockito.Shims.Experimental/README.md`）に Easy API
  （`ReplaceNew`）と警告（test-only / production in-place rewrite なし / BCL static 未対応）を追記。
- `RELEASE_NOTES.md` を新規作成し、両パッケージの版ごとの変更点を記載。

### 検証（Release）

- `dotnet clean` / `dotnet restore` / `dotnet build -c Release` / `dotnet test -c Release`
- `dotnet pack -c Release -o artifacts`（両パッケージ）
- nupkg 内容確認:
  - `MiniMockito.Net`: `lib/net8.0`, `lib/net48`（dll + xml）
  - `MiniMockito.Shims.Experimental`: `lib/net8.0`, `lib/net48`（dll + xml）
  - 余計な test assembly が含まれていないこと

### 対象外（Phase 22）

- nuget.org への push、API key の保存、GitHub release 作成、新機能追加、破壊的変更。

---

## Phase 24 — rewritten object inspection API（追記）

### 目標

新しい interception 機能は追加せず、`ForAssembly(...).ReplaceNew(...)` で rewrite された object graph を、
**型 identity mismatch を無理に解消せず** `object` のまま安全に検証できる inspection / reflection helper を追加する。

### 追加 inspection API

`Shims` facade:

| API | 内容 |
|-----|------|
| `GetValue(object, string path)` / `GetValue<T>(object, string path)` | path 評価 +（型付き時）変換 |
| `GetProperty(object, string name)` / `GetProperty<T>(object, string name)` | 単一プロパティ/フィールド読み取り |
| `Inspect(object)` → `ShimsObject` | object wrapper |
| `GetCollection(object, string path)` → `ShimsCollection` | collection wrapper |

wrapper / helper:

- `ShimsObject`（`Instance` / `GetValue` / `GetValue<T>` / `Get<T>` / `GetProperty(<T>)` / `GetObject` / `GetCollection`）
- `ShimsCollection : IEnumerable<ShimsObject>`（`Instance` / `Count` / `this[int]` / `GetRawItem` / `ToList`）
- internal: `ShimsPathEvaluator` / `ShimsReflectionAccessor`、例外 `ShimsInspectionException`

### path syntax の対応範囲

- property / field: `Items`, `Items.Count`, `SelectedUser.Name`
- indexer: `Items[0]`, `Items[0].Name`, `Rows[1].Cells[2].Text`
- `Count`: public `Count` / `ICollection.Count` / `ICollection<T>.Count` / `IReadOnlyCollection<T>.Count` /
  最終手段として `IEnumerable` 列挙数
- path 途中 null / 存在しないプロパティ / index 範囲外 / malformed は `ShimsInspectionException`
  （requested path / failed segment / runtime type / reason を含む）

### collection / ObservableCollection 対応

- `IEnumerable` / `IList` / array / `IReadOnlyList<T>` / `ICollection` / `ICollection<T>` / `ObservableCollection<T>`
- `ObservableCollection<T>` は BCL collection として扱い、要素 `T` が rewritten type でも wrapper で検証可能

### 型 identity mismatch への扱い

- `GetValue<T>` は assignable ならそのまま、primitive / enum / string / value 型は変換、`T==object` は raw を返す。
- **rewritten 参照型を同名 original 型へ強制 cast しない**。変換不可時は `ShimsInspectionException` に
  「rewritten object may belong to a different load context / use object or inspection API /
  use GetValue<T> for primitive properties」を案内。

### Create<T>() との関係

- 本 Phase で `Create<T>()` の identity 問題は解消しない。安全でない場合は従来どおり例外。
- cross-assembly / rewritten シナリオは `CreateObject(...)` + `Invoke(...)` + inspection API を基本にする。

### 追加テスト

- `tests/MiniMockito.Shims.Experimental.Tests/InspectionApiTests.cs`（net8, 12 件）
- `tests/MiniMockito.Shims.Experimental.Net48Tests/Net48InspectionApiTests.cs`（net48, 5 件）
- テスト用 sample: `CrossAssemblySample.UserViewModel` / `UserItem`（`ObservableCollection<UserItem> Items`、
  fake external db から `Items` / `SelectedUser` を構築する `Load` / `LoadMany`）。

### 対象外（Phase 24）

- 型 identity mismatch の根本解決、rewritten→original の自動変換、BCL static method mocking、
  external assembly 自体の rewrite、production in-place rewrite、WPF binding 完全統合、
  expression-based property path API。

---

## Phase 25 — instance method call shim（追記）

### 目標

`new` 差し替え（Phase 20/21/23）・user-defined static 差し替え（Phase 14）に続く第3の差し替え種別＝
**インスタンスメソッド呼び出しの差し替え（method shim）** を追加。**呼び出し側（rewrite 対象アセンブリ）の
call site IL を書き換える**方式なので、メソッドが virtual かどうかに関係なく差し替えられる（subclass
override 不可なメソッドも対象）。declaring 型のアセンブリ（外部 DLL / EntityFramework 等）は書き換えない。

### 追加 API

| API | 内容 |
|-----|------|
| `NewInterceptionHarness.WithMethodTarget<T>(methodName, returnSubstituteInterface?)` | call site allowlist 登録 |
| `NewInterceptionHarness.WithMethodTarget(Type, methodName, ...)` / `(assemblyPath, typeFullName, methodName, ...)` | Type / 文字列指定 |
| `NewInterceptionHarness.RegisterMethodShim(Type/typeFullName, methodName, Func<receiver, args, result>)` | shim 本体登録（last wins） |
| `Shims.ReplaceMethod<T>(...)` / `ReplaceMethod(Type, ...)` / `ReplaceMethod(assemblyPath, typeFullName, ...)` | Easy API |
| `ShimDispatcher.TryInvokeMethod(key, receiver, args, out result)` | rewrite 済み call site の入口 |
| `MethodShimRegistry` / `ShimContext.MethodRegistry` / `LastMethodShimResolved` | registry / 診断 |

### call-site 書き換えの方式

- 一致した `call`/`callvirt`（instance）を、生成した **concrete 静的ラッパー**呼び出しに置換。
  ラッパーは `receiver`＋boxed args を組み立て、`ShimDispatcher.TryInvokeMethod` を呼び、
  ヒット時は登録 shim の戻り値を（戻り値型へ cast して）返し、未ヒット時は**実メソッドを呼ぶ（フォールバック）**。
- **ジェネリックメソッドは call site の具象インスタンス化（例 `Query<GatewayItem>`）ごとに非ジェネリックな
  concrete ラッパーを生成**する（ジェネリックメソッド emit を避け IL リスクを低減）。型引数は 1 個まで。
- **戻り値型の差し替え**: 宣言戻り値型が生成不可能な具象型（内部 ctor。EF の `DbRawSqlQuery<T>` 相当）でも、
  結果が直後に **interface（`IEnumerable<T>` 等）として消費**されるなら、ラッパー戻り値型をその interface に
  差し替えて置換する（利用者が open generic interface を指定）。消費先が call/callvirt でない等で安全に
  差し替えられない場合は skip + 診断。
- 型 identity: method target の declaring 型アセンブリ（rewrite 対象でないもの）と要素型アセンブリは
  parent ALC から共有する（canned データの要素型が rewrite 後 identity と一致するように）。

### 対象 / 対象外

- 対象: 非 BCL 宣言型の public インスタンスメソッド（virtual/非 virtual）、単純引数、型引数 1 個のジェネリック、
  生成可能な戻り値型＋上記の interface-consumed 差し替え、no match フォールバック、internal/external 両対応。
- 対象外: BCL 宣言型メソッド、`ref`/`out`/`params`、複数型引数、ジェネリックパラメータ型の引数、
  生成不可能な具象を**そのまま具象として**返すケース、プロパティ/インデクサ、static の新規。

### 追加テスト

- `tests/MiniMockito.Shims.Experimental.Tests/MethodShimTests.cs`（net8, 7 件）
- `tests/MiniMockito.Shims.Experimental.Net48Tests/Net48MethodShimTests.cs`（net48, 4 件）
- サンプル: `ExternalLib.ExternalGateway`（`GetName` / `Query<T>` / `RawQuery<T>`）・`GatewayItem`・
  `RawResult<T>`（internal ctor）、`CrossAssemblySample.GatewayUserService`（`Run` / `LoadRows` / `LoadRawRows`）。

### 実案件への適用（EF）

- repository が `List<T>` / DTO を返すメソッドは method shim で差し替え可能。
- EF の `context.Database.SqlQuery<T>(sql).ToList()` も、戻り値が `IEnumerable<T>` として即消費されるため、
  `Database` 型・`SqlQuery` メソッドを method target にして `returnSubstituteInterface = typeof(IEnumerable<>)` を
  指定すれば**理論上差し替え可能**（生 SQL を実行せず canned データを返す）。`DbRawSqlQuery<T>` をローカルに格納
  する形は対象外。BCL static（`DateTime.Now` 等）は引き続き未対応。

---

## Phase 25 — Type-Safe Method Replacement API / Signature Validation（hardening）

### 目的

従来の name-only / `Func<object, object[], object>` API では、呼び出し側が戻り値を捨てている
`int` method を void と誤認し、callback が `null` を返すと generated wrapper の `unbox.any int`
で `NullReferenceException` になっていました。本追記では、コメントや呼び出し方ではなく実際の
`MethodInfo` を唯一のシグネチャ情報として登録・rewrite・dispatch を行います。

### 追加 API

| API / 型 | 内容 |
|---|---|
| `ReplaceMethod<TResult>(MethodInfo)` | exact method + typed return |
| `ReplaceMethod<TResult>(Type, name, parameterTypes)` | Type から exact overload を解決 |
| `ReplaceMethod<TTarget, TResult>(name, parameterTypes)` | compile-time target 型版 |
| `ReplaceVoidMethod(...)` | void 専用。`DoNothing` / `Callback` / `Throws` |
| `TypedMethodReplacementBuilder<TResult>` | `WithArguments` / typed `Returns` / `Throws` |
| `MethodReplacementContext` | exact `MethodInfo` / receiver / boxed arguments |
| `ShimMethodSignatureException` | 解決・static/void/return type 等の登録時エラー |
| `ShimReturnTypeMismatchException` | callback 結果と wrapper return type の不一致 |
| `MethodDispatchDiagnostics` | exact signature / backend / rule / return type / fallback |

### 実装方式

- Type 版は `BindingFlags.Instance | Static | Public | NonPublic | FlattenHierarchy` で候補を列挙し、
  parameter count / parameter type の完全一致で1件だけ選ぶ。名前だけで先頭候補を選ばない。
- typed API は public・instance・non-abstract・non-generic method を対象とし、static、by-ref return、
  `ref` / `out` / `in`、pointer を登録時に診断する。
- `MethodInfo.ReturnType` と `typeof(TResult)` は完全一致。void は `ReplaceVoidMethod` へ分離する。
- exact registry key は `DeclaringType::Method(parameter-type-list)`。wrapper cache key に callee の
  full signature と元の `call` / `callvirt` opcode を含め、overload 間で wrapper を共有しない。
- virtual / non-virtual は `MethodInfo.IsVirtual` から判定。現在の Shims session は実インスタンスの
  caller を rewrite するため、両方とも `InstanceCallSiteRewrite` backend を使う。
- dispatcher は callback 結果を wrapper が cast / unbox する前に検証する。non-nullable value type
  への `null`、不正な boxed value、reference type の非 assignable value は
  `ShimReturnTypeMismatchException`。
- exact matcher rule が no-match の場合は legacy catch-all へ流さず、元の call opcode で実メソッドへ
  fallback する。
- legacy name-only API は削除・変更せず advanced API として維持。単一型引数 generic method /
  return interface substitution の既存経路も維持する。

### 診断

登録・dispatch の診断には target type、exact MethodInfo signature、return / parameter types、
instance / static、virtual / non-virtual、selected backend、expected / actual return type、
null for non-nullable value type、candidate overloads、registration source、calling assembly /
method、selected rule、fallback を含めます。

### 追加テスト

- net8: `TypeSafeMethodReplacementTests`
  - MethodInfo / Type / generic target、callback、constructor 内 int call、ignored return
  - void 分離、overload、`Type.EmptyTypes`、optional parameter、virtuality、static rejection
  - Any / Eq / Is / `ShimCaptor`、`Throws`、typed no-match fallback
  - legacy `null -> int` の専用例外と diagnostics
- net48 / C# 7.3: `Net48TypeSafeMethodReplacementTests`
  - constructor 内 int call、generic target、zero args、void、optional parameter、legacy null guard
- 既存 suite により newobj、static、cross-assembly new、Easy `ReplaceNew`、inspection、
  legacy generic method、MiniMockito 本体を回帰確認する。

### 対象外

BCL method interception、DbContext 専用処理、sealed external class proxy、production assembly
in-place rewrite、runtime IL rewrite、CLR Profiling API、detour / method patching、Microsoft Fakes
完全互換、全 instance method interception の全面再設計、source generator API は対象外です。
