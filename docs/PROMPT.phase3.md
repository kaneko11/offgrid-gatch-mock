# PROMPT.phase3.md

MiniMockito.Net の Phase 3 を実装してください。

AGENTS.md の方針を守ってください。

## Phase 3 の対象

verification、captor、strict mode、no-interaction checks を追加する。

### 1. Verify API

以下を実装する。

```csharp
Verify(() => mock.Method(...));
Verify(() => mock.Method(...), Times.Once());
Verify(() => mock.Method(...), Times.Exactly(n));
Verify(() => mock.Method(...), Times.Never());
Verify(() => mock.Method(...), Times.AtLeast(n));
Verify(() => mock.Method(...), Times.AtMost(n));
```

### 2. No interaction API

以下を実装する。

```csharp
VerifyNoInteractions(mock);
VerifyNoMoreInteractions(mock);
```

仕様:

- Verify に成功した invocation は verified として印を付ける
- VerifyNoMoreInteractions は未検証の invocation を検出する

### 3. Captor

以下を実装する。

```csharp
var captor = Capture<string>();
Verify(() => mock.Save(captor.Value));
Assert.AreEqual("abc", captor.CapturedValue);
```

可能なら複数件取得も対応する。

```csharp
captor.CapturedValues
```

### 4. Strict mode

以下のような strict mock 作成をサポートする。

```csharp
var mock = Mock.Of<IMyService>(MockBehavior.Strict);
```

Strict モードの未 stub 呼び出しでは、分かりやすい例外を投げる。

例外メッセージには以下を含める。

- mock name または mock ID
- method name
- arguments
- existing stub candidates

### 5. Verify 失敗メッセージ

失敗メッセージには可能な限り以下を含める。

- Wanted:
- Actual invocations:
- Matching invocations:
- Method:
- Expected count:
- Actual count:
- Arguments:
- Closest recorded calls:

### 6. MSTest

以下のテストを追加する。

- Verify one call
- Times.Exactly
- Never
- AtLeast
- AtMost
- VerifyNoInteractions
- VerifyNoMoreInteractions
- Captor が引数を取得できる
- 対応するなら Captor が複数引数を取得できる
- Strict mock で未 stub 呼び出しが例外になる
- Lenient mock で未 stub 呼び出しが default を返す
- Verify 失敗メッセージに必要なラベルが含まれる

## まだ実装しないもの

この Phase では以下を実装しない。

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
