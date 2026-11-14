using System.Security.Cryptography;
using Shipwise.EntityFrameworkCore.Reliability;
using Shipwise.Messaging.Security;
using Shipwise.SharedKernel.Exceptions;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class ReliabilityTests
{
    [Fact]
    public void EncryptionBindsMetadataAndRandomizesKeys()
    {
        using var rsa = RSA.Create(3072);
        byte[] plain = "private payload"u8.ToArray();
        byte[] metadata = "sample.v1"u8.ToArray();
        var first = EnvelopeEncryption.Encrypt(plain, metadata, rsa.ExportSubjectPublicKeyInfoPem());
        var second = EnvelopeEncryption.Encrypt(plain, metadata, rsa.ExportSubjectPublicKeyInfoPem());
        Assert.NotEqual(first.WrappedKey, second.WrappedKey);
        Assert.Equal(plain, EnvelopeEncryption.Decrypt(first, metadata, rsa.ExportPkcs8PrivateKeyPem()));
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(first, "wrong"u8.ToArray(), rsa.ExportPkcs8PrivateKeyPem()));
        Assert.DoesNotContain(first.Ciphertext, first.ToString());
    }

    [Fact]
    public void ExpiredLeaseCannotPublishAndRecoveryKeepsMessageIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create(Guid.NewGuid(), "sample.v1", 1, Guid.NewGuid(), 1, "{}", Guid.NewGuid(), null, now);
        var identity = message.MessageId;
        var oldLease = Guid.NewGuid();
        message.Claim(oldLease, now, now.AddSeconds(1));
        Assert.Throws<ValidationException>(() => message.MarkPublished(oldLease, now.AddSeconds(2)));
        var newLease = Guid.NewGuid();
        message.Claim(newLease, now.AddSeconds(2), now.AddMinutes(1));
        Assert.Throws<ValidationException>(() => message.MarkPublished(oldLease, now.AddSeconds(3)));
        message.MarkPublished(newLease, now.AddSeconds(3));
        message.RequeuePublished(now.AddSeconds(4));
        Assert.Equal(identity, message.MessageId);
        Assert.Equal(OutboxStatus.Pending, message.Status);
    }
}
