# Release Notes

> nuget.org への push はこのリポジトリの CI / 手動運用で行います。本ドキュメントは各パッケージ版の
> 変更点メモです（Phase 22 で整備）。

## MiniMockito.Net

### 0.2.0-preview.7

- net48（PlatformTarget=x86 を含む）での interface mock を RealProxy backend で安定化（Phase 18）。
  net8.0 は引き続き DispatchProxy backend。
- public API の破壊的変更なし（preview.6 からのメタデータ / 安定化更新）。

対象フレームワーク: `net8.0`, `net48`。XML ドキュメント・シンボル（snupkg）同梱。

### 0.2.0-preview.6 以前

- interface mock / spy / stubbing / verification / argument matcher / captor、v2 class proxy
  （public virtual method）など。詳細は README を参照。

## MiniMockito.Shims.Experimental（実験的・テスト専用）

> **⚠️ EXPERIMENTAL / TEST-ONLY。API は予告なく変更されます。production への組み込み不可。**

### 0.1.0-alpha.8

- method shim の canned 戻り値を組み立てるためのヘルパーを追加（Phase 26、`Shims` に追加・後方互換）。
  - `GetRewrittenType(typeFullName)`: 書き換え後アセンブリから型を名前で解決。shim delegate 内で
    canned データの「書き換え後の型」を得るのに使う（同名の元型は load context が違い割り当て不可のため）。
  - `NewObject(typeFullName, properties?)`: 書き換え後の型のインスタンスを生成し、匿名オブジェクト
    （例 `new { 車名 = "A", 車体番号 = 1234 }`）のメンバを**名前一致**で代入。値は必要に応じて変換。
  - `NewList(typeFullName, params rows)`: 各 row（プロパティバッグ）から1要素ずつ作り、
    `List<書き換え後の型>` を返す。`ReplaceMethod` の `IEnumerable<T>` 戻り値差し替えにそのまま使える
    （例: `context.Database.SqlQuery<T>(sql).ToList()`）。
- これにより method shim の delegate が
  `(recv, args) => shims.NewList("My.QueryData", new { 車名 = "A" })` のように1行で書ける。
  - 注意: delegate 内で `shims` を参照するため、`shims` は `using (var shims = ...)` の初期化子内では
    なく**先に宣言**してから `ReplaceMethod` を登録する（C# のローカル変数の制約）。
- public API の破壊的変更なし（追加のみ）。

対象フレームワーク: `net8.0`, `net48`。XML ドキュメント・シンボル（snupkg）同梱。依存: `Mono.Cecil`。

### 0.1.0-alpha.7

- インスタンスメソッドの call-site 差し替え（method shim）を追加（Phase 25）。
  - `Shims.ForAssembly(path).ReplaceMethod(declaringType, methodName, func, substituteInterface?)`、
    `ReplaceMethod<TDeclaring>(...)`、`ReplaceMethod(externalAssemblyPath, typeFullName, methodName, func, substituteInterface?)`。
  - 呼び出し側 IL を書き換えるため、**non-virtual メソッドや型引数 1 個のジェネリックメソッド**も差し替え可能
    （宣言アセンブリの subclass override が不要）。
  - **interface return substitution**: 宣言された戻り値型が構築不可（internal ctor 等。例: EF6 の
    `DbRawSqlQuery<T>`）でも、結果が直後に interface として消費される call site（例: `.ToList()` が
    `IEnumerable<T>` を消費）であれば、ラッパー戻り値をその interface 型にして差し替え可能。
  - `ReplaceNew(...)` と併用して、`new DbContext()` を fake に差し替えつつ
    `context.Database.SqlQuery<T>(sql).ToList()` を canned データへ置換 ―― 実 DB 接続なしで検証できる。
- BCL 宣言型のメソッド（`DateTime.Now` / `File.ReadAllText` 等）は引き続き対象外。
- public API の破壊的変更なし（追加のみ）。

対象フレームワーク: `net8.0`, `net48`。XML ドキュメント・シンボル（snupkg）同梱。依存: `Mono.Cecil`。

### 0.1.0-alpha.6

- Phase 20 / 21 / 23 / 24 の成果をまとめた alpha。
- rewritten object inspection API（Phase 24）を追加:
  - `Shims.GetValue(object, path)` / `GetValue<T>(...)` / `GetProperty(<T>)(...)` / `Inspect(...)` / `GetCollection(...)`
  - wrapper: `ShimsObject`（`GetValue` / `Get<T>` / `GetObject` / `GetCollection`）、
    `ShimsCollection : IEnumerable<ShimsObject>`（`Count` / `this[int]` / `GetRawItem` / `ToList`）
  - property path（`Items.Count` / `Items[0].Name` / `SelectedUser.Name`）で rewritten object graph を
    `object` のまま検証。`ObservableCollection<T>` の要素 `T` が rewritten type でも検証可能。
  - rewritten 参照型を同名 original 型へ強制 cast しない。不一致時は `ShimsInspectionException`。
- public API の破壊的変更なし（追加のみ）。
- 含まれる既存成果: cross-assembly new interception（Phase 20 / 21）、Easy Shims API
  `Shims.ForAssembly(path).ReplaceNew(...)`（Phase 23）。下記 alpha.5 の項目を参照。

対象フレームワーク: `net8.0`, `net48`。XML ドキュメント・シンボル（snupkg）同梱。依存: `Mono.Cecil`。

### 0.1.0-alpha.5

- cross-assembly new interception（Phase 20 / 21）:
  - `WithExternalTarget<T>()` / `WithExternalTarget(Type)` / `WithExternalTarget(string assemblyPath, string typeFullName)`
  - `RegisterShim` の型 / Type / FullName / FullName+assembly 版
  - `ResolveExternalType(...)` / `CreateFakeExternal(...)` と cross-assembly diagnostics
- Easy Shims API（Phase 23）: `Shims.ForAssembly(path).ReplaceNew(...)`
  - 1 session で複数 `ReplaceNew(...)` を登録、internal / external 混在、same target は last stub wins
  - `ShimContext` は session が内部管理（利用者は `ShimContext.Create()` 不要）、`Dispose` で cleanup
  - diagnostics forwarding（`Diagnostics` / `LastDispatchDiagnostics` / `GetAlcDiagnostics()`）
- public API の破壊的変更なし（追加のみ）。

非対応（変更なし）: BCL static method mocking（`DateTime.Now` / `File.ReadAllText` 等）、
production assembly の in-place rewrite、generic / expression-based shim API。
parallel test は不可（`[assembly: DoNotParallelize]` 必須）。

対象フレームワーク: `net8.0`, `net48`。XML ドキュメント・シンボル（snupkg）同梱。依存: `Mono.Cecil`。

### 0.1.0-alpha.4 以前

- newobj interception / user-defined static method mocking / 高レベル `Shims.For<T>()` facade など。
