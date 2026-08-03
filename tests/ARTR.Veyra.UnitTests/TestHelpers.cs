using Microsoft.Extensions.Options;

namespace ARTR.Veyra.UnitTests;

internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class MutableOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    private T _value;

    public MutableOptionsMonitor(T value) => _value = value;

    public T CurrentValue => _value;

    public void Set(T value) => _value = value;

    public T Get(string? name) => _value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
