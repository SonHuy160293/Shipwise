namespace Shipwise.Identity.Domain.Models;

public enum SessionRevocationReason
{
    SignedOut = 1,
    ReuseDetected = 2,
    Replaced = 3,
}
