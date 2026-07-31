# MiniMockito.Shims.Experimental Phase 25 — Type-Safe Method Replacement API / Signature Validation

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および現在の `ReplaceMethod` / method call rewrite / wrapper generation / `Shims.ForAssembly` / `ShimsSession` 関連の実装とテストを読んでください。

MiniMockito.Shims.Experimental Phase 25 として、`ReplaceMethod` の型安全 API、正確なメソッドシグネチャ検証、および戻り値型不一致時の診断改善を実装してください。

## 背景

次のようなメソッドがあります。

```csharp
public int setTableData(
    ComboBox cbo,
    string sql,
    bool set_all = true)
```

呼び出し側では戻り値を使用していないため、一見すると `void` メソッドのように見えます。

```csharp
ComboBoxDataSet cbds = new ComboBoxDataSet();
cbds.setTableData(comShiireeigyou, sql, true);
```

しかし、実際の戻り値型は `int` です。

既存の非型安全な `ReplaceMethod` API で、以下のように callback から `null` を返すコードが生成されました。

```csharp
shims.ReplaceMethod(
    comboBoxDataSetAssemblyPath,
    comboBoxDataSetType.FullName,
    "setTableData",
    (recv, args) => (object)null,
    null);
```

生成 wrapper が callback の戻り値を `System.Int32` に unbox しようとし、仲介コード内で `NullReferenceException` が発生しました。

本来は、たとえば boxed `int` を返す必要があります。

```csharp
(recv, args) => (object)0
```

このような誤りを、AIや利用者がコメント、命名、呼び出し方から誤推測して起こさないようにしてください。

## 必須ルール

以下を実装方針、コード、テスト、README に反映してください。

1. 対象メソッドの戻り値型、引数型、static / instance、virtual / non-virtual を、コメント、メソッド名、呼び出し側が戻り値を受け取っているかどうかから推測しない。
2. 実装前または rule 登録時に、Reflection またはソース定義から正確な `MethodInfo` を取得する。
3. overload のあるメソッドをメソッド名だけで選択しない。
4. `parameterTypes` に `null` を渡すことを推奨しない。zero-argument method の場合は `Type.EmptyTypes` を使用する。
5. 戻り値が non-nullable value type の場合、replacement callback から `null` を返してはいけない。
6. 戻り値が `int` なら boxed `int`、`bool` なら boxed `bool`、enum なら該当 enum など、実際の戻り値型に適合する値を返す。
7. `null` を non-nullable value type へ unbox する前に検証し、`NullReferenceException` ではなく、原因が分かる専用例外を投げる。
8. `void` method と戻り値あり method は別の型安全 API に分ける。
9. 既存の low-level `ReplaceMethod` API は壊さない。ただし README では型安全 API を推奨し、low-level API は advanced 扱いにする。

## この Phase の目的

以下を実現してください。

- `MethodInfo` による正確なメソッド指定
- generic return type による戻り値型のコンパイル時制約
- method return type と `TResult` の runtime 整合性検証
- `parameterTypes` の必須化または明示化
- non-nullable value type に `null` を返した場合の明確な例外
- `void` 専用 API
- overload を安全に選択できる API
- virtual / non-virtual の検出と適切な interception 経路の選択
- net8.0 / net48 / C# 7.3 / net48 x86 対応
- README と quickstart の更新

## 目標 API

### 1. MethodInfo を使用する型安全 API

runtime で `Type` を解決しているケースでは、この形式を第一推奨にしてください。

```csharp
var method = comboBoxDataSetType.GetMethod(
    "setTableData",
    BindingFlags.Instance | BindingFlags.Public,
    null,
    new[]
    {
        typeof(ComboBox),
        typeof(string),
        typeof(bool)
    },
    null);

Assert.IsNotNull(method);
Assert.AreEqual(typeof(int), method.ReturnType);

using (var shims = Shims.ForAssembly(targetAssemblyPath))
{
    shims.ReplaceMethod<int>(method)
         .WithArguments(
             ShimArg.Any<ComboBox>(),
             ShimArg.Any<string>(),
             ShimArg.Eq(true))
         .Returns(0);

    var control = shims.CreateObject(
        "TargetApp.UcRowSetting");
}
```

callback:

