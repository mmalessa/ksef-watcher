namespace KsefWatcher.SubjectConfiguration;

/// <summary>Schema sketch from docs/08_subject_configuration_tactical_model.md.</summary>
public sealed class SubjectConfig
{
    public string Nip { get; set; } = string.Empty;
    public int IntervalOffset { get; set; } // minutes; explicit position within ConfigFile.IntervalMinutes's shared window — operator-set, not auto-computed
    public string KsefToken { get; set; } = string.Empty; // literal or ${ENV_VAR} — resolved by the loader (OQ-13)
    public string AmountDisplay { get; set; } = "brutto"; // OQ-16: brutto | netto
    public List<ChannelConfig> Channels { get; set; } = new();
}
