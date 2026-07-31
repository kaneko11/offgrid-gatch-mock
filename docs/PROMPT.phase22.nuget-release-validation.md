# MiniMockito Phase 22 — NuGet Package Update / Release Validation

AGENTS.md、README.md、src/MiniMockito/MiniMockito.csproj、src/MiniMockito.Shims.Experimental/MiniMockito.Shims.Experimental.csproj、docs/shims-experimental-quickstart.md、docs/shims-net48-compatibility-design.md、docs/shims-experimental-phase14-milestone.md を読んでください。

MiniMockito Phase 22 として、NuGet package update / release validation を実施してください。

## 目的

MiniMockito.Net と MiniMockito.Shims.Experimental の NuGet パッケージを更新できる状態にしてください。

この Phase では nuget.org への push は行わないでください。API key を扱わないでください。

## バージョン案

以下を候補にしてください。ただし、既存 csproj の現在バージョンを確認し、重複しないようにしてください。

```text
MiniMockito.Net:
  0.2.0-preview.7

MiniMockito.Shims.Experimental:
  0.1.0-alpha.5
```

Phase 18 の net48 x86 fallback や Phase 20 / 21 の cross-assembly new interception を含めてリリースする場合、README / docs に変更点を明記してください。

## 作業対象

- `src/MiniMockito/MiniMockito.csproj`
- `src/MiniMockito.Shims.Experimental/MiniMockito.Shims.Experimental.csproj`
- `README.md`
- `docs/shims-experimental-quickstart.md`
- `docs/shims-net48-compatibility-design.md`
- `docs/shims-experimental-phase14-milestone.md`
- 必要なら changelog / release notes document

## 作業内容

1. csproj の package metadata を確認する
   - PackageId
   - Version
   - PackageVersion
   - Authors
   - Description
   - PackageTags
   - RepositoryUrl
   - PackageLicenseExpression
   - GenerateDocumentationFile

2. Version / PackageVersion を更新する

3. README の PackageReference 例を更新する

4. docs の install 手順を更新する

5. Shims.Experimental の warning を明確にする
   - experimental
   - API may change
   - test-only
   - `[DoNotParallelize]` 必須
   - BCL static method 未対応
   - production assembly in-place rewrite はしない

6. Release build を確認する

7. Release test を確認する

8. pack を実行する

9. artifacts の nupkg を確認する

10. nupkg の中身を確認する
    - MiniMockito.Net に net8.0 / net48 が含まれること
    - MiniMockito.Shims.Experimental に net8.0 / net48 が含まれること
    - XML docs が含まれること
    - 余計な test assembly が含まれていないこと

## 実行コマンド

```powershell
dotnet clean
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

可能なら個別にも実行してください。

```powershell
dotnet test tests/MiniMockito.Tests/MiniMockito.Tests.csproj -c Release
dotnet test tests/MiniMockito.Shims.Experimental.Tests/MiniMockito.Shims.Experimental.Tests.csproj -c Release
dotnet test tests/MiniMockito.Shims.Experimental.Net48Tests/MiniMockito.Shims.Experimental.Net48Tests.csproj -c Release
```

pack:

```powershell
Remove-Item -Recurse -Force .\artifacts -ErrorAction SilentlyContinue
dotnet pack src/MiniMockito/MiniMockito.csproj -c Release -o artifacts
dotnet pack src/MiniMockito.Shims.Experimental/MiniMockito.Shims.Experimental.csproj -c Release -o artifacts
```

確認:

```powershell
Get-ChildItem .\artifacts
```

必要なら nupkg を展開して中身を確認してください。

```powershell
New-Item -ItemType Directory -Force .\artifacts\inspect
Expand-Archive .\artifacts\MiniMockito.Net.*.nupkg .\artifacts\inspect\MiniMockito.Net -Force
Expand-Archive .\artifacts\MiniMockito.Shims.Experimental.*.nupkg .\artifacts\inspect\MiniMockito.Shims.Experimental -Force
Get-ChildItem .\artifacts\inspect -Recurse
```

## ローカル NuGet 検証

可能なら、別の一時プロジェクトで local artifacts を参照して検証してください。

```powershell
dotnet new mstest -n MiniMockito.PackageSmokeTests -f net8.0
dotnet add MiniMockito.PackageSmokeTests package MiniMockito.Net --version 0.2.0-preview.7 --source .\artifacts
dotnet test MiniMockito.PackageSmokeTests
```

net48 も可能なら検証してください。

```powershell
dotnet new mstest -n MiniMockito.PackageSmokeTests.Net48 -f net48
dotnet add MiniMockito.PackageSmokeTests.Net48 package MiniMockito.Net --version 0.2.0-preview.7 --source .\artifacts
```

## この Phase でしないこと

- nuget.org への push
- API key の保存
- 新機能追加
- public API の破壊的変更
- GitHub release 作成
- production assembly rewrite
- BCL static method mocking

## 報告

最後の報告は日本語でお願いします。

報告には以下を含めてください。

- 更新した package version
- 更新した csproj metadata
- README / docs の更新内容
- dotnet build -c Release の結果
- dotnet test -c Release の結果
- dotnet pack の結果
- 生成された nupkg ファイル名
- nupkg の target framework 内容
- push はしていないこと
