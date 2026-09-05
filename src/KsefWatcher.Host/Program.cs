using KSeF.Client.Api.Services;
using KSeF.Client.ClientFactory;
using KSeF.Client.ClientFactory.DI;
using KsefWatcher.Host;
using KsefWatcher.Host.Configuration;
using KsefWatcher.Host.Persistence;
using KsefWatcher.Host.Scheduling;
using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.KsefAccess;
using KsefWatcher.NotificationDelivery;
using KsefWatcher.NotificationDelivery.Notifiers;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Bootstrap logger: ConfigWatcher.Start runs before the Host's own DI-provided logging exists,
// ahead of `Host.CreateApplicationBuilder`. Kept alive for the process lifetime — ConfigWatcher
// logs invalid reloads (I-16) through it for as long as the daemon runs, not just at startup.
var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole());

// I-13: fail-fast at startup on a missing/invalid config file — reported as a clean one-line
// (or short bulleted) message on stderr with exit code 1, not a raw .NET stack trace.
string configPath;
ConfigWatcher configWatcher;
try
{
    configPath = FindConfigFile();
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
catch (InvalidConfigException ex)
{
    Console.Error.WriteLine("ksef-watcher: config.yaml is invalid:");
    foreach (var error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

var stateDbPath = Path.Combine(Path.GetDirectoryName(configPath)!, "state.db");
var repository = new SqliteSubjectWatchRepository($"Data Source={stateDbPath}");
await repository.EnsureSchemaAsync(CancellationToken.None);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.RegisterKSeFClientFactory();
builder.Services.AddSingleton(configWatcher);
builder.Services.AddSingleton(repository);
builder.Services.AddSingleton<ISubjectWatchRepository>(repository);
builder.Services.AddSingleton<ICredentialsStore, ConfigCredentialsStore>();

// Per-subject environment (OQ-9): KsefClientAdapter resolves its dependencies per call from
// each subject's own SubjectCredentials.Environment, not a single fixed instance — so one daemon
// process serves subjects across test/demo/prod simultaneously. IKSeFClientFactory/
// IKSeFFactoryCryptographyServices already cache internally per environment.
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

static string FindConfigFile()
{
    string[] candidates =
    [
        Path.Combine(AppContext.BaseDirectory, "config.yaml"), // A12
        "/etc/ksef-watcher/config.yaml",
    ];
    return Array.Find(candidates, File.Exists)
        ?? throw new FileNotFoundException($"No config.yaml found. Searched: {string.Join(", ", candidates)} (A12).");
}
