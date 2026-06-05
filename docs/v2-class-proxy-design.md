# v2 Class Proxy Design

## 1. v2 の目的

MiniMockito.Net v2 の目的は、v1 の interface mock / spy / stubbing / verification の public API と内部モデルを壊さずに、class proxy による public virtual method mocking を追加することです。

v1 から拡張するもの:

- `DispatchProxy` による interface proxy に加えて、class proxy を追加する
- class の public virtual method を `When` / `ThenReturn` / `ThenThrow` / `ThenAnswer` / `ThenReturnSequence` の対象にする
- class mock の呼び出しを既存の `InvocationRecord` に記録する
- class mock の verification を既存の `Verify` / `Times` / `VerifyNoMoreInteractions` / `InOrder` で扱う
- class spy / partial mock で、stub がない virtual method は base implementation を呼ぶ設計を追加する

v2 本体でやらないこと:

- direct `new` interception
- static method mocking
- sealed class mocking
- non-virtual method mocking
- private method interception
- constructor interception
- runtime IL rewrite
- profiler API based shim
- Microsoft Fakes Shim 相当の透過的差し替え

Microsoft Fakes Shim 相当との差分:

- v2 本体は型生成による proxy の範囲に限定する
- 既存コード内の `new MyService()` や `DateTime.Now` のような呼び出しは差し替えない
- static / sealed / non-virtual / private / constructor には介入しない
- runtime rewrite や profiler API に依存しないため、通常の MSTest / Visual Studio 2022 / CI で扱いやすい一方、透過的な BCL 差し替えはできない

## 分類

v2 本体に入れるべきもの:

- public non-sealed class の proxy
- parameterless constructor を持つ class の最小 mock
- public virtual method の stubbing
- public virtual method の verification
- class spy / partial mock
- CallBase 相当の class mock option
- class proxy 固有の validation と error diagnostics
- interface mock と class mock の共存

別パッケージに分けるべきもの:

- direct new interception
- static method mocking
- sealed / non-virtual method mocking
- profiler API based shim
- runtime IL rewrite
- Microsoft Fakes Shim 相当の機能

experimental 扱いにすべきもの:

- `MiniMockito.Shims.Experimental` のような別パッケージでの runtime rewrite / profiler API 調査
- constructor interception の技術検証
- static method mocking の技術検証
- non-virtual method interception の技術検証

実装しないほうがよいもの:

- v2 本体での runtime rewrite
- v2 本体での profiler API
- v2 本体での BCL 呼び出し透過差し替え
- 外部 mocking framework への依存

## 2. public API 案

class mock:

```csharp
var mock = Mock.Class<MyService>();

When(() => mock.VirtualMethod(1))
    .ThenReturn("mocked");

Verify(() => mock.VirtualMethod(1), Times.Once());
```

class spy / partial mock:

```csharp
var spy = Spy.Class<MyService>();

When(() => spy.VirtualMethod(1))
    .ThenReturn("mocked");
```

CallBase:

```csharp
var mock = Mock.Class<MyService>(ClassMockOptions.CallBase);
```

constructor / factory injection 支援:

```csharp
var mock = Mock.Class<MyService>(
    ClassMockOptions.WithFactory(() => new MyService(dependency)));
```

または:

```csharp
var mock = Mock.Class<MyService>(new ClassMockOptions
{
    CallBase = true,
    Factory = () => new MyService(dependency)
});
```

推奨 API 方針:

- v1 の `Mock.Of<T>()` は interface 専用として維持する
- class mock は `Mock.Class<T>()` に分け、interface mock と曖昧にしない
- class spy は `Spy.Class<T>()` に分け、interface spy の `Spy.Of<T>(realInstance)` と競合させない
- option は `ClassMockOptions` に閉じ込め、今後の constructor / CallBase 拡張に備える
- unsupported target は `ClassProxyException` または `UnsupportedMockTargetException` 派生で診断を明確にする

## 3. 内部クラス構成案

候補構成:

```text
src/MiniMockito/
  Proxy/
    InterfaceProxy/
      MiniMockitoDispatchProxy
    ClassProxy/
      ClassProxyFactory
      ClassProxyBuilder
      ClassProxyTypeCache
      ClassProxyMethodEmitter
      ClassProxyInvocationDispatcher
      ClassProxyValidation
      ClassProxyUnsupportedReason
      ClassProxyException
      ClassMockOptions
```

