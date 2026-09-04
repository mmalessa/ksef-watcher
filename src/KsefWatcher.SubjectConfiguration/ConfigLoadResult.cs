namespace KsefWatcher.SubjectConfiguration;

/// <summary>
/// I-13/I-16: loading either yields a fully validated config, or a list of field-path errors —
/// never a partially-applied result. Errors reference field paths only, never raw values (I-14).
/// </summary>
public abstract record ConfigLoadResult
{
    public sealed record Success(ConfigFile Config) : ConfigLoadResult;

    public sealed record Failure(IReadOnlyList<string> Errors) : ConfigLoadResult;
}
