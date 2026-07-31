# PROMPT.v2.phase3.class-spy-partial.md

MiniMockito.Net の v2 Phase 3 を実装してください。

AGENTS.md と `docs/v2-class-proxy-design.md` を読んでください。v2 Phase 2 の class proxy 実装が存在する前提です。

## この Phase の目的

class spy / partial mock を追加します。

stub が合致する virtual method は stub behavior を使い、stub がない virtual method は real implementation / base implementation を呼ぶ挙動を実現します。

## 対象

この Phase で対応するもの:

- class spy
- partial mock
- public non-sealed class
- parameterless constructor を持つ class
- public virtual method
- stub がある場合は stub behavior を使う
- stub がない場合は base implementation を呼ぶ
- invocation log は常に記録する
- Verify / Times が class spy でも動作する

## 非対象

この Phase では以下を実装しないでください。

- sealed class mocking
- static method mocking
- non-virtual method mocking
- private method interception
- constructor interception
- direct new interception
- runtime IL rewrite
- profiler API
- ref / out parameter support
- generic method support
- protected virtual method support
- external real instance wrapping が難しい場合の完全対応

## 目標 public API

```csharp
var spy = Spy.Class<MyService>();

// stub がない virtual method は base implementation を呼ぶ
var realResult = spy.GetName(1);

// 一部だけ差し替える
When(() => spy.GetName(2))
    .ThenReturn("mocked");

Verify(() => spy.GetName(1), Times.Once());
Verify(() => spy.GetName(2), Times.Once());
```

必要なら以下も検討してください。

```csharp
var mock = Mock.Class<MyService>(ClassMockOptions.CallBase);
```

## 設計方針

- 既存の interface spy API を壊さないでください。
- 既存の class proxy API を壊さないでください。
- 既存の MockState / StubRule / InvocationRecord / Verification を再利用してください。
- class spy 固有の処理は class proxy 層に閉じ込めてください。
- base method 呼び出しの実装が難しい場合は、最小スコープに縮小し、理由を報告してください。

## MSTest

以下のテストを追加してください。

- class spy を作成できる
- stub がない public virtual method は base implementation を呼ぶ
- stub した public virtual method は ThenReturn の値を返す
- ThenThrow が class spy でも動作する
- ThenAnswer が class spy でも動作する
- class spy の呼び出しが invocation log に記録される
- class spy に対して Verify が動作する
- class spy に対して VerifyNoMoreInteractions が動作する
- 既存 interface spy テストが壊れていない
- 既存 class proxy テストが壊れていない

## 検証

最後に必ず以下を実行してください。

```bash
dotnet build
dotnet test
```

失敗した場合は原因を修正してください。

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 実装した class spy / partial mock の範囲
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