```csharp
string capturedSql = null;

using (var shims = Shims.ForAssembly(targetAssemblyPath))
{
    shims.ReplaceMethod<int>(method)
         .Returns(context =>
         {
             capturedSql = (string)context.Arguments[1];
             return 0;
         });

    var control = shims.CreateObject(
        "TargetApp.UcRowSetting");
}
```

### 2. compile-time で対象型を参照できる場合

```csharp
shims.ReplaceMethod<ComboBoxDataSet, int>(
         "setTableData",
         typeof(ComboBox),
         typeof(string),
         typeof(bool))
     .WithArguments(
         ShimArg.Any<ComboBox>(),
         ShimArg.Any<string>(),
         ShimArg.Eq(true))
     .Returns(0);
```

既存 API 命名との整合性を優先する場合は、次でも構いません。

```csharp
shims.ReplaceInstanceMethod<ComboBoxDataSet, int>(
         "setTableData",
         typeof(ComboBox),
         typeof(string),
         typeof(bool))
     .Returns(0);
```

### 3. Type を使用する場合

```csharp
shims.ReplaceMethod<int>(
         comboBoxDataSetType,
         "setTableData",
         typeof(ComboBox),
         typeof(string),
         typeof(bool))
     .Returns(0);
```

### 4. void 専用 API

`void` メソッドでは、戻り値あり API を使わせないでください。

```csharp
shims.ReplaceVoidMethod<ExternalLogger>(
         "Write",
         typeof(string))
     .DoNothing();
```

callback:

```csharp
shims.ReplaceVoidMethod<ExternalLogger>(
         "Write",
         typeof(string))
     .Callback(context =>
     {
         capturedMessage = (string)context.Arguments[0];
     });
```

`MethodInfo` 版:

```csharp
shims.ReplaceVoidMethod(methodInfo)
     .DoNothing();
```

`void MethodInfo` を戻り値あり API へ渡した場合、または戻り値あり `MethodInfo` を `void` API へ渡した場合は、rule 登録時に分かりやすい例外を投げてください。

## 実装対象

候補として以下を追加・整理してください。

- `MethodReplacementContext`
- `TypedMethodReplacementBuilder<TResult>`
- `VoidMethodReplacementBuilder`
- `MethodReplacementValidator`
- `MethodSignatureFormatter`
- `ShimReturnTypeMismatchException`
- `ShimMethodSignatureException`
- `MethodInfo` ベースの `ReplaceMethod<TResult>`
- `Type` ベースの `ReplaceMethod<TResult>`
- generic target 型ベースの `ReplaceMethod<TTarget, TResult>`
- `ReplaceVoidMethod`
- typed `Returns(TResult value)`
- typed `Returns(Func<MethodReplacementContext, TResult>)`
- `Throws(Exception)`
- `WithArguments(...)`
- `Callback(...)` for void
- `DoNothing()` for void
- diagnostics
- XML documentation
- net8 tests
- net48 tests
- net48 x86 tests
- README 更新

既存クラス名・設計に合わせて名称を調整して構いません。

## 正確な MethodInfo 解決

メソッド解決時は以下を確認してください。

- declaring type
- method name
- `BindingFlags`
- instance / static
- public / non-public
- parameter count
- parameter types
- return type
- generic / non-generic
- virtual / non-virtual
- abstract
- ref / out / in
- optional parameter
- overload ambiguity

`parameterTypes` を省略した場合に候補が複数あるなら、勝手に先頭のメソッドを選ばず、`ShimMethodSignatureException` を投げてください。

例外メッセージには以下を含めてください。

- Target type:
- Method name:
- Requested parameter types:
- Candidate methods:
- Reason:
- Hint:

optional parameter であっても、`MethodInfo` の parameter list には含まれます。

次のメソッドは3引数として扱ってください。

```csharp
public int setTableData(
    ComboBox cbo,
    string sql,
    bool set_all = true)
```

## 戻り値検証

### typed API

`ReplaceMethod<TResult>(MethodInfo method)` では、`method.ReturnType` と `typeof(TResult)` を rule 登録時に検証してください。

推奨仕様:

- 完全一致を基本とする
- reference type の場合は安全な assignability を検討してよい
- non-nullable value type は完全一致を優先する
- `void` は拒否する
- by-ref return は初期対応外として明示する

不一致例:

```csharp
shims.ReplaceMethod<string>(intReturningMethod);
```

この場合は rule 登録時に明確な例外を投げてください。

