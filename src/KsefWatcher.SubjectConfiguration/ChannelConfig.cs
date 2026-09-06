namespace KsefWatcher.SubjectConfiguration;

/// <summary>Schema sketch from docs/08_subject_configuration_tactical_model.md.</summary>
public sealed class ChannelConfig
{
    public string Type { get; set; } = string.Empty;
    public string? Token { get; set; } // Discord bot token, e.g. "${DISCORD_TOKEN}"
    public string? ChannelId { get; set; } // Discord channel ID, e.g. "${DISCORD_CHANNEL}"
    public string? WebhookUrl { get; set; } // Discord webhook URL — takes priority over Token/ChannelId when set
}
