using KsefWatcher.SubjectConfiguration.Tests.TestDoubles;
using Xunit;

namespace KsefWatcher.SubjectConfiguration.Tests;

public class ConfigLoaderTests
{
    private static ConfigLoader NewLoader(IReadOnlyDictionary<string, string>? env = null) =>
        new(new FakeEnvironmentVariables(env ?? new Dictionary<string, string>()));

    private const string ValidYaml = """
        version: 1
        defaultEnvironment: test
        subjects:
          - nip: "5260001246"
            intervalMinutes: 60
            ksefToken: "literal-token"
            environment: test
            amountDisplay: brutto
            channels:
              - type: discord
                webhookUrl: "https://example.invalid/webhook"
        """;

    [Fact]
    public void ValidYaml_ParsesIntoConfigFile()
    {
        var result = NewLoader().Load(ValidYaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal(1, success.Config.Version);
        Assert.Equal("test", success.Config.DefaultEnvironment);
        var subject = Assert.Single(success.Config.Subjects);
        Assert.Equal("5260001246", subject.Nip);
        Assert.Equal(60, subject.IntervalMinutes);
        Assert.Equal("literal-token", subject.KsefToken);
        Assert.Equal("test", subject.Environment);
        Assert.Equal("brutto", subject.AmountDisplay);
        var channel = Assert.Single(subject.Channels);
        Assert.Equal("discord", channel.Type);
        Assert.Equal("https://example.invalid/webhook", channel.WebhookUrl);
    }

    [Fact]
    public void MalformedYaml_ReturnsFailure_InsteadOfThrowing()
    {
        const string malformedYaml = """
            version: 1
            defaultEnvironment: test
            subjects: [
            """;

        var result = NewLoader().Load(malformedYaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("could not be parsed"));
    }

    [Fact]
    public void SubjectWithoutExplicitEnvironment_InheritsDefaultEnvironment()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: prod
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("prod", success.Config.Subjects[0].Environment);
    }

    [Fact]
    public void UnknownSchemaVersion_FailsValidation()
    {
        const string yaml = """
            version: 2
            defaultEnvironment: test
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
            defaultEnvironment: test
            subjects:
              - nip: ""
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "1234567890"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: ""
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 10
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].intervalMinutes"));
    }

    [Fact]
    public void IntervalAboveSevenDays_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 10081
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].intervalMinutes"));
    }

    [Fact]
    public void IntervalAtSevenDays_IsAccepted()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 10080
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal(10080, success.Config.Subjects[0].IntervalMinutes);
    }

    [Fact]
    public void UnknownChannelType_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: slack
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].type"));
    }

    [Fact]
    public void DiscordChannel_WithoutWebhookUrl_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].channels[0].webhookUrl"));
    }

    [Fact]
    public void SubjectWithNoChannels_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/a"
                  - type: discord
                    webhookUrl: "https://example.invalid/b"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                environment: prpd
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].environment"));
    }

    [Fact]
    public void EnvironmentWithDifferentCasing_IsAccepted()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                environment: PROD
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var success = Assert.IsType<ConfigLoadResult.Success>(result);
        Assert.Equal("PROD", success.Config.Subjects[0].Environment);
    }

    [Fact]
    public void UnknownDefaultEnvironment_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: prpd
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        Assert.Contains(failure.Errors, e => e.Contains("subjects[0].environment"));
    }

    [Fact]
    public void UnknownAmountDisplay_FailsValidation()
    {
        const string yaml = """
            version: 1
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                amountDisplay: gross
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "literal-token"
                amountDisplay: netto
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "${KSEF_TOKEN}"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "${KSEF_TOKEN}"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
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
            defaultEnvironment: test
            subjects:
              - nip: "5260001246"
                intervalMinutes: 60
                ksefToken: "super-secret-value"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
              - nip: ""
                intervalMinutes: 60
                ksefToken: "another-secret"
                channels:
                  - type: discord
                    webhookUrl: "https://example.invalid/webhook"
            """;

        var result = NewLoader().Load(yaml);

        var failure = Assert.IsType<ConfigLoadResult.Failure>(result);
        var allErrors = string.Join("\n", failure.Errors);
        Assert.DoesNotContain("super-secret-value", allErrors);
        Assert.DoesNotContain("another-secret", allErrors);
    }
}
