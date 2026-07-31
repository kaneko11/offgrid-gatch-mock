# PROMPT.v2.phase4.experimental-shim-design.md

MiniMockito.Net の v2 Phase 4 を実施してください。

AGENTS.md を読んでください。

## この Phase の目的

direct new interception / static method mocking / sealed / non-virtual method mocking について、experimental package として分離する前提で設計調査を行います。

この Phase では本体への実装はしないでください。runtime IL rewrite や profiler API の実装もまだ行わないでください。

## 背景

v1 / v2 本体は以下を中心にします。

- interface mock
- interface spy
- class proxy
- virtual method mocking
- class spy / partial mock

一方、以下は Microsoft Fakes Shim に近い高リスク領域です。

- `new SomeClass()` の差し替え
- static method の差し替え
- sealed class の差し替え
- non-virtual method の差し替え
- constructor interception
- .NET Framework / BCL 呼び出しの透過的差し替え

これらは MiniMockito 本体ではなく、`MiniMockito.Shims.Experimental` のような別パッケージで検討します。

## 調査対象

以下を比較してください。

1. runtime IL rewrite
2. CLR Profiling API
3. source rewriting
4. build-time weaving
5. detour / method patching
6. Roslyn source generator / analyzer による seam 提案
7. adapter / factory migration helper

## 出力してほしい内容

以下を Markdown ドキュメントとして追加してください。

- `docs/v2-shims-experimental-design.md`

内容には最低限以下を含めてください。

### 1. 目的

- なぜ direct new / static / sealed / non-virtual が難しいのか
- interface proxy / class proxy との違い
- Microsoft Fakes Shim 相当との違い

### 2. 方式比較

各方式について整理してください。

- 概要
- できること
- できないこと
- Visual Studio 2022 + MSTest との相性
- CI での扱いやすさ
- 並列テスト時のリスク
- デバッグ容易性
- 実装難易度
- 保守性
- セキュリティ / 実行環境制約

### 3. experimental package 案

候補:

```text
src/
  MiniMockito.Shims.Experimental/
```

候補 API:

```csharp
using (ShimContext.Create())
{
    Shim.Static(() => DateTime.Now).Returns(fixedTime);
    Shim.New<UserRepository>().Returns(fakeRepo);
}
```

この API はまだ確定しないでください。設計候補として扱ってください。

### 4. 本体との境界

以下を整理してください。

- MiniMockito 本体に残すもの
- MiniMockito.ClassProxy に置くもの
- MiniMockito.Shims.Experimental に分けるもの
- 共有できる Core model
- 共有してはいけない低レベル実装

### 5. 最初に実験すべき PoC

最小 PoC を提案してください。

候補:

- source rewriting による new replacement
- build-time weaving による method call replacement
- analyzer による DI / factory migration suggestion
- CLR Profiling API の feasibility check

### 6. やらない判断

以下について、なぜ本体に入れないか説明してください。

- direct new interception
- static method mocking
- sealed class mocking
- non-virtual method mocking
- runtime IL rewrite
- profiler API

### 7. 推奨ロードマップ

以下を整理してください。

- v2 本体でやること
- v2 experimental でやること
- v3 以降に回すこと
- 実装しないほうがよいこと

## 制約

- この Phase では production code を大きく変更しないでください。
- runtime IL rewrite を実装しないでください。
- profiler API を実装しないでください。
- direct new interception を実装しないでください。
- static method mocking を実装しないでください。
- sealed / non-virtual method mocking を実装しないでください。
- 既存 v1 / v2 テストを壊さないでください。

## 検証

可能なら以下を実行してください。

```bash
dotnet build
dotnet test
```

## 完了時の報告

最後に以下を日本語で報告してください。

- 変更ファイル一覧
- 作成した experimental design の要約
- 本体に入れないと判断した範囲
- experimental package として検討する範囲
- 次に推奨する Phase
