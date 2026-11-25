using Microsoft.Extensions.Caching.Memory;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Application.Features.Auth.Commands.RefreshSession;
using Shipwise.Identity.Application.Features.Auth.Commands.SignIn;
using Shipwise.Identity.Application.Features.Auth.Commands.SignOut;
using Shipwise.Identity.Infrastructure.Security;
using Shipwise.SharedKernel.Exceptions;
using Xunit;

namespace Shipwise.Identity.Tests.Sessions;

public sealed class SessionPolicyTests
{
    [Fact]
    public void EmailThrottleExpiresAtFirstFailurePlusFifteenMinutesAndSuccessClearsIt()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new SessionTestClock();
        var throttle = new MemorySignInThrottle(cache, clock);
        const string email = "normalized@example.test";
        throttle.RecordFailure(email);
        clock.Advance(TimeSpan.FromMinutes(14));
        for (int i = 1; i < 10; i++)
        {
            throttle.Check(email);
            throttle.RecordFailure(email);
        }

        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Throws<SignInThrottledException>(() => throttle.Check(email)).RetryAfter);
        throttle.Check("other@example.test");
        clock.Advance(TimeSpan.FromMinutes(1));
        throttle.Check(email);
        for (int i = 0; i < 9; i++)
        {
            throttle.RecordFailure(email);
        }

        throttle.Clear(email);
        throttle.RecordFailure(email);
        throttle.Check(email);
    }

    [Fact]
    public async Task ValidatorUsesTrimmedEmailAndPasswordExactlyAsSent()
    {
        var validator = new SignInCommandValidator();
        await validator.ValidateAsync(new(" not-an-address ", " ", null), default);
        foreach (var command in new[] { new SignInCommand(null, null, null), new("  ", string.Empty, null), new(new string('x', 255), new string('x', 129), null) })
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => validator.ValidateAsync(command, default));
            Assert.Equal(["email", "password"], error.Errors.Select(e => e.PropertyName));
        }
    }

    [Fact]
    public void CommandTextContainsOnlyTypeNames()
    {
        Assert.Equal("SignInCommand", new SignInCommand("address", "password", "token").ToString());
        Assert.Equal("RefreshSessionCommand", new RefreshSessionCommand("token").ToString());
        Assert.Equal("SignOutCommand", new SignOutCommand("token").ToString());
    }
}
