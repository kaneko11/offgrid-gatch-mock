# PROMPT.phase4.md

MiniMockito.Net の Phase 4 を実装してください。

AGENTS.md の方針を守ってください。

## Phase 4 の対象

interface spy、InOrder verification、README 完成、サンプル利用例を追加する。

### 1. Spy

以下を実装する。

```csharp
var spy = Spy.Of<IMyService>(realInstance);
```

仕様:

- T は interface のみ対応する
- realInstance は T を実装している必要がある
- stub が一致しない場合は realInstance を呼び出す
- stub が一致した場合は stub behavior を使う
- すべての呼び出しを記録する
- spy に対しても When / ThenReturn / ThenThrow / ThenAnswer を使えるようにする

### 2. InOrder verification

以下に近い API を実装する。

```csharp
var order = InOrder(mock1, mock2);
order.Verify(() => mock1.Start());
order.Verify(() => mock2.Save());
order.Verify(() => mock1.End());
```

仕様:

- 複数 mock 間の呼び出し順序を検証する
- invocation sequence number を使う
- 失敗時は以下を含める
  - Expected order
  - Actual order

### 3. README

README.md を更新し、以下を含める。

- 目的
- できること
- できないこと
- installation / local build
- 使い方
  - mock
  - when / thenReturn
  - verify
  - matchers
  - captor
  - spy
- Strict / Lenient
- async behavior
- v1 limitations
- future extension ideas

### 4. MSTest

以下のテストを追加する。

- Spy が実装を呼び出す
- Spy の一部メソッドだけ ThenReturn で差し替えられる
- Spy の呼び出しが記録される
- InOrder が複数 mock 間で動作する
- InOrder の失敗メッセージが分かりやすい
- 可能な範囲で README examples が compile できる

### 5. 最終検証

最後に必ず以下を実行する。

```bash
dotnet build
dotnet test
```

失敗した場合は原因を修正する。

## 完了時の報告

最後に以下を報告する。

- 実装内容の要約
- 変更ファイル一覧
- テスト結果
- v1 の制約
- v2 で推奨する改善点
