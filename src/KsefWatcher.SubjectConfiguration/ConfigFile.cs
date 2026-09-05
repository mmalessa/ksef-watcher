namespace KsefWatcher.SubjectConfiguration;

/// <summary>
/// Schema sketch from docs/08_subject_configuration_tactical_model.md (I-15: explicit schema
/// version). Loading, YAML binding, validation (I-13/I-13a/I-14) and hot-reload watching
/// (I-16/I-17) are not implemented in this scaffolding pass.
/// </summary>
public sealed class ConfigFile
{
    public int Version { get; set; } = 1;
    public string Environment { get; set; } = "test"; // OQ-9 — single environment for the whole daemon, no per-subject override
    public int IntervalMinutes { get; set; } = 60; // OQ-19: default 60, min 15 (I-13a) — shared by every subject
    public string? DatabasePath { get; set; } // null = Host default (state.db next to config.yaml)
    public List<SubjectConfig> Subjects { get; set; } = new();
}
