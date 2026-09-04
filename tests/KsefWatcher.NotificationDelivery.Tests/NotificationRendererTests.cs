using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.NotificationDelivery;
using Xunit;

namespace KsefWatcher.NotificationDelivery.Tests;

public class NotificationRendererTests
{
    private static DetectedInvoice AnyInvoice(string? issuerName = "Contractor Sp. z o.o.") =>
        new(new InvoiceReference("KSEF-1"), "FV/1/2026", 100m, 123m, "PLN", "1111111111", issuerName);

    [Fact]
    public void Renders_IssuerName_InvoiceNumber_Ref_AndGrossAmount_WhenBrutto()
    {
        var message = NotificationRenderer.Render(AnyInvoice(), AmountDisplay.Brutto);

        Assert.Contains("Contractor Sp. z o.o.", message);
        Assert.Contains("FV/1/2026", message);
        Assert.Contains("KSEF-1", message);
        Assert.Contains("123", message);
        Assert.Contains("PLN", message);
    }

    [Fact]
    public void FallsBackToNip_WhenIssuerNameAbsent()
    {
        var message = NotificationRenderer.Render(AnyInvoice(issuerName: null), AmountDisplay.Brutto);

        Assert.Contains("NIP 1111111111", message);
    }

    [Fact]
    public void ShowsNetAmount_NotGrossAmount_WhenNetto()
    {
        var message = NotificationRenderer.Render(AnyInvoice(), AmountDisplay.Netto);

        Assert.Contains("100", message);
        Assert.DoesNotContain("123", message);
    }

    [Fact]
    public void RenderHeartbeat_MentionsNoNewInvoicesAndTheDate()
    {
        var asOf = new DateOnly(2026, 1, 15);

        var message = NotificationRenderer.RenderHeartbeat(asOf);

        Assert.Contains("no new invoices", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-01-15", message);
    }
}
