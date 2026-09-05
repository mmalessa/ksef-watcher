using KSeF.Client.Api.Services;
using KSeF.Client.ClientFactory;
using KSeF.Client.ClientFactory.DI;
using KsefWatcher.Host;
using KsefWatcher.Host.Configuration;
using KsefWatcher.Host.Persistence;
using KsefWatcher.Host.Scheduling;
using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.KsefAccess;
using KsefWatcher.NotificationDelivery;
using KsefWatcher.NotificationDelivery.Notifiers;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

// Bootstrap logger: ConfigWatcher.Start runs before the Host's own DI-provided logging exists,
// ahead of `Host.CreateApplicationBuilder`. Kept alive for the process lifetime — ConfigWatcher
// logs invalid reloads (I-16) through it for as long as the daemon runs, not just at startup.
var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole());

// I-13: fail-fast at startup on bad CLI arguments or a missing/invalid config file — reported as
// a clean message on stderr with exit code 1, not a raw .NET stack trace.
string configPath;
ConfigWatcher configWatcher;
try
{
    configPath = FindConfigFile(args);
    configWatcher = ConfigWatcher.Start(
        new ConfigLoader(new EnvironmentVariables()),
        File.ReadAllText(configPath),
        bootstrapLoggerFactory.CreateLogger<ConfigWatcher>());
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"ksef-watcher: {ex.Message}");
    return 1;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"ksef-watcher: {ex.Message}");
    return 1;
}
catch (InvalidConfigException ex)
{
    Console.Error.WriteLine("ksef-watcher: config.yaml is invalid:");
    foreach (var error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

var stateDbPath = configWatcher.Current.DatabasePath ?? Path.Combine(Path.GetDirectoryName(configPath)!, "state.db");
var repository = new SqliteSubjectWatchRepository($"Data Source={stateDbPath}");
await repository.EnsureSchemaAsync(CancellationToken.None);

// --reset-hwm <nip>: a one-off maintenance command, not a daemon run — forgets the subject's HWM
// cursor and notified-invoice registry so its next poll re-establishes a fresh baseline (I-18).
// Useful when testing: a pre-existing invoice absorbed into the original baseline is otherwise
// never notified (by design), and this is the supported way to see it again.
var resetHwmIndex = Array.IndexOf(args, "--reset-hwm");
if (resetHwmIndex >= 0)
{
    if (resetHwmIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("ksef-watcher: --reset-hwm requires a NIP argument.");
        return 1;
    }

    var nipToReset = args[resetHwmIndex + 1];
    SubjectId subjectIdToReset;
    try
    {
        subjectIdToReset = new SubjectId(nipToReset);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"ksef-watcher: --reset-hwm: {ex.Message}");
        return 1;
    }

    await repository.DeleteAsync(subjectIdToReset, CancellationToken.None);
    Console.WriteLine($"ksef-watcher: reset HWM and notified-invoice registry for subject {nipToReset}. Its next poll will run as a first-ever poll (I-18): baseline only, no notifications for invoices already in KSeF.");
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.RegisterKSeFClientFactory();
builder.Services.AddSingleton(configWatcher);
builder.Services.AddSingleton(repository);
builder.Services.AddSingleton<ISubjectWatchRepository>(repository);
builder.Services.AddSingleton<ICredentialsStore, ConfigCredentialsStore>();

// OQ-9: a single environment for the whole daemon (config.yaml's top-level `environment`, no
// per-subject override) — every SubjectCredentials carries the same value. KsefClientAdapter
// still resolves its dependencies per call rather than caching one fixed instance; harmless
// given a single environment, and IKSeFClientFactory/IKSeFFactoryCryptographyServices already
// cache internally per environment regardless.
builder.Services.AddSingleton<IKsefQueryClient>(sp =>
{
    var clientFactory = sp.GetRequiredService<IKSeFClientFactory>();
    var cryptographyServiceFactory = sp.GetRequiredService<IKSeFFactoryCryptographyServices>();
    return new KsefClientAdapter(
        env => new AuthCoordinator(clientFactory.KSeFClient(env)),
        env => cryptographyServiceFactory.CryprographyService(env),
        env => clientFactory.KSeFClient(env));
});
builder.Services.AddSingleton<IInvoiceListProvider, KsefAccessService>();

// Named client, not a typed client pinned into a singleton (nameof(DiscordNotifier) matches the
// name DiscordNotifier itself requests via IHttpClientFactory.CreateClient) — lets the factory
// actually rotate the underlying handler for this long-lived daemon (stale DNS, dead connections).
builder.Services.AddHttpClient(nameof(DiscordNotifier));
builder.Services.AddSingleton<IChannelSender, DiscordNotifier>();
builder.Services.AddSingleton<IChannelSender, LogChannelSender>();
builder.Services.AddSingleton<INotifier, DeliveryService>();

builder.Services.AddSingleton<IDelay, RealDelay>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PollCycle>();

builder.Services.AddSingleton<PollingBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PollingBackgroundService>());
builder.Services.AddSingleton<HeartbeatScheduler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HeartbeatScheduler>());
builder.Services.AddHostedService<ConfigReloadCoordinator>();
builder.Services.AddHostedService(sp =>
    new ConfigFileWatcher(configWatcher, new RealConfigFileReader(), configPath, sp.GetRequiredService<ILogger<ConfigFileWatcher>>()));

var app = builder.Build();
await app.RunAsync();
return 0;

static void PrintHelp()
{
    Console.WriteLine("""
        ksef-watcher — watches KSeF invoice inboxes and notifies you the moment a new one arrives.

        Usage:
          ksef-watcher [--config <path>]
          ksef-watcher [--config <path>] --reset-hwm <nip>
          ksef-watcher --help

        Options:
          --config <path>   Path to config.yaml. Defaults to /etc/ksef-watcher/config.yaml.
          --reset-hwm <nip> Forget the given subject's HWM cursor and notified-invoice registry, so
                            its next poll re-establishes a fresh baseline (I-18) instead of resuming
                            from where it left off — useful while testing. Exits immediately; does
                            not start the daemon.
          --help, -h        Show this help and exit.
        """);
}

static string FindConfigFile(string[] args)
{
    const string defaultPath = "/etc/ksef-watcher/config.yaml";

    var flagIndex = Array.IndexOf(args, "--config");
    if (flagIndex < 0)
    {
        return File.Exists(defaultPath)
            ? defaultPath
            : throw new FileNotFoundException($"No config.yaml found at {defaultPath}. Pass --config <path> to use a different location.");
    }

    if (flagIndex + 1 >= args.Length)
    {
        throw new ArgumentException("--config requires a path argument.");
    }

    var explicitPath = args[flagIndex + 1];
    return File.Exists(explicitPath)
        ? explicitPath
        : throw new FileNotFoundException($"Config file not found: {explicitPath}");
}
