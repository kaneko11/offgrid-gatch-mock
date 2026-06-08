# MiniMockito.Shims.Experimental

このファイルは `new SomeClass()` の差し替えを実験するための Codex CLI 向けプロンプトです。

重要:
- MiniMockito 本体には混ぜないでください。
- `MiniMockito.Shims.Experimental` として別 package / 別 namespace / 別 test project に分離してください。
- 既存の v1 / v2 の public API とテストを壊さないでください。
- 最初から Microsoft Fakes Shim の完全代替を目指さないでください。
- まず user assembly の限定的な `newobj` 差し替え PoC を目指してください。

## 目的

`MiniMockito.Shims.Experimental` の目的は、MiniMockito 本体では扱わない高リスク領域、特に `new SomeClass()` の差し替えを段階的に検証することです。

MiniMockito 本体は以下に集中します。

- interface mock
- interface spy
- class proxy
- virtual method mocking
- class spy / partial mock

`MiniMockito.Shims.Experimental` は以下を実験対象にします。

- direct `new` interception
- constructor interception
- static method mocking の将来調査
- sealed / non-virtual method mocking の将来調査
- build-time weaving
- source rewriting
- runtime IL rewrite feasibility
- CLR Profiling API feasibility

## 最初の実装対象

最初に狙う対象は direct `new` interception の限定 PoC です。

目標イメージ:

```csharp
using MiniMockito.Shims.Experimental;

using (ShimContext.Create())
{
    var fake = Mock.Class<UserRepository>();

    Shim.New<UserRepository>()
        .Returns(fake);

    var service = new UserService();

    var result = service.GetDisplayName(1);
}
```

対象コード例:

```csharp
public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();
        return repository.GetName(id);
    }
}
```

この `new UserRepository()` を test scope 内で fake に差し替えることを最終目標にします。

## 最初に対応する範囲

初期対応は以下に限定してください。

- test project / sample project 内の user-defined class
- public class
- non-generic class
- parameterless constructor
- direct `new SomeClass()` の単純な `newobj`
- allowlist で明示された target type
- dedicated sample assembly
- MSTest での検証
- parallel test disabled の専用 test run

## 最初は対応しない範囲

以下は初期実装で対応しません。

- BCL / .NET runtime type の差し替え
- `DateTime.Now`
- `File.ReadAllText`
- static method mocking
- sealed class method interception
- non-virtual method body interception
- private method interception
- constructor arguments
- generic constructors
- generic classes
- nested new
- reflection 経由の construction
- dependency injection container 内部の new
- expression tree 内の new
- async state machine 内の複雑な new
- iterator 内の new
- ReadyToRun / AOT / NativeAOT
- production assembly の in-place rewrite
- process-wide stable guarantee
- parallel test safety guarantee

## 推奨方式

最初は runtime IL rewrite や CLR Profiling API ではなく、build-time / test-time weaving の限定 PoC を優先してください。

優先順位:

1. build-time weaving / test output assembly rewrite の限定 PoC
2. source rewriting dry-run
3. runtime IL rewrite feasibility
4. CLR Profiling API feasibility
5. detour / method patching feasibility

最初の実装候補:

- Mono.Cecil などによる test output assembly のコピー書き換え
- `newobj Target::.ctor()` を `ShimDispatcher.New<T>()` に向ける
- 書き換え対象は allowlist で明示指定する
- 書き換え済み assembly は別出力先に生成する
- original assembly は上書きしない
- MSTest から isolated sample を実行する

## 安全ルール

- MiniMockito 本体に shim 実装を混ぜない
- `MiniMockito` から `MiniMockito.Shims.Experimental` を参照しない
- experimental package は本体 release の安定性に影響させない
- process-wide patch を行う場合は危険性を明示する
- parallel test は既定で無効化する方針を検討する
- `ShimContext.Dispose()` で確実に cleanup する
- cleanup failure を握りつぶさない
- 既存 v1 / v2 のテストを壊さない

## API 方針

候補 API:

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .Returns(fakeRepository);
}
```

対象 assembly / method を明示する API も検討してください。

```csharp
using (ShimContext.Create())
{
    Shim.New<UserRepository>()
        .ForAssembly(typeof(UserService).Assembly)
        .Returns(fakeRepository);
}
```

初期 PoC では API を確定させすぎないでください。

## 診断メッセージ方針

失敗時のメッセージには可能な限り以下を含めてください。

- Target type:
- Constructor:
- Calling assembly:
- Calling method:
- Rewrite mode:
- Reason:
- Supported patterns:
- Unsupported patterns:
- Hint:

## 各 Phase 完了時の報告

各 Phase の最後に以下を報告してください。

- 変更ファイル
- 実装した内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に実施すべき Phase
