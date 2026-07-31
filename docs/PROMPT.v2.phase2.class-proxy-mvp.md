# PROMPT.v2.phase2.class-proxy-mvp.md

MiniMockito.Net の v2 Phase 2 を実装してください。

AGENTS.md と `docs/v2-class-proxy-design.md` を読んでください。

## この Phase の目的

class proxy の最小実装を追加します。interface proxy ではなく、class の public virtual method を mock できるようにしてください。

## 対象

この Phase で対応するもの:

- public class
- non-sealed class
- parameterless constructor を持つ class
- public virtual method
- non-generic method 優先
- 通常引数
- 戻り値あり / void
- Lenient default behavior
- Strict behavior
- 既存 When / ThenReturn / ThenThrow / ThenAnswer の再利用
- 既存 Verify / Times の再利用

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
- class spy / partial mock
- CallBase

## 目標 public API

```csharp
var mock = Mock.Class<MyService>();

When(() => mock.GetName(1))
    .ThenReturn("mocked");

var result = mock.GetName(1);

Verify(() => mock.GetName(1), Times.Once());
```

Strict mode:

```csharp
var mock = Mock.Class<MyService>(MockBehavior.Strict);
```

## 設計方針

- 既存の interface mock API を壊さないでください。
- 既存の v1 テストを壊さないでください。
- 既存の MockState / InvocationRecord / StubRule / Verification をできるだけ再利用してください。
- class proxy 固有コードは `Proxy/ClassProxy` 配下に分けてください。
- Reflection.Emit を使う場合は責務を局所化してください。
- 生成した proxy type は cache してください。
- unsupported target は分かりやすい例外にしてください。

## 追加候補クラス

必要に応じて以下を追加してください。

- ClassProxyFactory
- ClassProxyBuilder
- ClassProxyTypeCache
- ClassProxyMethodEmitter
- ClassProxyValidation
- ClassMockOptions
- ClassProxyException
- ClassProxyUnsupportedReason

## unsupported diagnostics

以下の場合は分かりやすい例外を出してください。

- T が class ではない
- T が sealed class
- T が abstract class で生成できない
- T に parameterless constructor がない
- 対象 method が static
- 対象 method が non-virtual
- 対象 method が private
- ref / out parameter がある

例外メッセージには可能な限り以下を含めてください。

- Target class:
- Method:
- Reason:
- Supported methods:
- Unsupported methods:
- Hint:

## MSTest

以下のテストを追加してください。

- public non-sealed class の mock を作成できる
- public virtual method を ThenReturn で stub できる
- public virtual method を ThenThrow で stub できる
- public virtual method を ThenAnswer で stub できる
- public virtual method を Verify できる
- Times.Once / Times.Exactly が class proxy でも動作する
- unstubbed virtual method が Lenient で default を返す
- Strict では unstubbed virtual method が例外になる
- sealed class を指定すると unsupported 例外になる
- parameterless constructor がない class では unsupported 例外になる
- non-virtual method は差し替え対象外であることが分かる
- 既存 interface mock テストが壊れていない

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
- 実装した class proxy の範囲
- 追加または更新したテスト
- `dotnet build` の結果
- `dotnet test` の結果
- 既知の制約
- 次に推奨する Phase
