# MiniMockito.Shims.Experimental Phase 25 — Instance Method Call Shim (PoC)

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および Phase 20 / 21 / 23 / 24 の実装・テストを読んでください。

MiniMockito.Shims.Experimental Phase 25 として、**インスタンスメソッド呼び出しの差し替え（method shim）** の最小 PoC を実装してください。

## 背景

Phase 20〜24 で、`newobj` 差し替え（cross-assembly 含む）、user-defined `static` 差し替え、Easy API（`ReplaceNew`）、inspection API が実装済みです。

しかし、次のような **インスタンスメソッド呼び出し** は今のところ差し替えられません。

```csharp
// TargetApp.dll 側のメソッド本体
var gateway = new ExternalLib.ExternalGateway();
var rows = gateway.Query<UserItem>("...").ToList();   // ← この Query 呼び出しを差し替えたい
var name = gateway.GetName(1);                         // ← この非 virtual メソッド呼び出しも差し替えたい
```

これらは interface でも virtual でもないため proxy では差し替えられず、サブクラスの override もできません（`Query` は非 virtual / ジェネリック）。

ただし重要なのは、**呼び出し命令（call site）自体は rewrite 対象アセンブリ（TargetApp.dll）の IL の中にある**ということです。`newobj` / `static` と同じく、**呼び出し側 IL を書き換える**方式なら、メソッドが virtual かどうかに関係なく差し替えられます（subclass override の制約とは別レイヤー）。

この Phase では、この「インスタンスメソッド呼び出しの call-site 差し替え」を限定スコープで PoC として実装します。

## 目的

利用者が、TargetApp.dll 内で呼ばれているインスタンスメソッド（非 virtual／ジェネリック含む）を、**実メソッドを実行せずに固定値へ差し替え**られるようにする。

実案件固有の型名・DLL 名はハードコードしないでください。`CommonModels` / `SP_USER_DATEntities` / `車両販売_買取下取一覧` などは **docs の「実案件適用例」としてのみ**扱い、実装・テスト・API 名には使わないでください。汎用の `ExternalLib` / `TargetApp`（`CrossAssemblySample`）サンプルで検証してください。

## 重要方針

- 新しい差し替え種別（method shim）を**追加**する。既存の newobj / static / Easy API / inspection API を**壊さない**。
- public API の**破壊的変更は行わない**（追加のみ）。
- **rewrite 対象アセンブリの call site だけ**を書き換える。メソッドの**宣言型が属するアセンブリ（外部 DLL / EntityFramework 等）は書き換えない**。
- BCL（`mscorlib` / `System.Private.CoreLib`）に定義されたメソッドは**対象外**のまま（call site は user 側でも、まず非 BCL 宣言型に限定する）。
- production assembly の in-place rewrite はしない。runtime IL rewrite / CLR Profiling / detour はしない。
- Microsoft Fakes Shim 完全互換は**目標外**（限定 PoC）。
- net8 / net48 両対応。net48 / C# 7.3 のサンプルは `using` statement 形式（`using var` 禁止）。
- `[assembly: DoNotParallelize]` 必須。

## この Phase の対象（スコープ）

- **public, non-generic な宣言型**の **public インスタンスメソッド**呼び出しの差し替え（virtual / non-virtual 問わず）。
- 宣言型は **user-defined / 外部アセンブリ型（BCL 以外）**。
- メソッド引数は **値型・string・参照型などの単純な引数**まで。
- **ジェネリックインスタンスメソッド（型引数 1 個）** を最低限サポート（例: `IEnumerable<T> Query<T>(string sql)`）。
- 戻り値型は **テスト側で生成・登録できる型**（`int` / `string` / DTO / `List<T>` / `IEnumerable<T>` など）。
- **【拡張】戻り値がインターフェース（`IEnumerable<T>` 等）として即消費される call site**は、
  たとえ宣言戻り値型が生成不可能な具象型（内部 ctor 等）でも、**ラッパーの戻り値型をその消費先インターフェース型に
  差し替えて**置換できること（後述「戻り値型の差し替え」）。
- no match 時は **元メソッド呼び出しにフォールバック**。
- internal target（rewrite 対象アセンブリ自身の型）と external target（別アセンブリの型）の**両方**で動くこと。

