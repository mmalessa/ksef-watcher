namespace KsefWatcher.SubjectConfiguration;

/// <summary>Schema sketch from docs/08_subject_configuration_tactical_model.md.</summary>
public sealed class ChannelConfig
{
    public string Type { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
}
