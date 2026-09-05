namespace KsefWatcher.NotificationDelivery.Tests.TestDoubles;

public sealed class FakeHttpClientFactory(Func<HttpClient> createClient) : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return createClient();
    }
}
