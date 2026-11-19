using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using miloRPC.Core.Server;
using miloRPC.Core.Shared;

namespace miloRPC.Tests;

[TestFixture]
public class PasswordlessCertificateTests
{
    [SetUp]
    public void SetUp()
    {
        mPfxPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pfx");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(mPfxPath))
            File.Delete(mPfxPath);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Existing_Passwordless_Certificate_Is_Accepted(string? password)
    {
        CreatePfx(mPfxPath, password: null);

        Assert.DoesNotThrow(
            () => new DefaultServerProtocolNegotiation(CreateSettings(mPfxPath, password)));
    }

    [Test]
    public void Existing_Certificate_With_Password_Is_Still_Accepted()
    {
        CreatePfx(mPfxPath, "a-password");

        Assert.DoesNotThrow(
            () => new DefaultServerProtocolNegotiation(CreateSettings(mPfxPath, "a-password")));
    }

    [Test]
    public void Existing_Certificate_With_Wrong_Password_Fails()
    {
        CreatePfx(mPfxPath, "a-password");

        Assert.Throws<InvalidOperationException>(
            () => new DefaultServerProtocolNegotiation(CreateSettings(mPfxPath, null)));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Self_Signed_Generation_Still_Requires_A_Password(string? password)
    {
        Assert.Throws<ArgumentException>(
            () => new DefaultServerProtocolNegotiation(CreateSettings(mPfxPath, password)));

        Assert.That(File.Exists(mPfxPath), Is.False);
    }

    static ConnectionSettings CreateSettings(string pfxPath, string? password)
    {
        return new ConnectionSettings
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                Status = SharedCapabilityEnablement.EnabledMandatory,
                CertificatePath = pfxPath,
                CertificatePassword = password
            }
        };
    }

    // Built with Pkcs12Builder instead of CreateSelfSigned + Export, which
    // needs the private key to go through the OS key store (fails on macOS)
    static void CreatePfx(string path, string? password)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=miloRPC-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using X509Certificate2 publicCert = request.Create(
            request.SubjectName,
            X509SignatureGenerator.CreateForRSA(key, RSASignaturePadding.Pkcs1),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            new byte[] { 1, 2, 3, 4 });

        Pkcs12SafeContents certContents = new();
        certContents.AddCertificate(publicCert);

        Pkcs12SafeContents keyContents = new();
        if (password == null)
            keyContents.AddKeyUnencrypted(key);
        else
            keyContents.AddShroudedKey(
                key,
                password,
                new PbeParameters(
                    PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2048));

        Pkcs12Builder builder = new();
        if (password == null)
        {
            builder.AddSafeContentsUnencrypted(certContents);
            builder.AddSafeContentsUnencrypted(keyContents);
            builder.SealWithoutIntegrity();
        }
        else
        {
            builder.AddSafeContentsUnencrypted(certContents);
            builder.AddSafeContentsUnencrypted(keyContents);
            builder.SealWithMac(password, HashAlgorithmName.SHA256, 2048);
        }

        File.WriteAllBytes(path, builder.Encode());
    }

    string mPfxPath = string.Empty;
}
