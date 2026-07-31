# PROMPT.phase2.md

MiniMockito.Net の Phase 2 を実装してください。

AGENTS.md の方針を守ってください。

## Phase 2 の対象

stubbing と basic matcher を追加する。

### 1. Stubbing public API

以下を実装する。

```csharp
When(() => mock.Method(...)).ThenReturn(value);
When(() => mock.Method(...)).ThenThrow(exception);
When(() => mock.Method(...)).ThenAnswer(ctx => ...);
When(() => mock.Method(...)).ThenReturnSequence(value1, value2, value3);
```

### 2. 内部 stubbing model

以下を追加または完成させる。

- StubRule
- StubBehavior
- ReturnBehavior
- ThrowBehavior
- AnswerBehavior
- StubContext

### 3. Matcher API

以下を実装する。

```csharp
Any<T>()
Eq(value)
Is<T>(predicate)
Null<T>()
NotNull<T>()
InRange(...)
```

### 4. Matching rules

- matcher を使っていない引数は equality matching にする
- When expression 内で matcher placeholder が使われた場合は ArgumentMatcher に変換する
- 実装は読みやすさを優先する

### 5. async stubbing

以下の戻り値でも自然に使えるようにする。

- Task
- Task<T>
- ValueTask
- ValueTask<T>

ThenReturn は async method return に対しても違和感なく使える設計にする。

### 6. MSTest

以下のテストを追加する。

- ThenReturn が動作する
- ThenThrow が動作する
- ThenAnswer が動作する
- ThenReturnSequence が動作する
- Any<T> が動作する
- Eq が動作する
- Is が動作する
- Null / NotNull が動作する
- InRange が動作する
- Task / Task<T> の stubbing が動作する
- ValueTask / ValueTask<T> の stubbing が動作する

## まだ実装しないもの

この Phase では以下を実装しない。

- Verify
- Captor
- Spy
- InOrder
- class proxy
- runtime rewriting

## 検証

最後に必ず以下を実行する。

```bash
dotnet build
dotnet test
```

失敗した場合は原因を修正する。

## 完了時の報告

最後に以下を報告する。

- 変更ファイル一覧
- 実装した内容
- テスト結果
- 既知の制約
