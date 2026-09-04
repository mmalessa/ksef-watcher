namespace KsefWatcher.SubjectConfiguration;

/// <summary>
/// Schema sketch from docs/08_subject_configuration_tactical_model.md (I-15: explicit schema
/// version). Loading, YAML binding, validation (I-13/I-13a/I-14) and hot-reload watching
/// (I-16/I-17) are not implemented in this scaffolding pass.
/// </summary>
public sealed class ConfigFile
{
    public int Version { get; set; } = 1;
    public string DefaultEnvironment { get; set; } = "test"; // OQ-9
    public List<SubjectConfig> Subjects { get; set; } = new();
}
