using Microsoft.Data.Sqlite;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.Host.Persistence;

/// <summary>
/// SQLite-backed implementation of <see cref="ISubjectWatchRepository"/> (docs/09_architecture.md,
/// "Persistence"). One file <c>state.db</c> next to <c>config.yaml</c>. <c>pendingWindow</c> is
/// never persisted (transient by design, docs/08_invoice_watching_aggregates.md).
/// </summary>
public sealed class SqliteSubjectWatchRepository(string connectionString) : ISubjectWatchRepository
{
    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS subject_state (
                subject_id   TEXT PRIMARY KEY,
                last_hwm_utc TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS notified_refs (
                subject_id  TEXT NOT NULL,
                ksef_number TEXT NOT NULL,
                PRIMARY KEY (subject_id, ksef_number)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SubjectWatch> LoadAsync(SubjectId subjectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var hwmCommand = connection.CreateCommand();
        hwmCommand.CommandText = "SELECT last_hwm_utc FROM subject_state WHERE subject_id = @subjectId;";
        hwmCommand.Parameters.AddWithValue("@subjectId", subjectId.Nip);
        var hwmValue = await hwmCommand.ExecuteScalarAsync(cancellationToken);
        var lastHwm = hwmValue is string hwmText ? new Hwm(DateTimeOffset.Parse(hwmText)) : null;

        var refsCommand = connection.CreateCommand();
        refsCommand.CommandText = "SELECT ksef_number FROM notified_refs WHERE subject_id = @subjectId;";
        refsCommand.Parameters.AddWithValue("@subjectId", subjectId.Nip);
        var refs = new HashSet<InvoiceReference>();
        await using (var reader = await refsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                refs.Add(new InvoiceReference(reader.GetString(0)));
            }
        }

        return new SubjectWatch(subjectId, refs, lastHwm);
    }

    public async Task SaveAsync(SubjectWatch subject, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var upsertHwm = connection.CreateCommand();
        upsertHwm.Transaction = transaction;
        upsertHwm.CommandText = """
            INSERT INTO subject_state (subject_id, last_hwm_utc) VALUES (@subjectId, @hwm)
            ON CONFLICT(subject_id) DO UPDATE SET last_hwm_utc = excluded.last_hwm_utc;
            """;
        upsertHwm.Parameters.AddWithValue("@subjectId", subject.SubjectId.Nip);
        upsertHwm.Parameters.AddWithValue("@hwm", subject.LastHwm is null ? DBNull.Value : subject.LastHwm.Utc.ToString("o"));
        await upsertHwm.ExecuteNonQueryAsync(cancellationToken);

        foreach (var reference in subject.NotifiedRefs)
        {
            var insertRef = connection.CreateCommand();
            insertRef.Transaction = transaction;
            insertRef.CommandText = "INSERT OR IGNORE INTO notified_refs (subject_id, ksef_number) VALUES (@subjectId, @ksefNumber);";
            insertRef.Parameters.AddWithValue("@subjectId", subject.SubjectId.Nip);
            insertRef.Parameters.AddWithValue("@ksefNumber", reference.KsefNumber);
            await insertRef.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// I-19: a deliberate, full reset of a subject's state — used by Host's config-reload
    /// coordinator when a subject is removed from config, not by anything in Invoice Watching
    /// (hence not part of <see cref="ISubjectWatchRepository"/> itself).
    /// </summary>
    public async Task DeleteAsync(SubjectId subjectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var deleteRefs = connection.CreateCommand();
        deleteRefs.Transaction = transaction;
        deleteRefs.CommandText = "DELETE FROM notified_refs WHERE subject_id = @subjectId;";
        deleteRefs.Parameters.AddWithValue("@subjectId", subjectId.Nip);
        await deleteRefs.ExecuteNonQueryAsync(cancellationToken);

        var deleteState = connection.CreateCommand();
        deleteState.Transaction = transaction;
        deleteState.CommandText = "DELETE FROM subject_state WHERE subject_id = @subjectId;";
        deleteState.Parameters.AddWithValue("@subjectId", subjectId.Nip);
        await deleteState.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