責務:

- `ClassProxyFactory`
  - public API から呼ばれる class proxy 作成の入口
  - target validation、type cache lookup、instance creation、`MockRepository` registration を担当する
- `ClassProxyBuilder`
  - `Reflection.Emit` の assembly / module / type builder を扱う
  - generated proxy type の生成を統括する
- `ClassProxyTypeCache`
  - target type と option の組み合わせごとに generated proxy type を cache する
  - thread-safe にする
- `ClassProxyMethodEmitter`
  - public virtual method override の IL emit を担当する
  - method arguments の boxing、dispatcher 呼び出し、return value unboxing / casting を扱う
- `ClassProxyInvocationDispatcher`
  - generated proxy から呼ばれる共通 dispatcher
  - `MockState`、`StubRule`、`DefaultValueProvider`、CallBase の分岐を扱う
- `ClassProxyValidation`
  - target class / constructor / method support を検査する
  - diagnostics 用の supported / unsupported method list を作る
- `ClassProxyUnsupportedReason`
  - `SealedClass`, `NoParameterlessConstructor`, `NonVirtualMethod`, `StaticMethod`, `GenericMethod`, `RefOutParameter` などの理由を列挙する
- `ClassProxyException`
  - class proxy 固有の失敗を表す
  - message に `Target class:`, `Method:`, `Reason:`, `Supported methods:`, `Unsupported methods:`, `Hint:` を含める
- `ClassMockOptions`
  - `CallBase`, `Factory`, 将来の constructor argument support などを保持する

## 4. 既存実装の再利用方針

`MockState`:

- class mock でも同じ state を使う
- class mock / class spy 用に `RealInstance` や `CallBase` 相当の option を追加する可能性はある
- v1 の interface mock / spy の挙動を壊さないよう、class proxy 固有情報は optional にする

`MockRepository`:

- generated class proxy instance を既存 repository に登録する
- `Verify`, `VerifyNoInteractions`, `VerifyNoMoreInteractions`, `InOrder` は repository から state を取得してそのまま動く設計にする

`InvocationRecord`:

- class proxy でも同じ record を使う
- `MethodInfo` は original target method を記録するのが望ましい
- generated override method を記録すると stubbing / verification expression の `MethodInfo` と一致しにくいため避ける

`StubRule` / `StubBehavior`:

- class proxy でも同じ rule resolution を使う
- `ReturnBehavior`, `ThrowBehavior`, `AnswerBehavior`, sequence は再利用する
- CallBase は新しい `StubBehavior` として入れるか、dispatcher option として扱うかを Phase 2 で決める

`ArgumentMatcher`:

- 既存の matcher をそのまま使う
- class method の arguments は generated proxy 側で `object?[]` に boxing して matcher に渡す

Verifier:

- `VerificationSetupFactory` は class proxy の expression でも同じ式木解析で使える可能性が高い
- `MockRepository.Default.GetState(mock)` が class proxy instance を認識できれば、既存 `VerificationEngine` を再利用できる

OrderVerifier:

- `InOrderContext` は sequence number だけを見るため、class proxy でもそのまま使う

`DefaultValueProvider`:

- lenient class mock の unstubbed virtual method 戻り値に再利用する
- async return handling も既存方針を使う

## 5. Reflection.Emit で実装する場合の方針

proxy class 生成:

- target class を継承する dynamic type を生成する
- target class が sealed の場合は unsupported
- abstract class は Phase 2 では対象外にする。将来対応する場合も abstract method の扱いを別途設計する
- generated type は private static module 内に生成し、`ClassProxyTypeCache` で再利用する

constructor:

- v2 Phase 2 MVP は public / protected parameterless constructor 必須にする
- generated proxy の parameterless constructor から base parameterless constructor を呼ぶ
- constructor injection / factory injection は Phase 2 MVP では設計だけに留め、Phase 3 以降で実装する
- factory injection は proxy instance 生成との相性が難しいため、real instance delegate 方式か constructor argument support のどちらが良いか検証が必要

virtual method override:

