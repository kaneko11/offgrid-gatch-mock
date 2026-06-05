using MiniMockito;
using static MiniMockito.Mock;

var service = Mock.Of<IMyService>();

When(() => service.GetName(Any<int>()))
    .ThenReturn("abc");

Console.WriteLine(service.GetName(123));
Verify(() => service.GetName(123), Times.Once());

var captor = Capture<string>();
service.Save("captured");
Verify(() => service.Save(captor.Value));
Console.WriteLine(captor.CapturedValue);

var real = new RealService();
var spy = Spy.Of<IMyService>(real);
When(() => spy.GetName(0)).ThenReturn("stubbed");

Console.WriteLine(spy.GetName(0));
Console.WriteLine(spy.GetName(7));

var first = Mock.Of<IWorkflowStep>();
var second = Mock.Of<IWorkflowStep>();
first.Start();
second.Save();
first.End();

var order = InOrder(first, second);
order.Verify(() => first.Start());
order.Verify(() => second.Save());
order.Verify(() => first.End());

internal interface IMyService
{
    string? GetName(int id);

    void Save(string value);
}

internal sealed class RealService : IMyService
{
    public string GetName(int id)
    {
        return $"real-{id}";
    }

    public void Save(string value)
    {
    }
}

internal interface IWorkflowStep
{
    void Start();

    void Save();

    void End();
}
