# MiniMockito.Net

MiniMockito.Net is a lightweight .NET mocking framework designed to feel natural with Visual Studio 2022 and MSTest.

It is not a Microsoft Fakes replacement. v1 focuses on interface mock / spy workflows. v2 adds class proxy support for public virtual methods on public non-sealed classes.

## What v2 Can Do

- Create interface mocks with `Mock.Of<T>()`
- Create interface spies with `Spy.Of<T>(realInstance)`
- Create class mocks with `Mock.Class<T>()`
- Create class spies and partial mocks with `Spy.Class<T>()` or `Mock.Class<T>(ClassMockOptions.CallBase)`
- Record invocations with method, arguments, timestamp, sequence number, return value, exception, thread ID, and mock ID
- Stub calls with `When`, `ThenReturn`, `ThenThrow`, `ThenAnswer`, and `ThenReturnSequence`
- Match arguments with `Any`, `Eq`, `Is`, `Null`, `NotNull`, and `InRange`
- Capture arguments with `Capture<T>()`
- Verify calls with `Times.Once`, `Exactly`, `Never`, `AtLeast`, and `AtMost`
- Check `VerifyNoInteractions` and `VerifyNoMoreInteractions`
- Verify order across multiple mocks and spies with `InOrder`
- Return natural async defaults for `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`
- Use strict or lenient mock behavior

## What v2 Cannot Do

- Intercept direct `new SomeClass()` calls
- Mock static methods
- Mock sealed classes
- Intercept non-virtual methods
- Intercept private methods
- Intercept constructors
- Rewrite IL at runtime
- Use CLR profiler API based shims
- Transparently replace .NET Framework or BCL calls

These high-risk shim scenarios are intentionally outside the main package. If they are explored later, they should live in a separate experimental package such as `MiniMockito.Shims.Experimental`.

## Installation / Local Build

This repository currently builds from source.

```bash
dotnet restore
dotnet build
dotnet test
```

Projects:

- `src/MiniMockito/MiniMockito.csproj`
- `tests/MiniMockito.Tests/MiniMockito.Tests.csproj`
- `samples/MiniMockito.Sample/MiniMockito.Sample.csproj`
- `samples/MiniMockito.Sample.MSTest/MiniMockito.Sample.MSTest.csproj`

## Visual Studio 2022 + MSTest

Add a project reference or NuGet package reference to your MSTest project, then import the API:

```csharp
using MiniMockito;
using static MiniMockito.Mock;
```

The MSTest sample project in `samples/MiniMockito.Sample.MSTest` contains executable examples for interface mocks, spies, class proxies, matchers, captors, and async methods.

## Interface Mock

```csharp
var service = Mock.Of<IUserService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

Assert.AreEqual("abc", service.GetName(123));
Verify(() => service.GetName(123), Times.Once());
```

Lenient mocks return default values for unstubbed calls. Interface mocks use `DispatchProxy`, so `T` must be an interface.

## Interface Spy

```csharp
var realService = new RealUserService();
var spy = Spy.Of<IUserService>(realService);

When(() => spy.GetName(0))
    .ThenReturn("stubbed");

Assert.AreEqual("stubbed", spy.GetName(0));
Assert.AreEqual("real-7", spy.GetName(7));
```

Interface spies are still interface proxies. When no stub matches, the call is delegated to the supplied real instance.

## Class Proxy

```csharp
var repository = Mock.Class<UserRepository>();

When(() => repository.FindName(1))
    .ThenReturn("mocked");

Assert.AreEqual("mocked", repository.FindName(1));
Verify(() => repository.FindName(1), Times.Once());
```

Class proxy support is intentionally narrow:

- `T` must be a public non-sealed class
- `T` must have a public or protected parameterless constructor
- only public virtual methods are intercepted
- non-virtual, static, private, generic, `ref`, and `out` methods are not supported

## Class Spy / Partial Mock

```csharp
var calculator = Spy.Class<TaxCalculator>();

When(() => calculator.GetRate("test"))
    .ThenReturn(0.20m);

Assert.AreEqual(0.20m, calculator.GetRate("test"));
Assert.AreEqual(0.10m, calculator.GetRate("default"));
```

`Spy.Class<T>()` and `Mock.Class<T>(ClassMockOptions.CallBase)` call the base implementation when no stub matches. Stubbed public virtual methods use the configured stub behavior.

