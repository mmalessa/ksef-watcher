namespace KsefWatcher.SubjectConfiguration;

/// <summary>Thrown at startup when the initial config file is invalid (I-13: fail-fast, precise error).</summary>
public sealed class InvalidConfigException(IReadOnlyList<string> errors) : Exception
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
