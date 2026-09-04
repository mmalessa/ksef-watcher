namespace KsefWatcher.Host.Tests.TestDoubles;

/// <summary>A real, throwaway SQLite file per test — no fake needed, SQLite is embedded.</summary>
public sealed class TempSqliteFile : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ksef-watcher-test-{Guid.NewGuid():N}.db");
    public string ConnectionString => $"Data Source={Path}";

    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}
