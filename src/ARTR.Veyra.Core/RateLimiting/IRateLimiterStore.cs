namespace ARTR.Veyra.Core.RateLimiting;

public interface IRateLimiterStore
{
    ValueTask<bool> TryAcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