## この Phase で対象外（PoC では実装しない）

- BCL（`mscorlib` / `System.Private.CoreLib`）定義メソッドの差し替え（`DateTime.Now` 等）。
- `ref` / `out` / `params` 引数、複数型引数の複雑なジェネリック、ジェネリック宣言型。
- プロパティ / インデクサ / イベント / 演算子の差し替え。
- 内部コンストラクタでしか生成できない戻り値型を、**そのままの具象型として返す**こと
  （生成できないため）。ただし下の「戻り値型の差し替え」が成立する消費パターンは対象に含める。
- static メソッドの新規差し替え（既存 `Shim.Static` の範囲）。
- expression-based API。
- production / runtime / profiler / detour 系。

## 目標 API（候補）

API 名・形は実装時に調整して構いませんが、以下のイメージを満たしてください。

### 低レベル（NewInterceptionHarness）

```csharp
using (var harness = NewInterceptionHarness.Create()
    .WithMethodTarget(typeof(ExternalGateway), "Query")     // 差し替えたいメソッド（宣言型 + 名前）
    .RewriteAssembly(targetAssemblyPath))
using (ShimContext.Create())
{
    // 実メソッドを呼ばず、固定値を返す
    harness.RegisterMethodShim(typeof(ExternalGateway), "Query",
        (receiver, args) => new List<UserItem> { new UserItem("fake-1") });

    var vm = harness.CreateObject("TargetApp.UserViewModel");
    harness.Invoke(vm, "Load");
}
```

### 高レベル（Shims facade）

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
    .ReplaceMethod(externalAssemblyPath, "ExternalLib.ExternalGateway", "GetName",
        (receiver, args) => "fake-" + args[0]))
{
    var vm = shims.CreateObject("TargetApp.UserViewModel");
    var result = shims.Invoke<string>(vm, "Run", 1);   // 内部の gateway.GetName(1) が "fake-1" に
}
```

### 引数条件・戻り値型を明示する builder（任意）

```csharp
shims.Method<string>("ExternalLib.ExternalGateway", "GetName")
     .WithArguments(ShimArg.Eq(1))
     .Returns("fake-1");