- public virtual method かつ final ではない method だけ override する
- static / private / non-virtual / sealed override / final method は unsupported list に入れる
- override body は以下を行う
  - `this` から proxy state を取得
  - original target `MethodInfo` を取得
  - arguments を `object?[]` に boxing
  - `ClassProxyInvocationDispatcher.Invoke(...)` を呼ぶ
  - return value を expected return type に cast / unbox
  - void は return value を捨てる

MethodInfo / arguments:

- method token から generated override ではなく target method の `MethodInfo` を渡す
- generic type の closed method で `MethodInfo` 解決が必要になる可能性がある
- Phase 2 MVP は non-generic public virtual method 優先にする

return value / exception:

- dispatcher は既存 interface proxy と同様に invocation を記録する
- stub があれば stub result を返す
- strict かつ stub なしなら class proxy diagnostics 付き例外を投げる
- lenient かつ stub なしなら `DefaultValueProvider` を使う
- CallBase 有効時は stub なしの場合に base implementation を呼ぶ
- exception は `InvocationRecord.Exception` に記録する

generics:

- Phase 2 MVP では generic methods は unsupported にする
- generic class の closed type は後続 Phase で検討する
- open generic type の mock は unsupported

async return:

- generated method は return type を変えない
- dispatcher は `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` に既存の `ReturnValueAdapter` / `DefaultValueProvider` 方針を使う
- async state machine 自体には介入しない

protected virtual method:

- Phase 2 MVP では public virtual method のみ対応する
- protected virtual method は stubbing expression から自然に指定しづらい
- 将来対応する場合は protected member setup API が必要になるため別設計にする

parameterless constructor:

- Phase 2 MVP は parameterless constructor 必須
- constructor arguments は Phase 3 以降で検討する

## 6. Castle DynamicProxy を使わない場合の難所

IL emit の複雑さ:

- method signature ごとの boxing / unboxing / cast / void handling が必要
- async return は通常の return type として扱えるが、stub result の型変換に注意が必要

constructors:

- base constructor 呼び出しは IL 的に制約がある
- parameterized constructor support は API と generated constructor の両方が必要

generic methods:

- method generic parameter の扱い、closed `MethodInfo` 解決、return type unboxing が難しい
- Phase 2 MVP では非対応にするのが安全

ref / out parameters:

- `object?[]` に boxing するだけでは呼び出し後の書き戻しができない
- Phase 2 MVP では unsupported

protected virtual methods:

- public API から expression で指定しづらい
- protected setup API を作ると public API が重くなる
- Phase 2 MVP では unsupported

value type return:

- dispatcher result の null / incompatible type を unbox すると実行時例外になる
- `ReturnValueAdapter` と validation を強化する必要がある

async return:

- `ThenReturn("abc")` を `Task<string>` に包む処理は既存再利用できる
- `ThenThrow` と faulted task の扱いは方針を決める必要がある。v1 同様に invocation 時に例外を投げる方針を維持するのが単純

debugging:

- generated IL の stack imbalance は原因追跡が難しい
- diagnostic tests と小さい emitter helper が重要

performance:

- 初回 type generation は高コスト
- type cache が必須
- invocation dispatcher は reflection lookup を cache する必要がある

type caching:

- target type、CallBase option、constructor shape で cache key を切る
- cache は thread-safe にする
- dynamic assembly unload は v1/v2 MVP では扱わない

## 7. v2 Phase 2 の最小スコープ

Phase 2 MVP に入れる:

- `Mock.Class<T>()`
- `Mock.Class<T>(ClassMockOptions options)`
- public non-sealed class
- public / protected parameterless constructor 必須
- public virtual non-final method のみ override
- non-generic method 優先
- void / reference type / value type return
- `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` return
- existing `When` / `Verify` / `Times` の再利用
- lenient default
- strict behavior
- class proxy 固有エラー

Phase 2 MVP で非対応:

- `Spy.Class<T>()`
- CallBase 実体動作
- constructor arguments
- factory injection
- protected virtual method stubbing
- generic methods
- ref / out parameters
- sealed / static / non-virtual / private method
- direct new interception
- runtime rewrite
- profiler API

Phase 3 以降:

- `Spy.Class<T>()`
- partial mock
- CallBase
- constructor argument support
- factory injection support

