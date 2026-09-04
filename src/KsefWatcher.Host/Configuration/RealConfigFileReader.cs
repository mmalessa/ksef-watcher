namespace KsefWatcher.Host.Configuration;

/// <summary>Production <see cref="IConfigFileReader"/> — reads straight from disk.</summary>
public sealed class RealConfigFileReader : IConfigFileReader
{
    public string ReadAllText(string path) => File.ReadAllText(path);
}