### legacy untyped API

既存の untyped callback が `null` を返した場合、生成 wrapper が unbox する前に検証してください。

次の場合:

```text
actual result: null
expected return type: System.Int32
```

`NullReferenceException` を投げず、以下のような専用例外を投げてください。

```text
Replacement callback returned null for a non-nullable value type.

Method: ComboBoxDataSet.setTableData
Expected return type: System.Int32
Actual value: null

Return a boxed System.Int32 value, for example:
(recv, args) => (object)0

Prefer the type-safe API:
ReplaceMethod<int>(methodInfo).Returns(0)
```

## virtual / non-virtual の扱い

対象メソッドの virtual / non-virtual を `MethodInfo` から判定してください。

コメントや命名から推測しないでください。

- virtual method:
  - 既存 class proxy 経路が適切なら再利用
  - class proxy の typed return 処理を検証
- non-virtual method:
  - 既存 call-site rewrite / wrapper 経路を使用
- static method:
  - instance method API では拒否し、既存 Static API を案内
- abstract method:
  - 適切な既存 proxy 経路または NotSupported
- final virtual:
  - override 不可として診断

どの interception backend を選択したかを internal diagnostics で確認できるようにしてください。

候補:

- ClassProxy
- InstanceCallSiteRewrite
- StaticCallSiteRewrite
- Unsupported

## wrapper generation

戻り値あり wrapper では、callback 結果を return type へ変換する前に検証してください。

non-nullable value type:

```text
result == null
  -> ShimReturnTypeMismatchException
```

value type:

```text
正しい boxed value
  -> unbox.any
```

reference type:

```text
null を許容
assignable であることを確認
```

void:

```text
戻り値を cast / unbox しない
Callback / DoNothing 後に Ret
```

呼び出し元が戻り値を使用していなくても、メソッドシグネチャに適合する値を wrapper から返してください。

## 最小再現テスト

実案件固有名は使わず、汎用 sample を追加してください。

```csharp
public class ExternalTableLoader
{
    public int Load(
        object combo,
        string sql,
        bool setAll = true)
    {
        throw new InvalidOperationException(
            "Real database access");
    }
}
```

```csharp
public class ConstructorCallsIntMethod
{
    public bool Initialized { get; private set; }

    public ConstructorCallsIntMethod()
    {
        var loader = new ExternalTableLoader();

        loader.Load(
            new object(),
            "SELECT * FROM Items",
            true);

        Initialized = true;
    }
}
```

型安全 API を使ったテスト:

```csharp
var method = typeof(ExternalTableLoader).GetMethod(
    "Load",
    new[]
    {
        typeof(object),
        typeof(string),
        typeof(bool)
    });

using (var shims = Shims.ForAssembly(targetAssemblyPath))
{
    shims.ReplaceMethod<int>(method)
         .Returns(0);

    var service = shims.CreateObject(
        "TargetApp.ConstructorCallsIntMethod");

    Assert.AreEqual(
        true,
        shims.GetValue<bool>(
            service,
            "Initialized"));
}
```

## MSTest

最低限、以下を追加してください。

1. `MethodInfo` 版 `ReplaceMethod<int>` で int 戻り値メソッドを差し替えられる
2. generic target 型版 `ReplaceMethod<TTarget, int>` が使える
3. `Type` 版 `ReplaceMethod<int>` が使える
4. callback から int を返せる
5. constructor 内で呼ばれる int 戻り値メソッドを差し替え、constructor が完了する
6. 戻り値を呼び出し元が捨てていても正しい int を返す
7. legacy API で null を返した場合、`NullReferenceException` ではなく `ShimReturnTypeMismatchException` になる
8. int method に `ReplaceMethod<string>` を指定すると登録時に例外
9. void method を `ReplaceMethod<int>` へ渡すと登録時に例外
10. int method を `ReplaceVoidMethod` へ渡すと登録時に例外
11. `ReplaceVoidMethod(...).DoNothing()` が動く
12. overload を `parameterTypes` で正確に選択できる
13. `parameterTypes` が null で overload が複数なら明確な例外
14. `Type.EmptyTypes` で引数なしメソッドを指定できる
15. optional bool parameter を含む3引数シグネチャを正しく解決できる
16. virtual method の `MethodInfo` 判定が正しい
17. non-virtual method の `MethodInfo` 判定が正しい
18. static method を instance API に渡した場合、Static API を案内する例外
19. `Any / Eq / Is` matcher が使える
20. `ShimCaptor` が使える
21. `Throws` が使える
22. no-match 時の call-original が壊れていない
23. generic 戻り値あり method shim が壊れていない
24. newobj shim が壊れていない
25. static shim が壊れていない
26. cross-assembly new interception が壊れていない
27. Easy `ReplaceNew` API が壊れていない
28. inspection API が壊れていない
29. net48 / C# 7.3 で動作する
30. net48 x86 tests が壊れていない
31. 既存 MiniMockito 本体 tests が壊れていない

