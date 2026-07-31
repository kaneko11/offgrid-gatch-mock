# PROMPT.v2.phase5.packaging-docs.md

MiniMockito.Net の v2 Phase 5 を実施してください。

AGENTS.md を読んでください。

## この Phase の目的

v2 の class proxy 対応後に、ドキュメント、サンプル、CI、パッケージングを整えます。

新しい大機能は追加せず、v2 をリリース候補に近づける hardening / release prep を行ってください。

## 対象

### 1. README 更新

README.md に以下を追加・整理してください。

- v1 interface mock の説明
- v1 interface spy の説明
- v2 class proxy の説明
- v2 class spy / partial mock の説明
- v2 でできること
- v2 でできないこと
- direct new / static / sealed / non-virtual が非対応であること
- MiniMockito.Shims.Experimental の将来構想
- Visual Studio 2022 + MSTest での使い方
- async の扱い
- Strict / Lenient の扱い
- エラーメッセージ例
- 既知の制約

### 2. samples 追加

可能なら以下のサンプルを追加してください。

```text
samples/
  MiniMockito.Sample.MSTest/
```

サンプルには以下を含めてください。

- interface mock
- When / ThenReturn
- Verify
- matcher
- captor
- interface spy
- class proxy
- class spy / partial mock
- async method

### 3. NuGet metadata

`.csproj` に必要な NuGet metadata を追加してください。

候補:

- PackageId
- Title
- Description
- Authors
- RepositoryUrl
- PackageTags
- Version
- PackageReadmeFile
- GenerateDocumentationFile

実際の値が不明なものは安全な仮値にしてください。

### 4. XML documentation

public API に XML documentation comments を追加してください。

対象:

- Mock
- Spy
- When
- Verify
- Times
- matchers
- captor
- class proxy public API
- main exceptions

### 5. CI

GitHub Actions workflow を追加してください。

候補:

```text
.github/workflows/ci.yml
```

内容:

- windows-latest
- dotnet restore
- dotnet build
- dotnet test

### 6. regression test

以下を確認するテストを追加または整理してください。

- README に載せている主要 API が実装と一致している
- interface mock と class proxy が同じテストプロジェクトで共存できる
- v1 の既存 API が壊れていない

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
- README 更新内容
- sample 追加内容
- NuGet metadata 追加内容
- CI 追加内容
- `dotnet build` の結果
- `dotnet test` の結果
- v2 の既知の制約
- 次に推奨する Phase
