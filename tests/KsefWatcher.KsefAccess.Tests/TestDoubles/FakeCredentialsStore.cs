using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.KsefAccess;

namespace KsefWatcher.KsefAccess.Tests.TestDoubles;

public sealed class FakeCredentialsStore(SubjectCredentials credentials) : ICredentialsStore
{
    public SubjectCredentials Current(SubjectId subjectId) => credentials;
}
