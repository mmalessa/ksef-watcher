using KsefWatcher.SubjectConfiguration;

namespace KsefWatcher.SubjectConfiguration.Tests.TestDoubles;

public sealed class FakeEnvironmentVariables(IReadOnlyDictionary<string, string> values) : IEnvironmentVariables
{
    public string? Get(string name) => values.TryGetValue(name, out var value) ? value : null;
}
