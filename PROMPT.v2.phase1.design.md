# PROMPT.v2.phase1.design.md

MiniMockito.Net の v2 Phase 1 を実施してください。

AGENTS.md を読んでください。

## この Phase の目的

今回は class proxy 対応の設計調査だけを行ってください。まだ大きな実装はしないでください。

v1 の interface proxy ベースの MiniMockito に対して、v2 で class proxy による virtual method mocking を追加するための設計を整理します。

## 調査対象

以下を調査・設計してください。

1. class proxy による virtual method mocking
2. class spy / partial mock
3. constructor injection / factory injection を支援する API
4. direct new interception
5. static method mocking
6. sealed / non-virtual method mocking

## 必ず分類すること

以下の分類を明確にしてください。

- v2 本体に入れるべきもの
- 別パッケージに分けるべきもの
- experimental 扱いにすべきもの
- 実装しないほうがよいもの

## 制約

- 既存の interface mock API を壊さないでください。
- v1 のテストを壊さないでください。
- この Phase では runtime IL rewrite や profiler API を実装しないでください。
- この Phase では direct new interception を実装しないでください。
- この Phase では static method mocking を実装しないでください。
- この Phase では sealed / non-virtual method mocking を実装しないでください。
- Moq / NSubstitute / FakeItEasy / JustMock / Rhino Mocks / Microsoft Fakes は使わないでください。
- まず設計案、リスク、実装順序、テスト方針だけを出してください。

## 出力してほしい内容

以下を Markdown ドキュメントとして追加してください。

- `docs/v2-class-proxy-design.md`

内容には最低限以下を含めてください。

### 1. v2 の目的

- v1 から何を拡張するのか
- v2 でやらないこと
- Microsoft Fakes Shim 相当との差分

### 2. public API 案

候補:

```csharp
var mock = Mock.Class<MyService>();

When(() => mock.VirtualMethod(1))
    .ThenReturn("mocked");

Verify(() => mock.VirtualMethod(1), Times.Once());
```

class spy / partial mock 候補:

```csharp
var spy = Spy.Class<MyService>();

When(() => spy.VirtualMethod(1))
    .ThenReturn("mocked");
```

CallBase 候補:

```csharp
var mock = Mock.Class<MyService>(ClassMockOptions.CallBase);
```

### 3. 内部クラス構成案

候補:

- ClassProxyFactory
- ClassProxyBuilder
- ClassProxyTypeCache
- ClassProxyMethodEmitter
- ClassMockOptions
- ClassProxyValidation
- ClassProxyUnsupportedReason
- ClassProxyException

### 4. 既存実装の再利用方針

以下をどう再利用するか整理してください。

- MockState
- MockRepository
- InvocationRecord
- StubRule
- StubBehavior
- ArgumentMatcher
- Verifier
- OrderVerifier
- DefaultValueProvider

### 5. Reflection.Emit で実装する場合の方針

以下を整理してください。

- proxy class をどのように生成するか
- virtual method override をどのように生成するか
- MethodInfo / arguments をどう渡すか
- return value / exception をどう扱うか
- generics をどこまで対応するか
- async return をどう扱うか
- protected virtual method をどう扱うか
- parameterless constructor を前提にするか

### 6. Castle DynamicProxy を使わない場合の難所

以下を具体的に整理してください。

- IL emit の複雑さ
- constructors
- generic methods
- ref / out parameters
- protected virtual methods
- value type return
- async return
- debugging
- performance
- type caching

### 7. v2 Phase 2 の最小スコープ

実装開始時の最小スコープを決めてください。

例:

- public non-sealed class
- parameterless constructor 必須
- public virtual method のみ
- non-generic method 優先
- ref / out は非対応
- sealed / static / non-virtual は非対応
- class proxy 固有エラーを出す

### 8. テスト方針

以下のテスト方針を整理してください。

- class proxy 作成
- public virtual method stubbing
- public virtual method verification
- lenient default
- strict behavior
- unsupported target diagnostics
- existing interface mock regression

### 9. リスク

以下を整理してください。

- 実装難易度
- 既存 API への影響
- テスト容易性
- Visual Studio 2022 / MSTest との相性
- CI での扱いやすさ
- 将来の shim experimental との境界

### 10. v2 Phase 2 用の実装プロンプト案

次の Phase で Codex CLI に渡せる実装プロンプトを Markdown 内に含めてください。

## 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 作成した設計ドキュメントの要約
- v2 本体に入れるべき範囲
- experimental に分けるべき範囲
- 次に推奨する Phase
