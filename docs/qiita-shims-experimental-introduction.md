# C#で `new` を差し替える実験的モックツールを作っている話

C# のテストで、次のようなコードに出会うことがあります。

```csharp
namespace TargetApp;

public class UserService
{
    public string GetDisplayName(int id)
    {
        using var db = new ExternalLib.ExternalDbContext();
        return db.FindName(id);
    }
}
```

テストでは DB に接続したくないので `ExternalDbContext` を fake に替えたい。しかし、`UserService` は依存をコンストラクターやプロパティから受け取らず、メソッドの中で直接 `new` しています。

このようなコードを対象に、`new SomeClass()` をテスト時だけ差し替える実験的なツール `MiniMockito.Shims.Experimental` を作っています。

この記事では、なぜ通常の mock では `new` を扱いづらいのか、cross-assembly new interception をどのように実現しているのか、そして IL リライトによって生じる型 identity の問題をどう検証しているのかを紹介します。

> `MiniMockito.Shims.Experimental` は名前のとおり experimental / test-only です。Microsoft Fakes の完全互換を目指すものではなく、本番コードへの組み込みも想定していません。

## 通常の mock では、なぜ `new` を差し替えづらいのか

一般的な mock は、interface の実装や virtual メソッドを持つ proxy オブジェクトを作り、その proxy に対する呼び出しを記録・差し替えします。

たとえば依存を外から受け取れるなら、素直にテストできます。

```csharp
public class UserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public string GetDisplayName(int id)
        => _repository.FindName(id);
}
```

この形なら、テスト側で `IUserRepository` の mock を作り、`UserService` に渡せば済みます。

一方、最初の例では `ExternalDbContext` の生成を製品コード自身が決めています。テスト側が fake を作っても、その fake を `UserService` に渡す入口がありません。

また、class proxy で差し替えられるのは、基本的に「proxy に対して呼ばれた virtual メソッド」です。メソッド本体に埋め込まれた `new ExternalDbContext()` が、どのインスタンスを生成するかまでは変えられません。

もちろん、長期的には依存性注入へリファクタリングするのが第一候補です。ただし、既存コード、生成元を変えられないコード、移行途中のコードなど、すぐには直せない場面もあります。その退避路を小さく検証するのが、この実験の目的です。

## MiniMockito.Shims.Experimental の概要

`MiniMockito.Shims.Experimental` は、テスト対象アセンブリのコピーを Mono.Cecil で読み、allowlist に登録した `newobj` 命令を書き換えます。

概念的には、次の変換を行います。

```text
new ExternalLib.ExternalDbContext()
        ↓ IL rewrite
ShimDispatcher.New<ExternalLib.ExternalDbContext>()
        ↓
登録済みの fake があれば fake、なければ実コンストラクター
```

大まかな流れは次のとおりです。

1. 書き換えるアセンブリと、差し替える型を明示する
2. 元のアセンブリを一時ディレクトリへコピーして IL を書き換える
3. 書き換え済みアセンブリを別のロード境界で読み込む
4. 書き換え済みコード内の `new` が dispatcher を経由して fake を取得する
5. `using` の終了時に shim rule とローダーを片付ける

元のアセンブリを in-place で上書きすることはありません。対象も明示登録された型に限定しています。

執筆時点のプロジェクトは `net8.0;net48` のマルチターゲットで、NuGet パッケージのバージョンは alpha です。

```xml
<PackageReference
    Include="MiniMockito.Shims.Experimental"
    Version="0.1.0-alpha.8" />
```

shim の状態はプロセス内で共有されるため、MSTest のテストアセンブリでは並列実行を無効にします。

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]
```

## cross-assembly new interception

実際のコードでは、`new` される型と、その `new` を呼ぶコードが別 DLL にあることが多いです。

```text
ExternalLib.dll
  └─ ExternalLib.ExternalDbContext

TargetApp.dll
  └─ TargetApp.UserService
       └─ new ExternalLib.ExternalDbContext()
