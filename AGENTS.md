# AGENTS.md

## プロジェクトの目的

Visual Studio 2022 + MSTest で自然に使える、軽量な .NET 向けモックフレームワークを実装する。

このライブラリは Microsoft Fakes の完全代替ではなく、v1 では Stub / Mock 系の用途に集中する。  
Java の Mockito に近い利用感を目指す。

ライブラリ名は以下を基本とする。

- MiniMockito.Net

## v1 の対象範囲

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

- static メソッド差し替え
- sealed class の mock
- non-virtual method の interception
- private method interception
- constructor interception
- runtime IL rewrite
- profiler API ベースの shim
- .NET Framework / BCL 呼び出しの透過的差し替え
- Moq / NSubstitute / FakeItEasy / JustMock / Rhino Mocks / Microsoft Fakes の利用

## アーキテクチャ方針

以下の責務分離を守る。

### Proxy

- DispatchProxy で interface proxy を生成する
- すべてのメソッド呼び出しを Invoke に集約する

### Invocation

- すべての呼び出しを InvocationRecord として記録する
- InvocationRecord には以下を含める
  - MethodInfo
  - arguments
  - timestamp
  - sequence number
  - return value
  - exception
  - thread ID
  - mock ID

### Stubbing

- Invocation に合致する StubRule を解決する
- StubRule は以下を扱えるようにする
  - return
  - throw
  - answer
  - call real method
  - sequence

### Verification

- invocation log を読んで検証する
- 以下を検証できるようにする
  - 呼び出し回数
  - 引数一致
  - 呼び出し順序
  - 未検証呼び出し
  - no interactions
  - no more interactions

### Matching

- 引数比較は ArgumentMatcher で抽象化する
- 以下を扱う
  - Any
  - Eq
  - Is
  - Null
  - NotNull
  - InRange

### Captor

- verify 時に実際の引数を取得できるようにする

### Spy

- 実インスタンスを保持する
- stub が合致しない場合は実インスタンスへ委譲する
- spy でも invocation log を残す

## 目標 public API

利用者向け API はシンプルにする。

```csharp
var service = Mock.Of<IMyService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

var result = service.GetName(123);

Verify(() => service.GetName(123), Times.Once());
```

以下も対応する。

```csharp
When(() => service.GetName(Any<int>()))
    .ThenThrow(new InvalidOperationException());

When(() => service.GetName(Any<int>()))
    .ThenAnswer(ctx => "id=" + ctx.Arguments[0]);

VerifyNoInteractions(service);
VerifyNoMoreInteractions(service);

var captor = Capture<string>();
Verify(() => service.Save(captor.Value));
Assert.AreEqual("abc", captor.CapturedValue);

var spy = Spy.Of<IMyService>(realService);
```

## 推奨プロジェクト構成

特別な理由がなければ、以下の構成にする。

```text
src/
  MiniMockito/
    Core/
    Proxy/
    Matching/
    Stubbing/
    Verification/
    Spy/
    Exceptions/
    Utilities/

tests/
  MiniMockito.Tests/
```

## 実装ルール

- C# latest を使う
- nullable reference types を有効にする
- public API の命名を一貫させる
- 過剰設計しない
- v1 に class proxy を入れない
- runtime rewrite や profiler ベースの shim を入れない
- 外部 mocking framework を使わない
- reflection を使いすぎて可読性を落とさない
- 将来の class proxy / shim 実験に備え、境界はきれいに保つ
- テストは deterministic にする
- テストフレームワークは MSTest を使う
- 各作業の最後に `dotnet build` と `dotnet test` を実行する

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

## 各タスク完了時の報告

各 Phase の最後に以下を報告する。

- 変更ファイル
- 実装した内容
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に実施すべき Phase
