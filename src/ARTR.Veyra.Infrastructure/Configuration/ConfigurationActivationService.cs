using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ARTR.Veyra.Infrastructure.Configuration;

public interface IConfigurationActivationState
{
    long Generation { get; }

    string Fingerprint { get; }

    bool IsLastKnownGoodActive { get; }

    DateTimeOffset LastActivatedUtc { get; }
}

public sealed partial class ConfigurationActivationService : IHostedService, IConfigurationActivationState, IDisposable
{
    private static readonly Meter Meter = new("ARTR.Veyra");
    private static readonly Counter<long> ActivationFailures =
        Meter.CreateCounter<long>(
            "veyra_config_activation_failures_total",
            description: "Configuration activation failures");

    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<VeyraOptions> _options;
    private readonly IValidateOptions<VeyraOptions> _validator;
    private readonly ILogger<ConfigurationActivationService> _logger;

    private readonly object _gate = new();
    private IDisposable? _changeSubscription;
    private VeyraOptions? _lastKnownGood;
    private string _fingerprint = "none";
    private long _generation;
    private bool _isLastKnownGoodActive;
    private DateTimeOffset _lastActivatedUtc = DateTimeOffset.UnixEpoch;

    public ConfigurationActivationService(
        IConfiguration configuration,
        IOptionsMonitor<VeyraOptions> options,
        IValidateOptions<VeyraOptions> validator,
        ILogger<ConfigurationActivationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public long Generation => Interlocked.Read(ref _generation);

    public string Fingerprint
    {
        get
        {
            lock (_gate)
            {
                return _fingerprint;
            }
        }
    }

    public bool IsLastKnownGoodActive
    {
        get
        {
            lock (_gate)
            {
                return _isLastKnownGoodActive;
            }
        }
    }

    public DateTimeOffset LastActivatedUtc
    {
        get
        {
            lock (_gate)
            {
                return _lastActivatedUtc;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Activate(_options.CurrentValue, isReload: false);
        _changeSubscription = _options.OnChange(OnOptionsChanged);
        _ = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            OnConfigurationChanged);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _changeSubscription?.Dispose();
        _changeSubscription = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _changeSubscription?.Dispose();

    private void OnOptionsChanged(VeyraOptions options, string? name) =>
        OnConfigurationChanged();

    private void OnConfigurationChanged()
    {
        try
        {
            Activate(_options.CurrentValue, isReload: true);
        }
        catch (Exception ex)
        {
            ActivationFailures.Add(1);
            LogReloadFailed(_logger, ex);
            lock (_gate)
            {
                _isLastKnownGoodActive = _lastKnownGood is not null;
            }
        }
    }

    private void Activate(VeyraOptions candidate, bool isReload)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.ConfigurationReload.Enabled && isReload)
        {
            return;
        }

        var result = _validator.Validate(Options.DefaultName, candidate);
        if (result.Failed)
        {
            ActivationFailures.Add(1);
            LogActivationRejected(_logger, string.Join("; ", result.Failures ?? []));
            lock (_gate)
            {
                _isLastKnownGoodActive = _lastKnownGood is not null && candidate.ConfigurationReload.RetainLastKnownGood;
            }

            if (!isReload)
            {
                throw new OptionsValidationException(Options.DefaultName, typeof(VeyraOptions), result.Failures);
            }

            return;
        }

        var fingerprint = ComputeFingerprint(candidate);
        long generation;
        lock (_gate)
        {
            _lastKnownGood = candidate;
            _fingerprint = fingerprint;
            generation = Interlocked.Increment(ref _generation);
            _isLastKnownGoodActive = false;
            _lastActivatedUtc = DateTimeOffset.UtcNow;
        }

        LogActivated(_logger, generation, fingerprint, isReload);
    }

    private static string ComputeFingerprint(VeyraOptions options)
    {
        var json = JsonSerializer.Serialize(options);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
#pragma warning disable CA1308
        return Convert.ToHexString(hash).ToLowerInvariant();
#pragma warning restore CA1308
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Configuration reload activation failed; retaining last-known-good.")]
    private static partial void LogReloadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Rejecting configuration activation: {Failures}")]
    private static partial void LogActivationRejected(ILogger logger, string failures);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Configuration activated generation={Generation} fingerprint={Fingerprint} reload={IsReload}")]
    private static partial void LogActivated(ILogger logger, long generation, string fingerprint, bool isReload);
}