```

この場合、書き換えるのは call site を持つ `TargetApp.dll` です。`ExternalLib.dll` 自体は書き換えません。

リライト後も `ExternalDbContext` への assembly reference は維持します。外部型のアセンブリは親側から共有し、テスト側で作った fake と、書き換え済みコードが要求する `ExternalDbContext` の identity が一致するようにしています。

外部型の shim rule は型の完全名を中心に照合し、assembly simple name も診断に使います。同じ完全名の型が複数の外部アセンブリにある構成は曖昧になるため、diagnostics で重複リスクを確認する必要があります。

これが cross-assembly new interception です。

## Easy API: `Shims.ForAssembly(...).ReplaceNew(...)`

初期の実装では、ハーネス、ロード処理、shim context、rule 登録を個別に操作していました。現在は、よく使う cross-assembly のケースを `Shims.ForAssembly(...).ReplaceNew(...)` にまとめています。

例として、外部ライブラリ側に次のクラスがあるとします。

```csharp
namespace ExternalLib;

public class ExternalDbContext : IDisposable
{
    public virtual string FindName(int id)
        => "real-" + id;

    public void Dispose()
    {
    }
}
```

テスト用の fake は手書きの subclass にします。

```csharp
private sealed class FakeExternalDbContext
    : ExternalLib.ExternalDbContext
{
    public override string FindName(int id)
        => "fake-" + id;
}
```

テストは次のように書けます。

```csharp
[TestClass]
[DoNotParallelize]
public sealed class UserServiceTests
{
    [TestMethod]
    public void DirectNew_IsReplaced()
    {
        var targetAssemblyPath =
            typeof(TargetApp.UserService).Assembly.Location;

        var externalAssemblyPath =
            typeof(ExternalLib.ExternalDbContext).Assembly.Location;

        var fakeContext = new FakeExternalDbContext();

        using (var shims = Shims
            .ForAssembly(targetAssemblyPath)
            .ReplaceNew(
                externalAssemblyPath,
                "ExternalLib.ExternalDbContext",
                fakeContext))
        {
            var service =
                shims.CreateObject("TargetApp.UserService");

            var result = shims.Invoke<string>(
                service,
                "GetDisplayName",
                1);

            Assert.AreEqual("fake-1", result);
        }
    }
}
```

ポイントは、テスト対象を通常の `new TargetApp.UserService()` では作らず、書き換え済みアセンブリから `CreateObject` で生成することです。メソッド呼び出しも `Invoke<T>` を使います。この理由は後述する型 identity にあります。

外部型をコンパイル時に参照できる場合は、次の overload も使えます。

```csharp
using (var shims = Shims
    .ForAssembly(targetAssemblyPath)
    .ReplaceNew<ExternalLib.ExternalDbContext>(fakeContext))
{
    // ...
}
```

`Type` を渡す overload もあります。

```csharp
.ReplaceNew(typeof(ExternalLib.ExternalDbContext), fakeContext)
```

assembly path と型の完全名を受け取る overload は、テストプロジェクトから対象型を直接参照しにくい場合にも使えるように用意しています。

## 1 セッションで複数の `ReplaceNew(...)`

1 つの処理が複数の依存を直接 `new` しているケースにも対応しています。

```csharp
namespace TargetApp;

public class UserService
{
    public string Run(int id)
    {
        using var db =
            new ExternalLib.ExternalDbContext();

        var logger =
            new ExternalLib.ExternalLogger();

        return db.FindName(id) + "|" + logger.Tag();
    }
}
```

2 種類の fake を同じセッションに登録します。

```csharp
using (var shims = Shims
    .ForAssembly(targetAssemblyPath)
    .ReplaceNew(
        externalAssemblyPath,
        "ExternalLib.ExternalDbContext",
        fakeDb)
    .ReplaceNew(
        externalAssemblyPath,
        "ExternalLib.ExternalLogger",
        fakeLogger))
{
    var service =
        shims.CreateObject("TargetApp.UserService");

    var result =
        shims.Invoke<string>(service, "Run", 1);

    Assert.AreEqual("fake-1|fake-log", result);
}
```

同じ型を複数回 `ReplaceNew` した場合は、最後に登録した rule が優先されます。逆に、登録していない型や引数条件に一致しない rule は実コンストラクターへフォールバックします。

なお、Easy API の `ReplaceNew` は catch-all です。コンストラクター引数に応じて fake を切り替えたい場合は、低レベル API の `New<T>().WithArguments(...).Returns(...)` と `Any` / `Eq` / `Is`、`ShimCaptor` を使います。

## rewritten assembly と型 identity mismatch

IL を書き換えた `TargetApp.dll` は、元の `TargetApp.dll` とは別にロードされます。

.NET では、型の同一性は namespace と型名だけでは決まりません。どの assembly instance、どの load context から来たかも型 identity に含まれます。

そのため、見た目が同じ `TargetApp.UserService` でも、次の 2 つは別の型です。

```text
テスト側が参照している TargetApp.UserService
    !=
