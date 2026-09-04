using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KsefWatcher.SubjectConfiguration;

/// <summary>
/// Holds the current validated config and applies hot-reload semantics
/// (docs/07_define_subject_configuration.md, docs/05_connect_message_flows.md Scenario D):
/// a reload either atomically replaces the whole config (I-17), or is rejected in its entirety,
/// keeping the last valid config in effect and reporting the rejection loudly (I-16).
/// Watching the actual file on disk (FileSystemWatcher) is separate Host infrastructure — this
/// class only reacts to file *content* it's handed.
/// </summary>
public sealed class ConfigWatcher
{
    private readonly ConfigLoader _loader;
    private readonly ILogger<ConfigWatcher> _logger;

    private ConfigWatcher(ConfigLoader loader, ConfigFile initial, ILogger<ConfigWatcher> logger)
    {
        _loader = loader;
        Current = initial;
        _logger = logger;
    }

    public ConfigFile Current { get; private set; }

    public event Action<ConfigFile>? Reloaded;

    /// <summary>I-16: invalid file on reload ⇒ Current is left untouched; this fires instead of Reloaded.</summary>
    public event Action<IReadOnlyList<string>>? ReloadRejected;

    public void Reload(string yaml)
    {
        var result = _loader.Load(yaml);
        switch (result)
        {
            case ConfigLoadResult.Success success:
                Current = success.Config;
                Reloaded?.Invoke(Current);
                break;
            case ConfigLoadResult.Failure failure:
                _logger.LogError("Invalid config on reload, keeping last valid config in effect (I-16): {Errors}", string.Join("; ", failure.Errors));
                ReloadRejected?.Invoke(failure.Errors);
                break;
        }
    }

    /// <summary>I-13: fail-fast at startup — throws <see cref="InvalidConfigException"/> if the initial file is invalid.</summary>
    public static ConfigWatcher Start(ConfigLoader loader, string initialYaml, ILogger<ConfigWatcher>? logger = null)
    {
        var result = loader.Load(initialYaml);
        if (result is ConfigLoadResult.Failure failure)
        {
            throw new InvalidConfigException(failure.Errors);
        }

        return new ConfigWatcher(loader, ((ConfigLoadResult.Success)result).Config, logger ?? NullLogger<ConfigWatcher>.Instance);
    }
}
