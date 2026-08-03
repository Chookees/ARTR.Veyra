using System.Collections.Concurrent;
using ARTR.Veyra.Core.RateLimiting;

namespace ARTR.Veyra.Infrastructure.RateLimiting;

public sealed class MemoryRateLimiterStore : IRateLimiterStore
{
    private readonly ConcurrentDictionary<string, WindowCounter> _windows = new(StringComparer.Ordinal);

    public ValueTask<bool> TryAcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(permitLimit, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var counter = _windows.AddOrUpdate(
            key,
            static (_, state) => new WindowCounter(state.Now, state.Window),
            static (_, existing, state) =>
            {
                if (state.Now - existing.WindowStart >= state.Window)
                {
                    return new WindowCounter(state.Now, state.Window);
                }

                return existing;
            },
            (Now: now, Window: window));

        lock (counter.SyncRoot)
        {
            if (now - counter.WindowStart >= window)
            {
                counter.Reset(now);
            }

            if (counter.Count >= permitLimit)
            {
                return ValueTask.FromResult(false);
            }

            counter.Count++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTimeOffset windowStart, TimeSpan window)
        {
            WindowStart = windowStart;
            Window = window;
        }

        public object SyncRoot { get; } = new();

        public DateTimeOffset WindowStart { get; private set; }

        public TimeSpan Window { get; }

        public int Count { get; set; }

        public void Reset(DateTimeOffset windowStart)
        {
            WindowStart = windowStart;
            Count = 0;
        }
    }
}
