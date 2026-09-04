using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.KsefAccess;

/// <summary>
/// Read-only access to validated per-subject KSeF credentials (docs/08_ksef_access_tactical_model.md,
/// "configStore.Current(subjectId)"). Implemented later against Subject Configuration's parsed
/// config (A5) — out of scope for this pass.
/// </summary>
public interface ICredentialsStore
{
    SubjectCredentials Current(SubjectId subjectId);
}
