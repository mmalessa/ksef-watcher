using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Xunit;

namespace KsefWatcher.KsefAccess.Tests;

public class ConfigCredentialsStoreTests
{
    private static ConfigWatcher NewWatcher(string yaml) =>
        ConfigWatcher.Start(new ConfigLoader(new NullEnvironmentVariables()), yaml);

    private const string Yaml = """
        version: 1
        environment: prod
        intervalMinutes: 60
        subjects:
          - nip: "5260001246"
            intervalOffset: 0
            ksefToken: "token-a"
            channels:
              - type: discord
                token: "bot-token-a"
                channelId: "111111111111111111"
        """;

    [Fact]
    public void Current_ReturnsCredentials_ForConfiguredSubject()
    {
        var sut = new ConfigCredentialsStore(NewWatcher(Yaml));

        var credentials = sut.Current(new SubjectId("5260001246"));

        Assert.Equal("5260001246", credentials.Nip);
        Assert.Equal("token-a", credentials.Token);
        Assert.Equal("prod", credentials.Environment);
    }

    [Fact]
    public void Current_ThrowsForUnconfiguredSubject()
    {
        var sut = new ConfigCredentialsStore(NewWatcher(Yaml));

        Assert.Throws<InvalidOperationException>(() => sut.Current(new SubjectId("9999999999")));
    }

    [Fact]
    public void Current_ReflectsHotReload_NotCached()
    {
        const string reloadedYaml = """
            version: 1
            environment: prod
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "token-b"
                channels:
                  - type: discord
                    token: "bot-token-a"
                    channelId: "111111111111111111"
            """;
        var watcher = NewWatcher(Yaml);
        var sut = new ConfigCredentialsStore(watcher);

        watcher.Reload(reloadedYaml);
        var credentials = sut.Current(new SubjectId("5260001246"));

        Assert.Equal("token-b", credentials.Token);
    }

    private sealed class NullEnvironmentVariables : IEnvironmentVariables
    {
        public string? Get(string name) => null;
    }
}
