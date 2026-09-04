namespace KsefWatcher.SubjectConfiguration;

/// <summary>Abstraction over process environment variables, so <c>${ENV_VAR}</c> credential
/// resolution (OQ-13) is deterministically testable.</summary>
public interface IEnvironmentVariables
{
    string? Get(string name);
}

public sealed class EnvironmentVariables : IEnvironmentVariables
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