## Stubbing

```csharp
When(() => service.GetName(1))
    .ThenThrow(new InvalidOperationException());

When(() => service.GetName(Any<int>()))
    .ThenAnswer(ctx => "id=" + ctx.Arguments[0]);

When(() => service.GetName(2))
    .ThenReturnSequence("a", "b", "c");
```

`ThenReturnSequence` returns values in order and repeats the last value after the sequence is exhausted.

## Matchers

```csharp
When(() => service.GetName(Any<int>())).ThenReturn("any");
When(() => service.GetName(Eq(10))).ThenReturn("ten");
When(() => service.GetName(Is<int>(value => value > 0))).ThenReturn("positive");
When(() => service.Find(Null<string>())).ThenReturn("missing");
When(() => service.Find(NotNull<string>())).ThenReturn("present");
When(() => service.GetName(InRange(1, 5))).ThenReturn("range");
```

Arguments without matchers use equality matching.

## Captor

```csharp
var captor = Capture<string>();

service.Save("abc");

Verify(() => service.Save(captor.Value));

Assert.AreEqual("abc", captor.CapturedValue);
```

Captor values are collected only after successful verification.

## Verification

```csharp
service.Save("abc");

Verify(() => service.Save("abc"), Times.Once());
Verify(() => service.Save("missing"), Times.Never());
VerifyNoMoreInteractions(service);
```

Successful `Verify` calls mark matching invocations as verified. `Verify(() => mock.Method(...))` evaluates the expression in verification mode and does not record that expression evaluation as a normal invocation.

## InOrder

```csharp
var first = Mock.Of<IWorkflowStep>();
var second = Mock.Class<WorkflowStep>();

first.Start();
second.Save();
first.End();

var order = InOrder(first, second);
order.Verify(() => first.Start());
order.Verify(() => second.Save());
order.Verify(() => first.End());
```

`InOrder` uses the global invocation sequence number, so it can verify ordering across multiple interface mocks, class proxies, and spies.

## Strict / Lenient

```csharp
var lenient = Mock.Of<IUserService>();
var strict = Mock.Of<IUserService>(MockBehavior.Strict);
var strictClass = Mock.Class<UserRepository>(MockBehavior.Strict);
```

- Lenient: unstubbed calls return default values.
- Strict: unstubbed calls throw a `MockException` or `ClassProxyException` with method and argument diagnostics.

## Async Behavior

Unstubbed async returns are completed defaults:

- `Task` returns `Task.CompletedTask`
- `Task<T>` returns a completed task with `default(T)`
- `ValueTask` returns a completed default value task
- `ValueTask<T>` returns a completed value task with `default(T)`

`ThenReturn` can be used with the logical result value:

```csharp
When(() => service.GetNameAsync(Any<int>()))
    .ThenReturn("abc");

Assert.AreEqual("abc", await service.GetNameAsync(123));
```

## Error Message Shape

Verification failures include labels intended for IDE and CI diagnostics:

```text
Wanted:
Actual invocations:
Matching invocations:
Method:
Expected count:
Actual count:
Arguments:
Closest recorded calls:
```

Class proxy failures include class-specific diagnostics:

```text
Target class:
Method:
Reason:
Supported methods:
Unsupported methods:
Hint:
```

## Known Constraints

- Interface mocks and spies require interfaces.
- Class mocks and class spies require public non-sealed classes with a public or protected parameterless constructor.
- Class proxy only intercepts public virtual methods.
- Static, sealed, non-virtual, private, constructor, and direct `new` interception are not implemented.
- Runtime IL rewrite and profiler API based shims are not implemented.
- Generic methods and `ref` / `out` parameters are outside the class proxy MVP.
- MiniMockito does not depend on Moq, NSubstitute, FakeItEasy, JustMock, Rhino Mocks, Microsoft Fakes, or Castle DynamicProxy.

## Future Experimental Shims

The main `MiniMockito` package remains proxy-based. Future research for direct `new`, static, sealed, and non-virtual mocking should be isolated in `MiniMockito.Shims.Experimental`.

The first recommended experiment is a Roslyn analyzer / code fix that suggests adapter, factory, or injectable clock seams for hard-to-mock code. Runtime IL rewriting, CLR Profiling API, and method patching are higher-risk options and should not be part of the stable main package.