## diagnostics

以下を含めてください。

- target type
- exact `MethodInfo` signature
- return type
- parameter types
- instance / static
- virtual / non-virtual
- selected backend
- expected return type
- actual replacement return type
- null returned for non-nullable value type
- candidate overloads
- registration source
  - typed API
  - legacy untyped API
- calling assembly / calling method
- selected rule
- fallback to original

## README / docs 更新

最終的に README を必ず修正してください。

更新対象:

- `README.md`
- `docs/shims-experimental-quickstart.md`
- `docs/shims-net48-compatibility-design.md`
- milestone document
- 必要なら API reference document

README では、型安全 API を最初に紹介してください。

### README に追加する推奨例

```csharp
var method = comboBoxDataSetType.GetMethod(
    "setTableData",
    BindingFlags.Instance | BindingFlags.Public,
    null,
    new[]
    {
        typeof(ComboBox),
        typeof(string),
        typeof(bool)
    },
    null);

using (var shims = Shims.ForAssembly(targetAssemblyPath))
{
    shims.ReplaceMethod<int>(method)
         .WithArguments(
             ShimArg.Any<ComboBox>(),
             ShimArg.Any<string>(),
             ShimArg.Eq(true))
         .Returns(0);

    var control = shims.CreateObject(
        "TargetApp.UcRowSetting");
}
```

### README に明記するルール

- コメントや呼び出し方から戻り値型を推測しない
- `MethodInfo` または正確な `parameterTypes` を指定する
- optional parameter もシグネチャに含める
- non-nullable value type に `null` を返さない
- `int` なら `0` など有効な int を返す
- `void` は `ReplaceVoidMethod` を使う
- overload がある場合は `parameterTypes` 必須
- low-level untyped `ReplaceMethod` は advanced API
- 新規コードでは型安全 API を推奨する

### 間違った例

```csharp
// setTableData は int を返すため誤り
shims.ReplaceMethod(
    assemblyPath,
    typeFullName,
    "setTableData",
    (recv, args) => null,
    null);
```

### 正しい例

```csharp
shims.ReplaceMethod<int>(methodInfo)
     .Returns(0);
```

callback:

```csharp
shims.ReplaceMethod<int>(methodInfo)
     .Returns(context =>
     {
         capturedSql =
             (string)context.Arguments[1];

         return 0;
     });
```

## 後方互換性

既存の以下は壊さないでください。

- low-level `ReplaceMethod`
- `ReplaceNew`
- static shim
- newobj shim
- matcher / captor
- net8 / net48 API
- C# 7.3 利用
- existing public API

既存 low-level API を削除・変更しないでください。

必要であれば、XML documentation や README で advanced / unsafe API として位置づけてください。

## この Phase で実装しないもの

- BCL method interception
- DbContext 専用処理
- sealed external class proxy
- production assembly in-place rewrite
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Microsoft Fakes 完全互換
- 全 instance method interception の全面再設計
- source generator API

## ビルド・テスト

最後に以下を実行してください。

```powershell
dotnet build
dotnet test
```

可能なら以下も実行してください。

```powershell
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/MiniMockito.Shims.Experimental.Net48Tests.csproj

dotnet test tests/MiniMockito.Net48X86Tests/MiniMockito.Net48X86Tests.csproj
```

失敗した場合は修正してください。

## 完了報告

最後の報告は日本語でお願いします。

報告には以下を含めてください。

- 追加した型安全 API
- `MethodInfo` 解決方式
- 戻り値型検証方式
- non-nullable value type への null 対策
- void API との分離
- virtual / non-virtual 判定と backend 選択
- legacy API の改善内容
- 追加したテスト
- README / docs 更新内容
- `dotnet build` / `dotnet test` 結果