書き換え済み TargetApp.dll 内の TargetApp.UserService
```

`Create<TargetApp.UserService>()` で元の型へ返そうとすると、安全には cast できません。そのため facade は無理に cast せず、一般的な rewritten シナリオでは `CreateObject(...)` と `Invoke(...)` を使う設計にしています。

この問題は戻り値の object graph にも伝播します。

## `ObservableCollection<T>` など complex object graph の検証課題

たとえば ViewModel が、書き換え対象アセンブリ内の `UserItem` をコレクションへ格納するとします。

```csharp
namespace TargetApp;

public class UserItem
{
    public UserItem(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public class UserViewModel
{
    public System.Collections.ObjectModel.ObservableCollection<UserItem> Items { get; }
        = new System.Collections.ObjectModel.ObservableCollection<UserItem>();

    public UserItem? SelectedUser { get; private set; }

    public void Load()
    {
        using var db =
            new ExternalLib.ExternalDbContext();

        var item = new UserItem(db.FindName(1));
        Items.Add(item);
        SelectedUser = item;
    }
}
```

書き換え後の `Items` は、概念的には次の型になります。

```text
ObservableCollection<rewritten TargetApp.UserItem>
```

`ObservableCollection<>` 自体は BCL の型でも、型引数 `UserItem` は書き換え済みアセンブリ側の型です。そのため、テスト側の `ObservableCollection<TargetApp.UserItem>` や `TargetApp.UserItem` には cast できません。

単純な戻り値なら `Invoke<string>` で済みますが、ViewModel、DTO、collection、ネストしたオブジェクトを検証したい場合は reflection の記述が増えてしまいます。

そこで、rewritten object を `object` のまま観察する inspection API を追加しました。

## `GetValue<T>` / `GetCollection` / `ShimsObject`

同じセッションで ViewModel を生成し、path で値を読み取れます。

```csharp
using (var shims = Shims
    .ForAssembly(targetAssemblyPath)
    .ReplaceNew(
        externalAssemblyPath,
        "ExternalLib.ExternalDbContext",
        fakeContext))
{
    var viewModel =
        shims.CreateObject("TargetApp.UserViewModel");

    shims.Invoke(viewModel, "Load");

    var count =
        shims.GetValue<int>(viewModel, "Items.Count");

    var firstName =
        shims.GetValue<string>(
            viewModel,
            "Items[0].Name");

    Assert.AreEqual(1, count);
    Assert.AreEqual("fake-1", firstName);
}
```

collection は `ShimsCollection` で包んで検証できます。

```csharp
var items =
    shims.GetCollection(viewModel, "Items");

Assert.AreEqual(1, items.Count);
Assert.AreEqual(
    "fake-1",
    items[0].Get<string>("Name"));
```

`ShimsObject` を使うと、ネストした object graph を段階的にたどれます。

```csharp
var selectedName = shims
    .Inspect(viewModel)
    .GetObject("SelectedUser")
    .GetValue<string>("Name");

Assert.AreEqual("fake-1", selectedName);
```

現在の path は、次のようなプロパティ、フィールド、index を対象にしています。

```text
Items
Items.Count
SelectedUser.Name
Items[0]
Items[0].Name
Rows[1].Cells[2].Text
```

`GetValue<T>` は `string`、数値、`bool`、enum などの leaf value を取得する用途に向いています。`GetValue<object>` なら raw object を返します。

一方、rewritten 参照型を同名の original 型へ強制 cast はしません。変換できない場合は `ShimsInspectionException` を投げ、「別の load context の可能性があるので inspection API を使う」という診断を返します。

これは型 identity の問題を解消する API ではありません。問題を隠して危険な変換を行わず、テストで必要な状態だけを安全に観察するための API です。

## net8.0 / net48 対応

`MiniMockito.Shims.Experimental` は `net8.0` と `net48` をターゲットにしています。Easy API、cross-assembly new interception、inspection API は両方の MSTest プロジェクトで検証しています。

ロード方法はランタイムごとに異なります。

- `net8.0` では collectible な isolated `AssemblyLoadContext` に書き換え済みアセンブリをロードする
- `net48` には `AssemblyLoadContext` がないため、`Assembly.Load(byte[])` と `AppDomain.AssemblyResolve` を使う

`net48` ではアセンブリ単位の collectible unload はできません。API の形はほぼ同じですが、C# 7.3 のテストでは `using var` ではなく `using (...) { ... }` を使います。

```csharp
using (Shims shims = Shims
    .ForAssembly(targetAssemblyPath)
    .ReplaceNew(
        externalAssemblyPath,
        "ExternalLib.ExternalDbContext",
        fakeContext))
{
    object service =
        shims.CreateObject("TargetApp.UserService");

    string result =
        shims.Invoke<string>(
            service,
            "GetDisplayName",
            1);

    Assert.AreEqual("fake-1", result);
}
```

どちらのランタイムでも、元 DLL を in-place で書き換えない点と、テストの並列実行を避ける点は同じです。

## できること / できないこと

現時点の主な範囲を整理します。

### できること

- allowlist に登録した user-defined な public / non-generic class の direct `new` interception
- parameterless constructor と、対応範囲内の constructor arguments
- 別アセンブリ型に対する cross-assembly new interception
- `Shims.ForAssembly(...).ReplaceNew(...)` による Easy API
- 1 セッション内の複数 `ReplaceNew`
- rule がない場合の実コンストラクターへのフォールバック
- last stub wins
- 低レベル API での constructor argument matcher と `ShimCaptor`
- `GetValue<T>` / `GetCollection` / `ShimsObject` による rewritten object graph の検証
- `net8.0` / `net48` の MSTest
- 関連する実験として、user-defined static method shim や限定的な instance method call shim

### できないこと、または意図的に行わないこと

- Microsoft Fakes Shim の完全互換
- `DateTime.Now`、`File.ReadAllText` など BCL static method の差し替え
- production assembly の in-place rewrite
- 起動後の runtime IL patch、CLR Profiling API、detour による process-wide interception
- reflection、expression tree、動的コード生成など、書き換え対象 DLL の直接的な `newobj` 以外の生成経路
- generic target class、by-ref / `params` など未対応パターンを含む constructor の網羅的な差し替え
- private method interception
- rewritten 型と original 型の identity を自動的に同一化すること
- parallel test safety の保証
- ReadyToRun / AOT / NativeAOT の保証

また、外部型の fake を自動生成して任意の振る舞いを付ける機能は限定的です。`ExternalDbContext` のような型では、手書き subclass や、virtual メソッドに対応した class mock を自分で用意して `ReplaceNew` に渡すのが基本です。

## 今後の課題

この仕組みは動作確認の段階を越えつつありますが、安定した汎用ツールと呼ぶには課題が残っています。

- process-wide な shim state を減らし、テスト並列性を改善する
- rewritten assembly と PDB / coverage の対応を改善する
- Visual Studio Test Explorer での実行・デバッグ体験を安定させる
- generic、`ref` / `out` / `params`、async など未対応 call site の範囲を整理する
- `ObservableCollection<T>` より複雑な dictionary、遅延列挙、循環参照を持つ object graph の inspection を拡充する
- string path に加え、型 identity を壊さない範囲で使える検証 API を検討する
- `net48` で unload できない制約を、診断とドキュメントでさらに明確にする
- API を安定化し、alpha としてどこまで互換性を保証するか決める

BCL 呼び出しの差し替えや profiler ベースの shim は、技術的にも安全性の面でも別の難しさがあります。必要になったとしても、現在の小さな test-time rewrite と同じ機能のように扱わず、別の実験として切り分けるつもりです。

## まとめ

`new SomeClass()` を差し替えるには、proxy オブジェクトを作るだけでは足りません。呼び出し側に埋め込まれた `newobj` を、テスト時だけ別の入口へ向ける必要があります。

`MiniMockito.Shims.Experimental` では、次の方針でこの問題を検証しています。

- 元のアセンブリは上書きせず、コピーした call site だけを IL リライトする
- cross-assembly では呼び出し側だけを書き換え、外部アセンブリは共有する
- Easy API の `Shims.ForAssembly(...).ReplaceNew(...)` で利用手順をまとめる
- 型 identity mismatch は無理に隠さず、inspection API で object graph を検証する
- `net8.0` と `net48` の両方でテストする

依存性注入を置き換えるものではありませんし、Microsoft Fakes の完全代替でもありません。それでも、すぐには設計を変えられない direct `new` を含むコードに対して、「どこまで安全にテストの逃げ道を作れるか」を考える材料にはなると思っています。
