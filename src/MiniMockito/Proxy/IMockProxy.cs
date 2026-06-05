using MiniMockito.Core;

namespace MiniMockito.Proxy;

internal interface IMockProxy
{
    void Configure(MockState state);
}
