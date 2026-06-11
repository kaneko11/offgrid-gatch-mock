# Phase 18 — .NET Framework 4.8 + x86 Interface Proxy Backend

## 1. 問題

`.NET Framework 4.8` + `PlatformTarget=x86` の環境で、interface mock / spy が次の例外で失敗していた。

```text
TypeLoadException: アクセスが拒否されました: 'MiniMockito.Proxy.MiniMockitoDispatchProxy'
```

### 根本原因

`Mock.Of<T>()` / `Spy.Of<T>(real)` は `System.Reflection.DispatchProxy.Create<T, MiniMockitoDispatchProxy>()`
で proxy を生成していた。`DispatchProxy` は実行時に動的 proxy 型を `TypeBuilder.CreateTypeInfo()` で
生成するが、この経路が **net48 + x86** の組み合わせで内部的に失敗し、生成中の
`MiniMockitoDispatchProxy` 派生型のロードが `TypeLoadException`（アクセス拒否）になる。

- net8.0 では発生しない（DispatchProxy は正常動作）。
- `MiniMockito.Shims.Experimental` とは**無関係**。あちらは assembly rewrite + ALC のレイヤーで、
  本件は interface proxy backend のレイヤー。

## 2. 方針

DispatchProxy を直接呼ぶ実装を **proxy backend 抽象化**に分離し、TFM ごとに backend を切り替える。
既存の invocation pipeline / stubbing / verification / matcher / captor / strict・lenient は
**再実装せず再利用**する（backend は `MethodInfo` + `object[]` を core handler に渡すだけ）。

## 3. アーキテクチャ

```
Mock.Of<T>() / Spy.Of<T>(real)
  → MockRepository.CreateState(...)             （従来どおり）
  → MockStateInterceptor(state)                 （共有 core invocation handler）
  → InterfaceProxyFactorySelector.Resolve()     （TFM で backend を選択）
       net8.0 → DispatchProxyInterfaceProxyFactory      （DispatchProxy）
       net48  → NetFrameworkRealProxyInterfaceProxyFactory（RealProxy）
  → factory.Create(interfaceType, interceptor)  → proxy object
  → MockRepository.Register(proxy, state)        （従来どおり）
```

### 追加した型（`src/MiniMockito/Proxy/`）

| 型 | 役割 |
|----|------|
| `IMiniMockitoInterceptor` | backend が呼ぶ core invocation handler の抽象（`object? Invoke(MethodInfo, object?[])`） |
| `MockStateInterceptor` | **唯一の**invocation 実装（record → stub → real(spy) → strict/throw → default）。両 backend が共有 |
| `IInterfaceProxyFactory` | proxy 生成 backend の抽象（`Name` / `Create(Type, IMiniMockitoInterceptor)`） |
| `DispatchProxyInterfaceProxyFactory` | DispatchProxy backend（net8.0） |
| `NetFrameworkRealProxyInterfaceProxyFactory` | RealProxy backend（`#if NETFRAMEWORK`、net48） |
| `InterfaceProxyFactorySelector` | TFM で backend を選択 |
| `ProxyBackendDiagnostics` / `ProxyBackendInfo` | 選択 backend / TFM / プロセス bitness / 理由を内部診断 |

`MiniMockitoDispatchProxy` は mock ロジックを持たず、`IMiniMockitoInterceptor.Invoke` に委譲するだけに
変更。旧 `IMockProxy` は不要になり削除。

### RealProxy backend の要点

- `RealProxy(interfaceType)` の `GetTransparentProxy()` が interface を実装した透過 proxy を返す。
- `Invoke(IMessage)` で `IMethodCallMessage` から `MethodInfo` / `Args` を取り出し、
  `interceptor.Invoke(method, args)` に委譲、結果を `ReturnMessage` で返す。例外は `ReturnMessage(ex, call)`。
- `System.Object` のメソッド（`ToString` / `Equals` / `GetHashCode` / `GetType`）は interface 契約外なので
  interceptor に渡さず backend 内で処理（DispatchProxy が interface メソッドのみ intercept するのと同じ挙動）。
  これにより strict mock で `ToString()` 等が誤って例外にならない。

## 4. backend 選択

net48 では **常に RealProxy** を使う（`#if NETFRAMEWORK`）。x86 / x64 の判定は不要だが、
診断用に `Environment.Is64BitProcess` を `ProxyBackendInfo` に含める。net8.0 は DispatchProxy。

## 5. テスト

`tests/MiniMockito.Net48X86Tests/`（`net48` / `LangVersion=7.3` / `PlatformTarget=x86` / `Prefer32Bit=true`）。

- 再現/回帰: `Net48X86_InterfaceMock_DoesNotThrowTypeLoadException`、
  `Net48X86_MockOf_Interface_DoesNotUseBrokenDispatchProxyPath`（backend == RealProxy を確認）。
- Mock.Of / ThenReturn / Verify / Times.Once / strict / lenient default / spy 委譲 / spy 部分 stub /
  Any・Eq・Is matcher / Capture / ThenThrow / ThenAnswer / ThenReturnSequence /
  Task&lt;T&gt; stub・default / ValueTask&lt;T&gt; stub・default / VerifyNoInteractions /
  VerifyNoMoreInteractions / InOrder。
- テスト host が実際に 32-bit で動作していること（`Is64BitProcess=False`, `IntPtr.Size=4`）を確認済み。

## 6. 対象外（このフェーズで変更しない）

class proxy / sealed / non-virtual / static / constructor new / Shims.Experimental の新機能 /
DispatchProxy 自体の修正 / Castle DynamicProxy / Moq・NSubstitute 依存 / assembly rewrite /
CLR Profiling API / detour。
