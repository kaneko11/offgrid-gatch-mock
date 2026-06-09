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
