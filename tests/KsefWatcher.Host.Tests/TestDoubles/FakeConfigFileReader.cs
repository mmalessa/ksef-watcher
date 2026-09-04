using KsefWatcher.Host.Configuration;

namespace KsefWatcher.Host.Tests.TestDoubles;

public sealed class FakeConfigFileReader(Func<string> read) : IConfigFileReader
{
    public string ReadAllText(string path) => read();
}
