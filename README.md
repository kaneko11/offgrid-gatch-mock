# MiniMockito.Net

MiniMockito.Net is a lightweight .NET mocking framework intended to feel natural with Visual Studio 2022 and MSTest.

Current Phase 3 scope provides:

- `Mock.Of<T>()` for interface mocks
- `DispatchProxy` based interface proxying
- internal invocation recording
- lenient default return values for unstubbed calls
- base exception and core model types
- `When(...).ThenReturn(...)`
- `When(...).ThenThrow(...)`
- `When(...).ThenAnswer(...)`
- `When(...).ThenReturnSequence(...)`
- basic argument matchers: `Any`, `Eq`, `Is`, `Null`, `NotNull`, `InRange`
- `Verify(...)` with `Times.Once`, `Exactly`, `Never`, `AtLeast`, `AtMost`
- `VerifyNoInteractions(...)`
- `VerifyNoMoreInteractions(...)`
- argument captors via `Capture<T>()`
- strict mocks via `Mock.Of<T>(MockBehavior.Strict)`

The following features are intentionally not implemented yet:

- spies
- in-order verification
- class proxies
- source generators
- runtime rewriting

## Example

```csharp
using static MiniMockito.Mock;

var service = Mock.Of<IMyService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

var name = service.GetName(123);

Verify(() => service.GetName(123), Times.Once());
```

Unstubbed calls on lenient mocks return default values and are recorded internally.
