namespace KsefWatcher.SubjectConfiguration;

/// <summary>Schema sketch from docs/08_subject_configuration_tactical_model.md.</summary>
public sealed class SubjectConfig
{
    public string Nip { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 60; // OQ-19: default 60, min 15 (I-13a) — validated elsewhere
    public string KsefToken { get; set; } = string.Empty; // literal or ${ENV_VAR} — resolved by the loader (OQ-13)
    public string? Environment { get; set; } // OQ-9: null = inherit ConfigFile.DefaultEnvironment; resolved by the loader
    public string AmountDisplay { get; set; } = "brutto"; // OQ-16: brutto | netto
    public List<ChannelConfig> Channels { get; set; } = new();
}
