using KsefWatcher.SubjectConfiguration.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.SubjectConfiguration.Tests;

public class ConfigWatcherTests
{
    private static ConfigLoader NewLoader() => new(new FakeEnvironmentVariables(new Dictionary<string, string>()));

    private const string ValidYaml = """
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

    private const string InvalidYaml = """
        version: 1
        environment: test
        intervalMinutes: 60
        subjects:
          - nip: ""
            intervalOffset: 0
            ksefToken: "token-a"
            channels:
              - type: discord
                token: "bot-token-a"
                channelId: "111111111111111111"
        """;

    [Fact]
    public void Start_WithValidYaml_ExposesItAsCurrent()
    {
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml);

        Assert.Equal("5260001246", watcher.Current.Subjects[0].Nip);
    }

    [Fact]
    public void Start_WithInvalidYaml_ThrowsWithErrors()
    {
        var ex = Assert.Throws<InvalidConfigException>(() => ConfigWatcher.Start(NewLoader(), InvalidYaml));

        Assert.Contains(ex.Errors, e => e.Contains("subjects[0].nip"));
    }

    private const string ValidYamlV2 = """
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

    [Fact]
    public void Reload_WithValidYaml_ReplacesCurrent_AndRaisesReloaded()
    {
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml);
        ConfigFile? raised = null;
        watcher.Reloaded += config => raised = config;

        watcher.Reload(ValidYamlV2);

        Assert.Equal("9999999999", watcher.Current.Subjects[0].Nip);
        Assert.NotNull(raised);
        Assert.Equal("9999999999", raised!.Subjects[0].Nip);
    }

    [Fact]
    public void Reload_WithInvalidYaml_KeepsLastValidCurrent_RaisesReloadRejected_DoesNotThrow()
    {
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml);
        var originalCurrent = watcher.Current;
        IReadOnlyList<string>? rejectedErrors = null;
        var reloadedFired = false;
        watcher.ReloadRejected += errors => rejectedErrors = errors;
        watcher.Reloaded += _ => reloadedFired = true;

        var exception = Record.Exception(() => watcher.Reload(InvalidYaml));

        Assert.Null(exception);
        Assert.Same(originalCurrent, watcher.Current);
        Assert.NotNull(rejectedErrors);
        Assert.Contains(rejectedErrors!, e => e.Contains("subjects[0].nip"));
        Assert.False(reloadedFired);
    }

    [Fact]
    public void Reload_WithInvalidYaml_LogsError()
    {
        var logger = new FakeLogger<ConfigWatcher>();
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml, logger);

        watcher.Reload(InvalidYaml);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void Reload_WithMalformedYaml_KeepsLastValidCurrent_DoesNotThrow()
    {
        const string malformedYaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects: [
            """;
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml);
        var originalCurrent = watcher.Current;

        var exception = Record.Exception(() => watcher.Reload(malformedYaml));

        Assert.Null(exception);
        Assert.Same(originalCurrent, watcher.Current);
    }

    [Fact]
    public void Reload_RecoversWithAValidFile_AfterAPreviousRejection()
    {
        var watcher = ConfigWatcher.Start(NewLoader(), ValidYaml);
        watcher.Reload(InvalidYaml);

        watcher.Reload(ValidYamlV2);

        Assert.Equal("9999999999", watcher.Current.Subjects[0].Nip);
    }
}
