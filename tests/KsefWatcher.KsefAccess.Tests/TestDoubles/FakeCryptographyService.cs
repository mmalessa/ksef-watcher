using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Certificates;
using KSeF.Client.Core.Models.Sessions;

namespace KsefWatcher.KsefAccess.Tests.TestDoubles;

/// <summary>
/// Never actually exercised in KsefClientAdapter tests — <see cref="FakeAuthCoordinator"/> replaces
/// the real AuthCoordinator that would otherwise call into this. Exists only to satisfy the
/// constructor dependency.
/// </summary>
public sealed class FakeCryptographyService : ICryptographyService
{
    private static NotSupportedException NotNeeded() => new("Not exercised — real AuthCoordinator is faked out in these tests.");

    public bool IsWarmedUp() => throw NotNeeded();
    public EncryptionData GetEncryptionData() => throw NotNeeded();
    public byte[] EncryptBytesWithAES256(byte[] content, byte[] key, byte[] iv) => throw NotNeeded();
    public void EncryptStreamWithAES256(Stream input, Stream output, byte[] key, byte[] iv) => throw NotNeeded();
    public Task EncryptStreamWithAES256Async(Stream input, Stream output, byte[] key, byte[] iv, CancellationToken cancellationToken = default) => throw NotNeeded();
    public byte[] DecryptBytesWithAES256(byte[] content, byte[] key, byte[] iv) => throw NotNeeded();
    public void DecryptStreamWithAES256(Stream input, Stream output, byte[] key, byte[] iv) => throw NotNeeded();
    public Task DecryptStreamWithAES256Async(Stream input, Stream output, byte[] key, byte[] iv, CancellationToken cancellationToken = default) => throw NotNeeded();
    public (string, string) GenerateCsrWithRsa(CertificateEnrollmentsInfoResponse certificateInfo, RSASignaturePadding? padding = null) => throw NotNeeded();
    public (string, string) GenerateCsrWithEcdsa(CertificateEnrollmentsInfoResponse certificateInfo) => throw NotNeeded();
    public FileMetadata GetMetaData(byte[] file) => throw NotNeeded();
    public FileMetadata GetMetaData(Stream fileStream) => throw NotNeeded();
    public Task<FileMetadata> GetMetaDataAsync(Stream fileStream, CancellationToken cancellationToken = default) => throw NotNeeded();
    public byte[] EncryptWithRSAUsingPublicKey(byte[] content, RSAEncryptionPadding padding) => throw NotNeeded();
    public byte[] EncryptKsefTokenWithRSAUsingPublicKey(byte[] content) => throw NotNeeded();
    public byte[] EncryptWithECDSAUsingPublicKey(byte[] content) => throw NotNeeded();
    public Task WarmupAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task ForceRefreshAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public void SetExternalMaterials(X509Certificate2 symmetricKeyCert, X509Certificate2 ksefTokenCert, string? symmetricKeyPublicKeyId = null, string? ksefTokenPublicKeyId = null) => throw NotNeeded();
    public X509Certificate2 SymmetricKeyCertificate => throw NotNeeded();
    public X509Certificate2 KsefTokenCertificate => throw NotNeeded();
    public string? SymmetricKeyPublicKeyId => throw NotNeeded();
    public string? KsefTokenPublicKeyId => throw NotNeeded();
}
