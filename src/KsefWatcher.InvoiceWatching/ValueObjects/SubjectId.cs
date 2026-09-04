namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// Identity of the <c>SubjectWatch</c> aggregate (docs/08_invoice_watching_value_objects.md).
/// Also the key for poll-offset derivation (A9) and the per-subject KSeF rate budget (I-21).
/// </summary>
public sealed record SubjectId
{
    private static readonly int[] ChecksumWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];

    public string Nip { get; }

    public SubjectId(string nip)
    {
        if (string.IsNullOrWhiteSpace(nip))
        {
            throw new ArgumentException("NIP must not be empty.", nameof(nip));
        }

        if (!HasValidChecksum(nip))
        {
            throw new ArgumentException("NIP has an invalid checksum (I-13).", nameof(nip));
        }

        Nip = nip;
    }

    private static bool HasValidChecksum(string nip)
    {
        if (nip.Length != 10 || !nip.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < ChecksumWeights.Length; i++)
        {
            sum += (nip[i] - '0') * ChecksumWeights[i];
        }

        var checkDigit = sum % 11;
        return checkDigit != 10 && checkDigit == nip[9] - '0';
    }
}