```

`WithArguments` / matcher は既存 `ShimArg`（Any / Eq / Is）を流用できるなら流用する。重すぎる場合は catch-all（引数無条件）だけで良く、その旨を docs に明記する。

## 実装詳細

### 1. call-site 書き換え（rewriter）

- rewrite 対象アセンブリの各メソッド本体をスキャンし、`call` / `callvirt` のうち
  「宣言型・メソッド名（と必要ならシグネチャ）が allowlist に一致する instance メソッド呼び出し」を検出。
- 一致した call site を、`ShimDispatcher` 上の **wrapper/dispatch メソッド呼び出し**へ置換する。
  - wrapper は **receiver（this）と引数を受け取り**、登録された shim があればその戻り値を返し、
    無ければ**元のメソッドを呼ぶ（フォールバック）**。
- **wrapper の戻り値型は、元メソッドの戻り値型 R と IL 検証上整合**させること（R と同じ、または R へ代入可能）。

### 1b. 戻り値型の差し替え（interface-consumed return substitution）

R が生成不可能な具象型（内部 ctor 等。EF の `DbRawSqlQuery<T>` 相当）でも、**call site の直後で結果が
インターフェース型 I（例: `IEnumerable<T>`）として消費されている**場合は、置換できるようにする。

- 方式: wrapper の戻り値型を **R ではなく I** にして call site を置換する。
  - shim ヒット時: 登録された I の値（`(I)result`）を返す（canned データ。例: `List<T>`）。
  - フォールバック時: 実メソッドを呼ぶ。実メソッドの戻り値 R は I へ暗黙アップキャストできる（`R : I`）。
- **消費先の検証**: 置換しても IL が valid になるのは「直後の命令が I を受け取れる」場合だけ。
  rewriter は call site の**直後の消費命令**（例: `call ToList<T>(IEnumerable<T>)` や `foreach` の
  `GetEnumerator`）を見て、I が代入可能かを判定する（簡易ピープホールで可）。
  判定できない・元の具象型 R のローカルへ格納している等で I に差し替えると壊れる場合は **skip + 診断**。
- I（消費先インターフェース）は、API で利用者が明示できるようにする
  （例: `ReplaceMethod<IEnumerable<UserItem>>(...)`）。これにより rewriter は「戻り値を I に差し替える」と判断する。
- これにより、`gateway.Query<T>(sql).ToList()` のように
  「生成不可能な具象を返すが直後に `IEnumerable<T>` として消費される」call site を差し替えられる。
- ジェネリックインスタンスメソッド（型引数 1 個）の場合は、`GenericInstanceMethod` の型引数を維持して wrapper を生成する。
- `ref` / `out` / `params` / 複数型引数 / BCL 宣言型 / 生成不可能戻り値型は **skip + 診断**。
- 既存 newobj / static の rewrite を壊さないこと。宣言型アセンブリ（外部 DLL）は書き換えないこと。

### 2. dispatcher / registry

- メソッドシグネチャ（宣言型 FullName + メソッド名 + 必要ならパラメータ型 / arity）をキーに、
  shim 本体（`Func<object receiver, object[] args, object? result>` 相当）を登録する registry を追加。
- external 型は Phase 20/21 と同様、`Type` 完全一致でなく **FullName ベース**で照合できるようにする
  （cross-assembly / 別ロードコンテキスト対応）。
- 既存 `ShimContext` / `ShimRuleRegistry` の枠組みに沿わせる（別 registry を足すか、拡張するかは実装判断）。

### 3. フォールバック

- 一致する shim が無い場合は**元メソッドをそのまま実行**する（receiver と args で実呼び出し）。
- これにより「登録した呼び出しだけ差し替わり、それ以外は素通り」になること。

### 4. 既存 inspection API との連携

- 差し替え後の object graph は、Phase 24 の `GetValue<T>` / `GetCollection` / `ShimsObject` で検証できること
  （rewritten 型を cast せずに検証）。

## テスト用サンプル

`ExternalLib`（既存テスト用アセンブリ）に汎用型を追加してください。例:

```csharp
namespace ExternalLib
{
    public class ExternalGateway
    {
        // 非 virtual インスタンスメソッド（実体は「実処理」を表すダミー）
        public string GetName(int id) { return "real-" + id; }

        // ジェネリックインスタンスメソッド（実体は実行されたら例外 or "real" を返す）
        public System.Collections.Generic.IEnumerable<T> Query<T>(string sql)
        {
            throw new System.NotSupportedException("real Query は呼ばれてはならない");
        }

        // 戻り値型の差し替え検証用: 内部 ctor の具象型を返すが、呼び出し側は IEnumerable<T> として消費する
        // （EF の DbRawSqlQuery<T> 相当の状況を汎用サンプルで再現）
        public RawResult<T> RawQuery<T>(string sql)
        {
            throw new System.NotSupportedException("real RawQuery は呼ばれてはならない");
        }
    }

    // 内部コンストラクタのみ（テスト側で直接生成できない）の IEnumerable<T> 実装。
    public class RawResult<T> : System.Collections.Generic.IEnumerable<T>
    {
        internal RawResult() { }   // ← internal ctor（DbRawSqlQuery<T> 相当）
        public System.Collections.Generic.IEnumerator<T> GetEnumerator()
            => throw new System.NotSupportedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => throw new System.NotSupportedException();
    }
}
```

`CrossAssemblySample`（TargetApp 相当）に、上記を**呼び出す**コードを追加してください。例:

```csharp
namespace CrossAssemblySample
{
    public class GatewayUserService
    {
        public string Run(int id)
        {
            var gateway = new ExternalLib.ExternalGateway();
            return gateway.GetName(id);           // ← 非 virtual メソッド差し替えの対象
        }

        public System.Collections.Generic.List<UserItem> LoadRows()
        {
            var gateway = new ExternalLib.ExternalGateway();
            return System.Linq.Enumerable.ToList(gateway.Query<UserItem>("select ..."));  // ← ジェネリックメソッド差し替えの対象
        }

