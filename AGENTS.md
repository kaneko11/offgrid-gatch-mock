# AGENTS.md

## プロジェクトの目的

Visual Studio 2022 + MSTest で自然に使える、軽量な .NET 向けモックフレームワークを実装する。

このライブラリは Microsoft Fakes の完全代替ではない。v1 では Stub / Mock 系、つまり interface mock / spy / stubbing / verification に集中する。v2 では v1 の public API と内部構造を壊さず、class proxy による virtual method mocking へ拡張する。

ライブラリ名は以下を基本とする。

- MiniMockito.Net

## バージョン方針

### v1

v1 は interface proxy ベースの安定版とする。

v1 で対応するもの:

- interface の mock
- interface の spy
- DispatchProxy ベースの interface proxy
- 呼び出し記録
- When / ThenReturn / ThenThrow / ThenAnswer による stubbing
- 連続返却
- Times / Never / AtLeast / AtMost による verify
- VerifyNoInteractions
- VerifyNoMoreInteractions
- InOrder による順序検証
- argument matcher
- argument captor
- Task / Task<T> / ValueTask / ValueTask<T> の async 戻り値
- Strict / Lenient モード
- MSTest によるテスト

v1 で対応しないもの:

- class proxy
- static メソッド差し替え
- sealed class の mock
- non-virtual method の interception
- private method interception
- constructor interception
- runtime IL rewrite
- profiler API ベースの shim
- .NET Framework / BCL 呼び出しの透過的差し替え
- Moq / NSubstitute / FakeItEasy / JustMock / Rhino Mocks / Microsoft Fakes の利用

### v2

v2 は v1 の上に class proxy を追加する拡張版とする。

v2 本体で対応するもの:

- class proxy の設計
- public non-sealed class の mock
- parameterless constructor を持つ class の最小対応
- public virtual method の stubbing
- public virtual method の verification
- class spy / partial mock
- CallBase 相当の挙動の検討
- interface mock と class mock の共存
- class proxy 固有のエラー診断
- README / sample / tests の拡充

v2 本体で対応しないもの:

- direct new interception
- static method mocking
- sealed class mocking
- non-virtual method mocking
- private method interception
- constructor interception
- runtime IL rewrite
- profiler API based shim
- Microsoft Fakes Shim 相当機能

これらは必要になった場合でも、MiniMockito.Shims.Experimental のような別パッケージで設計・検証する。

## アーキテクチャ方針

### Proxy

v1:

- DispatchProxy で interface proxy を生成する
- すべての interface method 呼び出しを Invoke に集約する

v2:

- class proxy は interface proxy と分離する
- class proxy 固有コードは Proxy/ClassProxy 配下に分ける
- 既存の interface proxy 実装を壊さない
- Reflection.Emit を使う場合は責務を ClassProxy 配下へ閉じ込める
- Castle DynamicProxy などの外部 mocking / proxy framework は使わない方針を優先する

### Invocation

- すべての呼び出しを InvocationRecord として記録する
- InvocationRecord には MethodInfo, arguments, timestamp, sequence number, return value, exception, thread ID, mock ID を含める
- v2 class proxy でも可能な限り同じ InvocationRecord を使う

### Stubbing

- Invocation に合致する StubRule を解決する
- StubRule は return / throw / answer / call real method / sequence を扱う
- v2 class proxy でも可能な限り同じ StubRule / StubBehavior を使う

### Verification

- invocation log を読んで検証する
- 呼び出し回数、引数一致、呼び出し順序、未検証呼び出し、no interactions、no more interactions を検証する
- v2 class proxy でも既存 Verifier を再利用する

### Matching / Captor / Spy

- 引数比較は ArgumentMatcher で抽象化する
- verify 時に Captor で実引数を取得できるようにする
- v1 は interface spy、v2 は class spy / partial mock を追加検討する
- class spy では stub がない virtual method は base implementation を呼ぶ設計を検討する
- non-virtual method は差し替え対象外とする

## 目標 public API

### v1 interface mock

```csharp
var service = Mock.Of<IMyService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

Verify(() => service.GetName(123), Times.Once());
```

### v2 class mock 候補

```csharp
var service = Mock.Class<MyService>();

When(() => service.GetName(1))
    .ThenReturn("mocked");

Verify(() => service.GetName(1), Times.Once());
```

### v2 class spy / partial mock 候補

```csharp
var service = Spy.Class<MyService>();

When(() => service.GetName(1))
    .ThenReturn("mocked");
```

CallBase 候補:

```csharp
var service = Mock.Class<MyService>(ClassMockOptions.CallBase);
```

最終 API は v2 Phase 1 の設計調査で決める。既存の v1 API を壊してはいけない。

## 推奨プロジェクト構成

```text
src/
  MiniMockito/
    Core/
    Proxy/
      InterfaceProxy/
      ClassProxy/
    Matching/
    Stubbing/
    Verification/
    Spy/
    Exceptions/
    Utilities/

tests/
  MiniMockito.Tests/
    InterfaceMocking/
    Stubbing/
    Verification/
    Spy/
    ClassProxy/
```

既存構成がある場合は、無理に大規模移動しない。破壊的なフォルダ再編より安全な拡張を優先する。

## 実装ルール

- C# latest を使う
- nullable reference types を有効にする
- public API の命名を一貫させる
- 外部 mocking framework を使わない
- Moq / NSubstitute / FakeItEasy / JustMock / Rhino Mocks / Microsoft Fakes は使わない
- v1 の interface mock API を壊さない
- v1 の既存テストを壊さない
- reflection を使いすぎて可読性を落とさない
- Reflection.Emit を使う場合は責務を ClassProxy 配下へ閉じ込める
- runtime rewrite や profiler ベースの shim を本体に入れない
- direct new / static / sealed / non-virtual の差し替えは本体に入れない
- テストは deterministic にする
- テストフレームワークは MSTest を使う
- 各作業の最後に `dotnet build` と `dotnet test` を実行する

## public API 変更ルール

Phase 5 以降、既存 public API を破壊的変更してはいけない。

破壊的変更の例:

- 既存メソッド名を変える
- 既存の引数順序を変える
- 既存 API を削除する
- 既存の戻り値型を変える
- 既存テストで使っている API を別 API に置き換える

破壊的変更が必要だと判断した場合は、実装前に以下を説明する。

- なぜ必要か
- 影響を受ける API
- 影響を受けるテスト
- 代替案
- 後方互換を維持する方法

## エラーメッセージ方針

verify 失敗時のメッセージには、可能な限り以下を含める。

- Wanted:
- Actual invocations:
- Matching invocations:
- Method:
- Expected count:
- Actual count:
- Arguments:
- Closest recorded calls:

InOrder の失敗時には以下を含める。

- Expected order
- Actual order

v2 class proxy 固有の失敗では、可能な限り以下を含める。

- Target class:
- Method:
- Reason:
- Supported methods:
- Unsupported methods:
- Hint:

## 各タスク完了時の報告

各 Phase の最後に以下を報告する。

- 変更ファイル
- 実装した内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に実施すべき Phase
