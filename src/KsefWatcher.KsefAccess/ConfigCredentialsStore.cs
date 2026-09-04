using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;

namespace KsefWatcher.KsefAccess;

/// <summary>
/// Implementation of <see cref="ICredentialsStore"/> reading from Subject Configuration's current,
/// validated config (docs/08_ksef_access_tactical_model.md: "configStore.Current(subjectId)").
/// Always reads live from <see cref="ConfigWatcher.Current"/> — reflects hot reloads without caching.
/// </summary>
public sealed class ConfigCredentialsStore(ConfigWatcher configWatcher) : ICredentialsStore
{
    public SubjectCredentials Current(SubjectId subjectId)
    {
        var subject = configWatcher.Current.Subjects.FirstOrDefault(s => s.Nip == subjectId.Nip);
        if (subject is null)
        {
            throw new InvalidOperationException($"No configuration for subject '{subjectId.Nip}' — should be impossible; the scheduler only polls configured subjects.");
        }

        return new SubjectCredentials(subject.Nip, subject.KsefToken, subject.Environment!);
    }
}
