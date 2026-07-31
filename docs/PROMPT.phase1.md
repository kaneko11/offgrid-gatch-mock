# PROMPT.phase1.md

MiniMockito.Net の Phase 1 を実装してください。

作業前に、まず以下を簡潔に整理してください。

- 予定するクラス構成
- Phase 1 で公開する public API
- v1 全体で割り切る制約

その後、以下の範囲だけを実装してください。

## Phase 1 の対象

軽量な MSTest 向けモックフレームワークの土台を作る。

### 1. ソリューションとプロジェクト構成

以下を作成する。

- src/MiniMockito/MiniMockito.csproj
- tests/MiniMockito.Tests/MiniMockito.Tests.csproj
- MiniMockito.sln
- README.md

### 2. 例外クラス

以下を実装する。

- MockException
- VerificationException
- StubbingException
- UnsupportedMockTargetException

### 3. Core model

以下を実装する。

- MockBehavior または MockMode
  - Lenient
  - Strict
- InvocationRecord
- MockState
- MockRepository
- InvocationMatcher
- ArgumentMatcher base type

### 4. interface mock 作成

以下の public API を実装する。

```csharp
var mock = Mock.Of<IMyService>();
```

仕様:

- T は interface のみ対応する
- T が interface でない場合は UnsupportedMockTargetException を投げる
- DispatchProxy を使う
- Lenient モードでは、未 stub の呼び出しは default 値を返す
- すべての呼び出しを InvocationRecord として記録する

### 5. default value handling

未 stub 呼び出しの戻り値として、以下を扱う。

- void
- reference type
- value type
- Task
- Task<T>
- ValueTask
- ValueTask<T>

### 6. MSTest の初期テスト

以下のテストを追加する。

- interface の mock が作成できる
- 非 interface を指定すると UnsupportedMockTargetException になる
- メソッド呼び出しが内部的に記録される
- Lenient モードで未 stub の reference return が null を返す
- Lenient モードで未 stub の value return が default value を返す
- Lenient モードで未 stub の Task return が completed Task を返す
- Lenient モードで未 stub の Task<T> return が default value の completed Task<T> を返す
- Lenient モードで未 stub の ValueTask / ValueTask<T> が適切に返る

## まだ実装しないもの

この Phase では以下を実装しない。

- When / ThenReturn
- ThenThrow
- ThenAnswer
- Verify
- Matchers の具体実装
- Captor
- Spy
- InOrder
- Source Generator
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
- 次に推奨する Phase
