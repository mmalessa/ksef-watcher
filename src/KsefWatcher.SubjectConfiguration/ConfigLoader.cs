using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KsefWatcher.SubjectConfiguration;

/// <summary>
/// Parses, validates (I-13/I-13a/I-15) and resolves credentials (OQ-13) for the config file
/// (docs/07_define_subject_configuration.md, docs/08_subject_configuration_tactical_model.md).
/// Hot-reload watching/keep-last-valid (I-16/I-17) is a runtime component built separately in Host.
/// </summary>
public sealed class ConfigLoader(IEnvironmentVariables environmentVariables)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private const int SupportedSchemaVersion = 1; // I-15
    private const int MinIntervalMinutes = 15; // I-13a, MF recommendation
    private const int MaxIntervalMinutes = 10080; // I-13b, 7 days — operator decision
    private static readonly int[] NipChecksumWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
    private static readonly HashSet<string> SupportedChannelTypes = ["discord", "logs"]; // "logs" — dev/testing only, writes to the daemon's own log instead of a real messenger
    private static readonly HashSet<string> SupportedAmountDisplays = ["brutto", "netto"]; // OQ-16
    private static readonly HashSet<string> SupportedEnvironments = new(["test", "demo", "prod"], StringComparer.OrdinalIgnoreCase); // OQ-9
    private static readonly Regex EnvVarReference = new(@"^\$\{(?<name>[^}]+)\}$"); // OQ-13

    public ConfigLoadResult Load(string yaml)
    {
        ConfigFile config;
        try
        {
            config = Deserializer.Deserialize<ConfigFile>(yaml);
        }
        catch (YamlException ex)
        {
            return new ConfigLoadResult.Failure([$"yaml: could not be parsed ({ex.Message})"]);
        }

        var errors = new List<string>();

        if (config.Version != SupportedSchemaVersion)
        {
            errors.Add($"version: unsupported schema version {config.Version} (expected {SupportedSchemaVersion}).");
        }

        if (!SupportedEnvironments.Contains(config.Environment))
        {
            errors.Add($"environment: must be 'test', 'demo' or 'prod' (OQ-9), was '{config.Environment}'.");
        }

        if (config.IntervalMinutes < MinIntervalMinutes)
        {
            errors.Add($"intervalMinutes: must be at least {MinIntervalMinutes} (I-13a).");
        }

        if (config.IntervalMinutes > MaxIntervalMinutes)
        {
            errors.Add($"intervalMinutes: must be at most {MaxIntervalMinutes} (I-13b).");
        }

        for (var i = 0; i < config.Subjects.Count; i++)
        {
            ValidateSubject(config.Subjects[i], i, config.IntervalMinutes, errors);
        }

        return errors.Count > 0
            ? new ConfigLoadResult.Failure(errors)
            : new ConfigLoadResult.Success(config);
    }

    /// <summary>Resolves <c>${ENV_VAR}</c> credentials (OQ-13) in place, then validates (I-13/I-13a).</summary>
    private void ValidateSubject(SubjectConfig subject, int index, int intervalMinutes, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(subject.Nip))
        {
            errors.Add($"subjects[{index}].nip: must not be empty.");
        }
        else if (!HasValidNipChecksum(subject.Nip))
        {
            errors.Add($"subjects[{index}].nip: invalid checksum (I-13).");
        }

        subject.KsefToken = ResolveEnvVarReference(subject.KsefToken, $"subjects[{index}].ksefToken", errors);

        if (string.IsNullOrEmpty(subject.KsefToken))
        {
            errors.Add($"subjects[{index}].ksefToken: must not be empty.");
        }

        if (subject.IntervalOffset < 0 || subject.IntervalOffset >= intervalMinutes)
        {
            errors.Add($"subjects[{index}].intervalOffset: must be between 0 and {intervalMinutes - 1} (within intervalMinutes), was {subject.IntervalOffset}.");
        }

        if (!SupportedAmountDisplays.Contains(subject.AmountDisplay))
        {
            errors.Add($"subjects[{index}].amountDisplay: must be 'brutto' or 'netto' (OQ-16), was '{subject.AmountDisplay}'.");
        }

        if (subject.Channels.Count != 1)
        {
            errors.Add($"subjects[{index}].channels: must have exactly one entry (OQ-12), had {subject.Channels.Count}.");
        }

        for (var c = 0; c < subject.Channels.Count; c++)
        {
            var channel = subject.Channels[c];
            if (!SupportedChannelTypes.Contains(channel.Type))
            {
                errors.Add($"subjects[{index}].channels[{c}].type: unsupported channel type '{channel.Type}'.");
                continue;
            }

            if (channel.Type != "discord")
            {
                continue;
            }

            channel.Token = ResolveEnvVarReference(channel.Token, $"subjects[{index}].channels[{c}].token", errors);
            channel.ChannelId = ResolveEnvVarReference(channel.ChannelId, $"subjects[{index}].channels[{c}].channelId", errors);

            if (string.IsNullOrWhiteSpace(channel.Token))
            {
                errors.Add($"subjects[{index}].channels[{c}].token: must not be empty for type 'discord'.");
            }

            if (string.IsNullOrWhiteSpace(channel.ChannelId))
            {
                errors.Add($"subjects[{index}].channels[{c}].channelId: must not be empty for type 'discord'.");
            }
        }
    }

    /// <summary>Resolves a <c>${ENV_VAR}</c> reference (OQ-13) if present; returns the value unchanged otherwise.</summary>
    private string? ResolveEnvVarReference(string? value, string fieldLabel, List<string> errors)
    {
        var match = EnvVarReference.Match(value ?? string.Empty);
        if (!match.Success)
        {
            return value;
        }

        var varName = match.Groups["name"].Value;
        var resolved = environmentVariables.Get(varName);
        if (resolved is null)
        {
            errors.Add($"{fieldLabel}: environment variable '{varName}' is not set.");
            return value;
        }

        return resolved;
    }

    private static bool HasValidNipChecksum(string nip)
    {
        if (nip.Length != 10 || !nip.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < NipChecksumWeights.Length; i++)
        {
            sum += (nip[i] - '0') * NipChecksumWeights[i];
        }

        var checkDigit = sum % 11;
        return checkDigit != 10 && checkDigit == nip[9] - '0';
    }
}
