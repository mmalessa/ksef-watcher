using System.Text.RegularExpressions;
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
    private static readonly HashSet<string> SupportedChannelTypes = ["discord"]; // V1
    private static readonly HashSet<string> SupportedAmountDisplays = ["brutto", "netto"]; // OQ-16
    private static readonly Regex EnvVarReference = new(@"^\$\{(?<name>[^}]+)\}$"); // OQ-13

    public ConfigLoadResult Load(string yaml)
    {
        var config = Deserializer.Deserialize<ConfigFile>(yaml);
        var errors = new List<string>();

        if (config.Version != SupportedSchemaVersion)
        {
            errors.Add($"version: unsupported schema version {config.Version} (expected {SupportedSchemaVersion}).");
        }

        for (var i = 0; i < config.Subjects.Count; i++)
        {
            var subject = config.Subjects[i];
            subject.Environment ??= config.DefaultEnvironment; // OQ-9: inherit when not explicitly overridden
            ValidateSubject(subject, i, errors);
        }

        return errors.Count > 0
            ? new ConfigLoadResult.Failure(errors)
            : new ConfigLoadResult.Success(config);
    }

    /// <summary>Resolves <c>${ENV_VAR}</c> credentials (OQ-13) in place, then validates (I-13/I-13a).</summary>
    private void ValidateSubject(SubjectConfig subject, int index, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(subject.Nip))
        {
            errors.Add($"subjects[{index}].nip: must not be empty.");
        }
        else if (!HasValidNipChecksum(subject.Nip))
        {
            errors.Add($"subjects[{index}].nip: invalid checksum (I-13).");
        }

        var envMatch = EnvVarReference.Match(subject.KsefToken ?? string.Empty);
        if (envMatch.Success)
        {
            var varName = envMatch.Groups["name"].Value;
            var resolved = environmentVariables.Get(varName);
            if (resolved is null)
            {
                errors.Add($"subjects[{index}].ksefToken: environment variable '{varName}' is not set.");
            }
            else
            {
                subject.KsefToken = resolved;
            }
        }

        if (string.IsNullOrEmpty(subject.KsefToken))
        {
            errors.Add($"subjects[{index}].ksefToken: must not be empty.");
        }

        if (subject.IntervalMinutes < MinIntervalMinutes)
        {
            errors.Add($"subjects[{index}].intervalMinutes: must be at least {MinIntervalMinutes} (I-13a).");
        }

        if (subject.IntervalMinutes > MaxIntervalMinutes)
        {
            errors.Add($"subjects[{index}].intervalMinutes: must be at most {MaxIntervalMinutes} (I-13b).");
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
            }
            else if (channel.Type == "discord" && string.IsNullOrWhiteSpace(channel.WebhookUrl))
            {
                errors.Add($"subjects[{index}].channels[{c}].webhookUrl: must not be empty for type 'discord'.");
            }
        }
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
