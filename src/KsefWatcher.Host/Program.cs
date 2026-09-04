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

// I-13: fail-fast at startup — FindConfigFile/ConfigWatcher.Start throw on a missing/invalid file.
var configPath = FindConfigFile();
var configWatcher = ConfigWatcher.Start(
    new ConfigLoader(new EnvironmentVariables()),
    File.ReadAllText(configPath),
    bootstrapLoggerFactory.CreateLogger<ConfigWatcher>());

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

builder.Services.AddHttpClient<DiscordNotifier>();
builder.Services.AddSingleton<IChannelSender>(sp => sp.GetRequiredService<DiscordNotifier>());
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
