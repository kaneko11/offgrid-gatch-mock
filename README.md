# MiniMockito.Net

MiniMockito.Net is a lightweight .NET mocking framework intended to feel natural with Visual Studio 2022 and MSTest.

Current Phase 2 scope provides:

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

The following features are intentionally not implemented yet:

- `Verify`
- captors
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
```

Unstubbed calls on lenient mocks return default values and are recorded internally.