## 8. テスト方針

class proxy 作成:

- public non-sealed class with parameterless constructor は作成できる
- interface mock の既存テストがすべて通る
- sealed class / static class / no parameterless constructor は diagnostic exception

public virtual method stubbing:

- `ThenReturn`
- `ThenThrow`
- `ThenAnswer`
- `ThenReturnSequence`
- argument matcher

public virtual method verification:

- `Verify(..., Times.Once())`
- `Times.Exactly`, `Never`, `AtLeast`, `AtMost`
- `VerifyNoInteractions`
- `VerifyNoMoreInteractions`
- `InOrder` with interface mock + class mock mixed

lenient default:

- unstubbed reference return
- unstubbed value return
- unstubbed void
- async defaults

strict behavior:

- unstubbed public virtual method throws
- error includes `Target class:`, `Method:`, `Reason:`, `Supported methods:`, `Unsupported methods:`, `Hint:`

unsupported target diagnostics:

- sealed class
- non-virtual method
- static method
- private method
- generic method
- ref / out method
- no parameterless constructor

existing interface mock regression:

- all v1 tests remain unchanged
- `Mock.Of<T>()` stays interface-only
- `Spy.Of<T>(realInstance)` stays interface spy

## 9. リスク

実装難易度:

- Reflection.Emit は high risk
- method signature coverage を広げすぎると不安定になる
- MVP を public virtual non-generic method に絞るべき

既存 API への影響:

- `Mock.Of<T>()` を class 対応に広げると既存の unsupported behavior が変わるため避ける
- `Mock.Class<T>()` を追加 API にすることで v1 互換を維持する

テスト容易性:

- generated proxy は unit tests で対象 method pattern を細かく増やす必要がある
- IL emit helper 単位のテストより、public behavior tests を厚くするほうが保守しやすい

Visual Studio 2022 / MSTest との相性:

- runtime generated type は MSTest で通常実行可能
- PDB/debug support は後回しでよい
- exception diagnostics を厚くすることで IDE 利用時の調査性を補う

CI での扱いやすさ:

- runtime rewrite / profiler API を使わないため GitHub Actions でも扱いやすい
- Reflection.Emit は platform differences に注意する
- Windows + net8.0 をまず基準にする

将来の shim experimental との境界:

- v2 本体は proxy-based mocking に限定する
- `MiniMockito.Shims.Experimental` は別 package / 別 namespace / 別 docs に分ける
- 本体の `Mock.Class<T>()` が shim 機能に依存しないようにする

## 10. v2 Phase 2 用の実装プロンプト案

```markdown
AGENTS.md と docs/v2-class-proxy-design.md を読んでください。
v2 Phase 2 として class proxy MVP だけを実装してください。

範囲:
- `Mock.Class<T>()`
- `Mock.Class<T>(ClassMockOptions options)`
- `ClassMockOptions`
- `ClassProxyFactory`
- `ClassProxyBuilder`
- `ClassProxyTypeCache`
- `ClassProxyMethodEmitter`
- `ClassProxyValidation`
- `ClassProxyException`
- public non-sealed class
- public/protected parameterless constructor 必須
- public virtual non-final non-generic method の stubbing / verification
- void / reference / value / Task / Task<T> / ValueTask / ValueTask<T> return
- lenient default
- strict behavior
- class proxy 固有 diagnostics

制約:
- `Mock.Of<T>()` の interface mock API を壊さない
- `Spy.Of<T>()` の interface spy API を壊さない
- class spy / CallBase はまだ実装しない
- direct new interception は実装しない
- static method mocking は実装しない
- sealed / non-virtual method mocking は実装しない
- runtime IL rewrite は実装しない
- profiler API は使わない
- 外部 mocking framework は使わない
- ref / out / generic method は unsupported diagnostics を出す

テスト:
- class proxy 作成
- public virtual method ThenReturn / ThenThrow / ThenAnswer / sequence
- matchers
- Verify / Times
- VerifyNoInteractions / VerifyNoMoreInteractions
- lenient default
- strict error
- unsupported target diagnostics
- existing interface mock regression

最後に `dotnet build` と `dotnet test` を実行し、失敗した場合は修正してください。
最後の報告は日本語でお願いします。
```
