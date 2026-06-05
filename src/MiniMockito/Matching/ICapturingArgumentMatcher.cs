namespace MiniMockito.Matching;

internal interface ICapturingArgumentMatcher
{
    void Capture(object? argument);
}
