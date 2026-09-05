using KsefWatcher.SubjectConfiguration.Tests.TestDoubles;
using Xunit;

namespace KsefWatcher.SubjectConfiguration.Tests;

public class ConfigLoaderTests
{
    private static ConfigLoader NewLoader(IReadOnlyDictionary<string, string>? env = null) =>
        new(new FakeEnvironmentVariables(env ?? new Dictionary<string, string>()));

    private const string ValidYaml = """
        version: 1
        environment: test
        intervalMinutes: 60
        subjects:
          - nip: "5260001246"
            intervalOffset: 0
            ksefToken: "literal-token"
            amountDisplay: brutto
            channels:
              - type: discord
                token: "bot-token"
                channelId: "111111111111111111"
        """;

    [Fact]
    public void ValidYaml_ParsesIntoConfigFile()
    {
        var result = NewLoader().Load(ValidYaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal(1, success.Config.Version);
        Assert.Equal("test", success.Config.Environment);
        Assert.Equal(60, success.Config.IntervalMinutes);
        var subject = Assert.Single(success.Config.Subjects);
        Assert.Equal("5260001246", subject.Nip);
        Assert.Equal(0, subject.IntervalOffset);
        Assert.Equal("literal-token", subject.KsefToken);
        Assert.Equal("brutto", subject.AmountDisplay);
        var channel = Assert.Single(subject.Channels);
        Assert.Equal("discord", channel.Type);
        Assert.Equal("bot-token", channel.Token);
        Assert.Equal("111111111111111111", channel.ChannelId);
    }

    [Fact]
    public void DatabasePath_WhenNotSpecified_DefaultsToNull()
    {
        var result = NewLoader().Load(ValidYaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Null(success.Config.DatabasePath);
    }

    [Fact]
    public void DatabasePath_WhenSpecified_IsParsed()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            databasePath: /var/lib/ksef-watcher/state.db
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("/var/lib/ksef-watcher/state.db", success.Config.DatabasePath);
    }

    [Fact]
    public void MalformedYaml_ReturnsFailure_InsteadOfThrowing()
    {
        const string malformedYaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects: [
            """;

        var result = NewLoader().Load(malformedYaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("could not be parsed"));
    }

    [Fact]
    public void UnknownSchemaVersion_FailsValidation()
    {
        const string yaml = """
            version: 2
            environment: test
            intervalMinutes: 60
            subjects: []
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("version"));
    }

    [Fact]
    public void EmptyNip_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: ""
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].nip"));
    }

    [Fact]
    public void NipWithInvalidChecksum_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "1234567890"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].nip") && e.Contains("checksum"));
    }

    [Fact]
    public void EmptyKsefToken_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: ""
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].ksefToken"));
    }

    [Fact]
    public void IntervalBelowFifteenMinutes_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 10
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("intervalMinutes"));
    }

    [Fact]
    public void IntervalAboveSevenDays_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 10081
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("intervalMinutes"));
    }

    [Fact]
    public void IntervalAtSevenDays_IsAccepted()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 10080
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal(10080, success.Config.IntervalMinutes);
    }

    [Fact]
    public void IntervalOffsetNegative_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: -1
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].intervalOffset"));
    }

    [Fact]
    public void IntervalOffsetEqualToIntervalMinutes_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].intervalOffset"));
    }

    [Fact]
    public void IntervalOffsetOneBelowIntervalMinutes_IsAccepted()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 59
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal(59, success.Config.Subjects[0].IntervalOffset);
    }

    [Fact]
    public void UnknownChannelType_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: slack
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].type"));
    }

    [Fact]
    public void DiscordChannel_WithoutToken_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].token"));
    }

    [Fact]
    public void DiscordChannel_WithoutChannelId_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].channelId"));
    }

    [Fact]
    public void DiscordToken_EnvVarReference_ResolvesFromEnvironment()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "${DISCORD_TOKEN}"
                    channelId: "${DISCORD_CHANNEL}"
            """;
        var env = new Dictionary<string, string>
        {
            ["DISCORD_TOKEN"] = "resolved-bot-token",
            ["DISCORD_CHANNEL"] = "999999999999999999",
        };

        var result = NewLoader(env).Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        var channel = success.Config.Subjects[0].Channels[0];
        Assert.Equal("resolved-bot-token", channel.Token);
        Assert.Equal("999999999999999999", channel.ChannelId);
    }

    [Fact]
    public void DiscordToken_EnvVarReference_MissingVariable_FailsValidation_NamingTheVariable()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "${DISCORD_TOKEN}"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].token") && e.Contains("DISCORD_TOKEN"));
    }

    [Fact]
    public void LogsChannel_WithoutTokenOrChannelId_IsAccepted()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: logs
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("logs", success.Config.Subjects[0].Channels[0].Type);
    }

    [Fact]
    public void SubjectWithNoChannels_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels: []
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels") && e.Contains("exactly one"));
    }

    [Fact]
    public void SubjectWithMultipleChannels_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token-a"
                    channelId: "111111111111111111"
                  - type: discord
                    token: "bot-token-b"
                    channelId: "222222222222222222"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels") && e.Contains("exactly one"));
    }

    [Fact]
    public void UnknownEnvironment_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: prpd
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("environment"));
    }

    [Fact]
    public void EnvironmentWithDifferentCasing_IsAccepted()
    {
        const string yaml = """
            version: 1
            environment: PROD
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("PROD", success.Config.Environment);
    }

    [Fact]
    public void UnknownAmountDisplay_FailsValidation()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                amountDisplay: gross
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].amountDisplay"));
    }

    [Fact]
    public void NettoAmountDisplay_IsAccepted()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "literal-token"
                amountDisplay: netto
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("netto", success.Config.Subjects[0].AmountDisplay);
    }

    [Fact]
    public void EnvVarToken_ResolvesFromEnvironment()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "${KSEF_TOKEN}"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;
        var env = new Dictionary<string, string> { ["KSEF_TOKEN"] = "resolved-secret-value" };

        var result = NewLoader(env).Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("resolved-secret-value", success.Config.Subjects[0].KsefToken);
    }

    [Fact]
    public void EnvVarToken_MissingVariable_FailsValidation_NamingTheVariable()
    {
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "${KSEF_TOKEN}"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].ksefToken") && e.Contains("KSEF_TOKEN"));
    }

    [Fact]
    public void ErrorMessages_NeverContainTheResolvedSecretValue()
    {
        // Second subject has a bad NIP, forcing a Failure result while the first subject's
        // real secret has already been resolved into the (discarded) config — I-14 must hold
        // even though a genuine secret value passed through the loader.
        const string yaml = """
            version: 1
            environment: test
            intervalMinutes: 60
            subjects:
              - nip: "5260001246"
                intervalOffset: 0
                ksefToken: "super-secret-value"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
              - nip: ""
                intervalOffset: 0
                ksefToken: "another-secret"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        var allErrors = string.Join("\n", failure.Errors);
        Assert.DoesNotContain("super-secret-value", allErrors);
        Assert.DoesNotContain("another-secret", allErrors);
    }
}
