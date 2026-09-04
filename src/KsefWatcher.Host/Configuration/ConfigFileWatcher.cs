using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KsefWatcher.Host.Configuration;

/// <summary>
/// Real hot-reload trigger (OQ-3): watches <c>config.yaml</c> on disk and calls
/// <see cref="ConfigWatcher.Reload"/> whenever it changes. The reactive logic
/// (<see cref="HandleFileChanged"/>) is a plain, testable method; wiring it to a real
/// <see cref="FileSystemWatcher"/> in <see cref="StartAsync"/> is thin glue, verified manually
/// rather than by an automated test (filesystem-watching is unreliable to exercise in a sandbox).
/// No debounce: editors on Linux (A6) commonly save via an atomic temp-file-plus-rename, which
/// raises exactly one event pointing at a fully-written file. An in-place write can raise more
/// than one event for the same content, but re-reading and re-`Reload`-ing identical content is
/// harmless (I-17) — simpler than an untestable timing-based coalescing scheme.
/// </summary>
public sealed class ConfigFileWatcher(
    ConfigWatcher configWatcher,
    IConfigFileReader fileReader,
    string filePath,
    ILogger<ConfigFileWatcher>? logger = null) : IHostedService, IDisposable
{
    private readonly ILogger<ConfigFileWatcher> _logger = logger ?? NullLogger<ConfigFileWatcher>.Instance;
    private FileSystemWatcher? _watcher;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var fileName = Path.GetFileName(filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };
        watcher.Changed += (_, _) => HandleFileChanged();
        watcher.Created += (_, _) => HandleFileChanged();
        watcher.Renamed += (_, _) => HandleFileChanged();
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _watcher?.Dispose();

    public void HandleFileChanged()
    {
        string content;
        try
        {
            content = fileReader.ReadAllText(filePath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read {FilePath} after a change notification (OQ-3) — will retry on the next change.", filePath);
            return;
        }

        configWatcher.Reload(content);
    }
}
