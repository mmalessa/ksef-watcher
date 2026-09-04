namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The registry key (docs/08_invoice_watching_value_objects.md). ACL-translated from KSeF's
/// <c>KsefNumber</c> — no other KSeF shape enters this context. Equality is case-insensitive,
/// matching the official C# client's E2E dedup convention.
/// </summary>
public sealed class InvoiceReference : IEquatable<InvoiceReference>
{
    public string KsefNumber { get; }

    public InvoiceReference(string ksefNumber)
    {
        if (string.IsNullOrWhiteSpace(ksefNumber))
        {
            throw new ArgumentException("KsefNumber must not be empty.", nameof(ksefNumber));
        }

        KsefNumber = ksefNumber;
    }

    public bool Equals(InvoiceReference? other) =>
        other is not null && string.Equals(KsefNumber, other.KsefNumber, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as InvoiceReference);

    public override int GetHashCode() => KsefNumber.ToUpperInvariant().GetHashCode();
}
