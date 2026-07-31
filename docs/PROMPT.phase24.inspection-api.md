# MiniMockito.Shims.Experimental Phase 24 — Rewritten Object Inspection API

AGENTS.md、AGENTS.shims-experimental.md、README.md、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md、および Phase 20 / Phase 21 / Phase 23 の実装・テストを読んでください。

MiniMockito.Shims.Experimental Phase 24 として、rewritten assembly の型 identity 問題を避けるための inspection / reflection helper API を追加してください。

## 背景

Phase 20〜23 により、cross-assembly new interception と Easy Shims API は実装済みです。

ただし、`Shims.ForAssembly(...).ReplaceNew(...)` は target assembly を rewrite して別ロードするため、rewritten assembly 内の型とテスト側が参照している元の型の identity が一致しないケースがあります。

特に以下のようなケースで問題が顕在化します。

- `Create<T>()` で strongly typed に戻せない
- rewritten object をテスト側の元の型に cast できない
- `ObservableCollection<T>` の `T` が target assembly 内の型で、original / rewritten type mismatch が起きる
- ViewModel / Model / DTO / collection / WPF binding 用 object graph をそのまま strongly typed に検証しようとして失敗する
- generic collection の要素型が rewritten assembly 側の型になる

この Phase の目的は、型 identity mismatch を無理に解消することではありません。

rewritten object graph を `object` のまま安全に観察・検証できる high-level inspection API を追加することです。

## 目的

利用者が以下のように書けるようにしてください。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalDbContext",
                            fakeContext))
{
    var vm = shims.CreateObject("TargetApp.UserViewModel");

    shims.Invoke(vm, "Load");

    var count = shims.GetValue<int>(vm, "Items.Count");
    var firstName = shims.GetValue<string>(vm, "Items[0].Name");

    Assert.AreEqual(1, count);
    Assert.AreEqual("fake-1", firstName);
}
```

また、collection を wrapper として扱えるようにしてください。

```csharp
using (var shims = Shims.ForAssembly(targetAssemblyPath)
                        .ReplaceNew(
                            externalAssemblyPath,
                            "ExternalLib.ExternalDbContext",
                            fakeContext))
{
    var vm = shims.CreateObject("TargetApp.UserViewModel");

    shims.Invoke(vm, "Load");

    var items = shims.GetCollection(vm, "Items");

    Assert.AreEqual(1, items.Count);
    Assert.AreEqual("fake-1", items[0].Get<string>("Name"));
}
```

## 重要方針

この Phase では、新しい interception 機能は追加しません。

既存の `Shims.ForAssembly(...)` / `ReplaceNew(...)` / `CreateObject(...)` / `Invoke(...)` の上に、検証用 helper を追加してください。

既存 low-level API は壊さないでください。
public API の破壊的変更は行わないでください。

## 実装対象 API

`ShimsSession` / `Shims` facade に以下を追加してください。

```csharp
public object GetValue(object instance, string path);

public T GetValue<T>(object instance, string path);

public object GetProperty(object instance, string propertyName);

public T GetProperty<T>(object instance, string propertyName);

public ShimsObject Inspect(object instance);

public ShimsCollection GetCollection(object instance, string path);
```

`ShimsObject` wrapper を追加してください。

```csharp
public sealed class ShimsObject
{
    public object Instance { get; }

    public object GetValue(string path);

    public T GetValue<T>(string path);

    public object GetProperty(string propertyName);

    public T GetProperty<T>(string propertyName);

    public ShimsObject GetObject(string path);

    public ShimsCollection GetCollection(string path);
}
```

`ShimsCollection` wrapper を追加してください。

```csharp
public sealed class ShimsCollection : IEnumerable<ShimsObject>
{
    public object Instance { get; }

    public int Count { get; }

    public ShimsObject this[int index] { get; }

    public object GetRawItem(int index);

    public IReadOnlyList<ShimsObject> ToList();
}
```

必要に応じて internal helper を追加してください。

候補:

- `ShimsPathEvaluator`
- `ShimsReflectionAccessor`
- `ShimsInspectionException`

## Path syntax

最低限、以下をサポートしてください。

### Property access

```text
Items
Items.Count
SelectedUser.Name
```

### Indexer access

```text
Items[0]
Items[0].Name
Rows[1].Cells[2].Text
```

### Count

`Items.Count` は以下のいずれかで取得できるようにしてください。

- public `Count` property
- `ICollection.Count`
- `ICollection<T>.Count`
- `IReadOnlyCollection<T>.Count`
- fallback として `IEnumerable` の enumerate count

### Nullable / null

path の途中で null が見つかった場合は、分かりやすい `ShimsInspectionException` を投げてください。

例外メッセージには以下を含めてください。

- requested path
- failed segment
- null was encountered

### Missing property / invalid index

property が存在しない場合、または index が範囲外の場合は、分かりやすい `ShimsInspectionException` を投げてください。

例外メッセージには以下を含めてください。

- requested path
- failed segment
- target runtime type
- reason

## 型変換

`GetValue<T>` は以下をサポートしてください。

- 既に `T` に assignable ならそのまま返す
- primitive / enum / string / decimal / DateTime などの一般的な値は必要に応じて `Convert.ChangeType` 等で変換
- nullable value type への変換
- `T` が `object` の場合は raw object を返す
- `T` に変換できない場合は `ShimsInspectionException`

重要:

rewritten assembly 内の型を、テスト側の同名 original type に無理に cast しないでください。

型 identity mismatch が疑われる場合は、例外メッセージで以下を案内してください。

- rewritten object may belong to a different load context / assembly identity
- use object / inspection API instead of strongly typed cast
- use GetValue<T> for primitive properties

## Collection handling

`ShimsCollection` は以下をサポートしてください。

- `IEnumerable`
- `IList`
- array
- `IReadOnlyList<T>`
- `ICollection`
- `ICollection<T>`
- `ObservableCollection<T>`

`ObservableCollection<T>` は BCL collection として扱い、中の `T` が rewritten type でも wrapper で検証できるようにしてください。

例:

```csharp
var items = shims.GetCollection(vm, "Items");

