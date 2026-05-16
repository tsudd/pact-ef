namespace PactEf.Capture.TestContext;

public static class PactEfTestContext
{
    private static readonly AsyncLocal<string?> _current = new();
    private static readonly AsyncLocal<string?> _currentClass = new();

    public static string? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public static string? CurrentClass
    {
        get => _currentClass.Value;
        set => _currentClass.Value = value;
    }
}
