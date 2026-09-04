namespace KsefWatcher.Host.Configuration;

/// <summary>
/// Abstraction over reading the config file's current content, so <see cref="ConfigFileWatcher"/>'s
/// react-to-a-change logic is testable without a real, flaky filesystem.
/// </summary>
public interface IConfigFileReader
{
    string ReadAllText(string path);
}