        // 戻り値型の差し替え検証用: RawResult<T>（内部 ctor）を返すが IEnumerable<T> として即消費
        public System.Collections.Generic.List<UserItem> LoadRawRows()
        {
            var gateway = new ExternalLib.ExternalGateway();
            return System.Linq.Enumerable.ToList(gateway.RawQuery<UserItem>("select ..."));  // ← 戻り値型差し替えの対象
        }
    }
}
```

`Query` の実体が例外を投げる設計にすることで、「差し替えが効いている＝実メソッドが呼ばれていない」ことを確実に検証できます。

## MSTest（追加・更新）

1. 非 virtual インスタンスメソッド（`GetName`）の呼び出しが差し替わる
2. ジェネリックインスタンスメソッド（`Query<T>`）の呼び出しが差し替わる
2b. **戻り値型の差し替え**：`RawQuery<T>`（内部 ctor の具象型）を返す call site が、`.ToList()`（`IEnumerable<T>` 消費）経由で差し替わる
3. 差し替え時に**実メソッドが実行されない**（実体の例外が出ない＝固定値が返る）
4. 戻り値（`string` / `List<T>` / `IEnumerable<T>`）が canned 値になる
5. 引数 matcher（Any / Eq）対応（実装する場合）
6. no match 時は元メソッドにフォールバックする
7. allowlist に未登録のメソッド呼び出しは書き換えられない
8. `ref` / `out` / `params` / 複数型引数 / BCL 宣言型 / 生成不可能戻り値型は skip + 診断
9. internal target / external target の両方で差し替えできる
10. Phase 24 inspection API（`GetValue<T>` / `GetCollection`）で結果検証できる
11. 既存 Phase 20 / 21 / 23 / 24 tests が壊れていない
12. 既存 low-level API tests が壊れていない
13. 既存 MiniMockito 本体 tests が壊れていない
14. net8 tests / net48 tests が通る（net48 は C# 7.3・using statement 形式）

## diagnostics

以下を出せるようにし、テストで検証可能にしてください。

- method shim target registered
- method call site detected
- method call site rewritten
- method call site skipped + skipped reason（ref/out/params / generic-arity / BCL / non-returnable return type 等）
- method shim resolved（FullName fallback used 等、Phase 21 と整合）
- fallback to original method

## docs

以下を更新してください。

- docs/shims-experimental-quickstart.md（method shim の使い方・対象/対象外を追記）
- docs/shims-net48-compatibility-design.md（net48 サンプルを using statement 形式で追記）
- docs/shims-experimental-phase14-milestone.md（Phase 25 セクションを追記）
- README.md の Shims.Experimental セクション

docs の方針:

- 「`new` / static に続く第3の差し替え＝インスタンスメソッド呼び出し差し替え」であることを明記。
- **call-site 書き換え方式なので virtual 不要**（subclass override 不可なメソッドも差し替え可）であることを明記。
- 宣言型アセンブリ（外部 DLL / EntityFramework 等）は**書き換えない**ことを明記。
- 対象外（BCL メソッド / ref-out-params / 生成不可能戻り値型）を明記。
- **実案件適用例**として、次の旨を一般論で記載してよい（型名はハードコードしない）:
  > リポジトリが `List<T>` / DTO を返すメソッドなら method shim で差し替え可能。
  > 一方、EF の `context.Database.SqlQuery<T>(sql)` は戻り値 `DbRawSqlQuery<T>` が内部コンストラクタのため
  > 直接差し替えは対象外で、interface 注入 or 結合テスト隔離を推奨。
- `[DoNotParallelize]` 必須・BCL static 未対応を明記。

## ビルド・テスト

最後に以下を実行してください。

```powershell
dotnet build
dotnet test
```

可能なら net48 project 単体も実行してください。

```powershell
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/MiniMockito.Shims.Experimental.Net48Tests.csproj
```

失敗した場合は修正してください。

## 報告（日本語）

- 追加した method shim API（低レベル / 高レベル）
- call-site 書き換えの方式（receiver/args の扱い、ジェネリック型引数の維持、戻り値型の整合、フォールバック）
- 対応範囲（virtual/non-virtual、ジェネリック型引数1個、戻り値型）と対象外（理由つき：BCL / ref-out-params / 生成不可能戻り値型）
- registry / 照合の型キー方式（FullName fallback 含む）
- 既存機能（newobj / static / inspection）との非干渉
- 追加したテスト
- docs 更新内容
- dotnet build / dotnet test の結果
- 実案件（EF 生 SQL 等）に適用する場合の注意点
```
