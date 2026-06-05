# MiniMockito.Net

MiniMockito.Net is a lightweight .NET mocking framework intended to feel natural with Visual Studio 2022 and MSTest.

Phase 1 provides the foundation only:

- `Mock.Of<T>()` for interface mocks
- `DispatchProxy` based interface proxying
- internal invocation recording
- lenient default return values for unstubbed calls
- base exception and core model types

The following features are intentionally not implemented in Phase 1:

- `When` / `ThenReturn` / `ThenThrow` / `ThenAnswer`
- `Verify`
- concrete argument matchers
- captors
- spies
- in-order verification
- class proxies
- source generators
- runtime rewriting

## Example

```csharp
var service = Mock.Of<IMyService>();

var name = service.GetName(123);
```

Unstubbed calls on lenient mocks return default values and are recorded internally.
