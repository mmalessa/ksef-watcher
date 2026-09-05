using KsefWatcher.Host.Configuration;
using KsefWatcher.Host.Tests.TestDoubles;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.Host.Tests.Configuration;

public class ConfigFileWatcherTests
{
    private const string ValidYamlA = """
        version: 1
        environment: test
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

    private const string ValidYamlB = """
        version: 1
        environment: test
        intervalMinutes: 60
        subjects:
          - nip: "9999999999"
            intervalOffset: 0
            ksefToken: "token-b"
            channels:
              - type: discord
                token: "bot-token-b"
                channelId: "222222222222222222"
        """;

    private static ConfigWatcher NewConfigWatcher(string initialYaml) =>
        ConfigWatcher.Start(new ConfigLoader(new EnvironmentVariables()), initialYaml);

    [Fact]
    public void HandleFileChanged_ReadsFileAndAppliesReload()
    {
        var configWatcher = NewConfigWatcher(ValidYamlA);
        var fileReader = new FakeConfigFileReader(() => ValidYamlB);
        var sut = new ConfigFileWatcher(configWatcher, fileReader, "config.yaml");

        sut.HandleFileChanged();

        Assert.Equal("9999999999", configWatcher.Current.Subjects[0].Nip);
    }

    [Fact]
    public void HandleFileChanged_WhenReadThrowsIOException_DoesNotThrow_KeepsLastValidConfig()
    {
        var configWatcher = NewConfigWatcher(ValidYamlA);
        var fileReader = new FakeConfigFileReader(() => throw new IOException("file locked by another process"));
        var sut = new ConfigFileWatcher(configWatcher, fileReader, "config.yaml");

        var exception = Record.Exception(() => sut.HandleFileChanged());

        Assert.Null(exception);
        Assert.Equal("5260001246", configWatcher.Current.Subjects[0].Nip);
    }

    [Fact]
    public void HandleFileChanged_WithMalformedYaml_DoesNotThrow_KeepsLastValidConfig()
    {
        const string malformedYaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects: [
            """;
        var configWatcher = NewConfigWatcher(ValidYamlA);
        var fileReader = new FakeConfigFileReader(() => malformedYaml);
        var sut = new ConfigFileWatcher(configWatcher, fileReader, "config.yaml");

        var exception = Record.Exception(() => sut.HandleFileChanged());

        Assert.Null(exception);
        Assert.Equal("5260001246", configWatcher.Current.Subjects[0].Nip);
    }

    [Fact]
    public void HandleFileChanged_WhenReadThrowsIOException_LogsWarning()
    {
        var configWatcher = NewConfigWatcher(ValidYamlA);
        var fileReader = new FakeConfigFileReader(() => throw new IOException("file locked by another process"));
        var logger = new FakeLogger<ConfigFileWatcher>();
        var sut = new ConfigFileWatcher(configWatcher, fileReader, "config.yaml", logger);

        sut.HandleFileChanged();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }
}