Assert.AreEqual(2, items.Count);
Assert.AreEqual("A", items[0].Get<string>("Name"));
Assert.AreEqual("B", items[1].Get<string>("Name"));
```

## Create<T>() との関係

この Phase で `Create<T>()` の型 identity 問題を解消しようとしないでください。

`Create<T>()` が安全でない場合は、これまで通り分かりやすい例外を投げてください。

docs では、以下の方針を明記してください。

- `Create<T>()` は型 identity が一致する場合だけ使う
- `ForAssembly(...)` を使う cross-assembly / rewritten assembly シナリオでは `CreateObject(...)` を基本にする
- complex object graph / collection / DTO を検証する場合は inspection API を使う

## MSTest

以下を追加または更新してください。

1. `GetValue<T>(object, "Property")` で primitive property を取得できる
2. `GetValue<T>(object, "Nested.Property")` で nested property を取得できる
3. `GetValue<T>(object, "Items.Count")` で collection count を取得できる
4. `GetValue<T>(object, "Items[0].Name")` で collection item property を取得できる
5. `GetCollection(object, "Items")` で `ShimsCollection` を取得できる
6. `ShimsCollection.Count` が取得できる
7. `ShimsCollection[index].Get<T>("Property")` が使える
8. `ObservableCollection<T>` の `T` が rewritten type でも property を取得できる
9. path の途中が null の場合に `ShimsInspectionException` が分かりやすい
10. property が存在しない場合に `ShimsInspectionException` が分かりやすい
11. index out of range の場合に `ShimsInspectionException` が分かりやすい
12. `GetValue<object>` は raw object を返す
13. strongly typed cast できない rewritten object でも inspection API で検証できる
14. `ShimsObject` wrapper 経由で nested / collection access ができる
15. net48 / C# 7.3 でも同じ inspection API が使える
16. 既存 Phase 20 / 21 / 23 tests が壊れていない
17. 既存 low-level API tests が壊れていない
18. 既存 MiniMockito 本体 tests が壊れていない

テスト用 sample として、target assembly 側に以下のような汎用 sample を追加して構いません。

- `TargetApp.UserViewModel`
- `TargetApp.UserItem`
- `ObservableCollection<UserItem> Items`
- `Load()` が fake external db から値を受け取り `Items` に詰める

実案件固有の型名・DLL名は使わないでください。

## docs

以下を更新してください。

- `docs/shims-experimental-quickstart.md`
- `docs/shims-net48-compatibility-design.md`
- `docs/shims-experimental-phase14-milestone.md`
- `README.md` の Shims.Experimental セクション

docs の方針:

- `ForAssembly(...).ReplaceNew(...)` では型 identity が変わる可能性があることを明記
- `Create<T>()` より `CreateObject(...)` + `Invoke(...)` が安全な場合があることを明記
- `ObservableCollection<T>` や complex object graph は inspection API で検証する例を追加
- rewritten type を original type に無理に cast しない方針を明記
- `GetValue<T>` / `GetCollection` / `ShimsObject` の使い方を紹介
- net48 sample は `using` statement 形式にする
- BCL static method は未対応と明記する
- DbContext 系では手動 fake を `ReplaceNew` に渡し、結果検証は inspection API を使う例を追加

## この Phase で実装しないもの

- 型 identity mismatch の根本解決
- rewritten object を original type に自動変換する機能
- BCL static method mocking
- DateTime.Now mocking
- File.ReadAllText mocking
- sealed external class mocking
- DbContext 専用処理
- external assembly 自体の rewrite
- production assembly in-place rewrite
- runtime IL rewrite
- CLR Profiling API
- detour / method patching
- Microsoft Fakes Shim 完全互換
- WPF binding engine の完全統合
- expression-based property path API

## C# 7.3 / net48 制約

net48 tests と docs の sample では以下を守ってください。

- `using var` は使わない
- `using (...) { }` を使う
- nullable reference syntax は使わない
- target-typed new は使わない
- collection expression は使わない
- record / init / file-scoped namespace は使わない
- switch expression は使わない
- async streams は使わない

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

## 報告

最後の報告は日本語でお願いします。

報告には以下を含めてください。

- 追加した inspection API
- path syntax の対応範囲
- collection / ObservableCollection 対応
- 型 identity mismatch への扱い
- `Create<T>()` との関係
- 追加したテスト
- docs 更新内容
- dotnet build / dotnet test の結果
