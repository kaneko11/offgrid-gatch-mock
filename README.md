# MiniMockito.Net

MiniMockito.Net is a lightweight .NET mocking framework for interface-based tests, designed to feel natural with Visual Studio 2022 and MSTest.

It is not a Microsoft Fakes replacement. The v1 scope focuses on mock, stub, spy, matching, captor, and verification workflows for interfaces.

## What It Can Do

- Create interface mocks with `DispatchProxy`
- Record every invocation with method, arguments, sequence number, return value, exception, thread ID, and mock ID
- Stub calls with `When`, `ThenReturn`, `ThenThrow`, `ThenAnswer`, and `ThenReturnSequence`
- Match arguments with `Any`, `Eq`, `Is`, `Null`, `NotNull`, and `InRange`
- Verify calls with `Times.Once`, `Exactly`, `Never`, `AtLeast`, and `AtMost`
- Check `VerifyNoInteractions` and `VerifyNoMoreInteractions`
- Capture arguments with `Capture<T>()`
- Create interface spies with `Spy.Of<T>(realInstance)`
- Verify order across multiple mocks with `InOrder`
- Return natural async values for `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`
- Use strict or lenient mock behavior

## What It Cannot Do

- Mock classes
- Mock sealed classes
- Intercept non-virtual methods
- Replace static methods
- Intercept private methods or constructors
- Rewrite IL at runtime
- Use profiler API based shims
- Transparently replace .NET Framework or BCL calls

## Installation / Local Build

This repository currently builds from source.

```bash
dotnet build
dotnet test
```

Projects:

- `src/MiniMockito/MiniMockito.csproj`
- `tests/MiniMockito.Tests/MiniMockito.Tests.csproj`
- `samples/MiniMockito.Sample/MiniMockito.Sample.csproj`

## Basic Mock

```csharp
using MiniMockito;
using static MiniMockito.Mock;

var service = Mock.Of<IMyService>();

var name = service.GetName(123);
```

Lenient mocks return default values for unstubbed calls.

## When / ThenReturn

```csharp
var service = Mock.Of<IMyService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

var result = service.GetName(123);
```

## ThenThrow / ThenAnswer / Sequence

```csharp
When(() => service.GetName(1))
    .ThenThrow(new InvalidOperationException());

When(() => service.GetName(Any<int>()))
    .ThenAnswer(ctx => "id=" + ctx.Arguments[0]);

When(() => service.GetName(2))
    .ThenReturnSequence("a", "b", "c");
```

## Verify

```csharp
service.GetName(123);

Verify(() => service.GetName(123), Times.Once());
Verify(() => service.GetName(999), Times.Never());
VerifyNoMoreInteractions(service);
```

Successful `Verify` calls mark matching invocations as verified.

## Matchers

```csharp
When(() => service.GetName(Any<int>())).ThenReturn("any");
When(() => service.GetName(Eq(10))).ThenReturn("ten");
When(() => service.GetName(Is<int>(value => value > 0))).ThenReturn("positive");
When(() => service.Save(Null<string>())).ThenReturn();
When(() => service.Save(NotNull<string>())).ThenReturn();
When(() => service.GetName(InRange(1, 5))).ThenReturn("range");
```

Arguments without matchers use equality matching.

## Captor

```csharp
var captor = Capture<string>();

service.Save("abc");

Verify(() => service.Save(captor.Value));

var value = captor.CapturedValue;
var values = captor.CapturedValues;
```

Captor values are collected only after a successful `Verify`.

## Spy

```csharp
var realService = new RealService();
var spy = Spy.Of<IMyService>(realService);

When(() => spy.GetName(0))
    .ThenReturn("stubbed");

var stubbed = spy.GetName(0);
var real = spy.GetName(7);
```

Spies are still interface proxies. If no stub matches, the proxy delegates to the supplied real instance.

## InOrder

```csharp
var first = Mock.Of<IWorkflowStep>();
var second = Mock.Of<IWorkflowStep>();

first.Start();
second.Save();
first.End();

var order = InOrder(first, second);
order.Verify(() => first.Start());
order.Verify(() => second.Save());
order.Verify(() => first.End());
```

`InOrder` uses the global invocation sequence number, so it can verify ordering across multiple mocks.

## Strict / Lenient

```csharp
var lenient = Mock.Of<IMyService>();
var strict = Mock.Of<IMyService>(MockBehavior.Strict);
```

- Lenient: unstubbed calls return default values.
- Strict: unstubbed calls throw `MockException` with mock ID, method, arguments, and stub candidates.

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
```

## v1 Limitations

- Interface proxying only
- No class proxy
- No static, constructor, private method, or non-virtual interception
- No runtime IL rewriting
- No profiler API shims
- No external mocking framework dependency
- Spy requires an interface and a real instance implementing that interface

## Future Extension Ideas

- Class proxy experiments behind a separate boundary
- Richer diagnostics for matcher mismatch explanations
- Additional matchers
- Optional source generator helpers
- Better support for by-ref and out parameters
- NuGet packaging and versioned release workflow
