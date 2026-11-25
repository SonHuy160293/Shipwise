using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Shipwise.Identity.Application.Abstractions.Security;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class MemorySignInThrottle(IMemoryCache cache, TimeProvider clock) : ISignInThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly Lock _gate = new();

    public void Check(string normalizedEmail)
    {
        lock (_gate)
        {
            var entry = cache.Get<Failures>(Key(normalizedEmail));
            var now = clock.GetUtcNow();
            if (entry is { Count: >= 10 } && entry.Until > now)
            {
                throw new SignInThrottledException(entry.Until - now);
            }
        }
    }

    public void RecordFailure(string normalizedEmail)
    {
        string key = Key(normalizedEmail);
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            var entry = cache.Get<Failures>(key);
            var failures = entry is null || entry.Until <= now ? new Failures(now + Window) : entry;
            failures.Count++;
            cache.Set(key, failures, failures.Until - now);
        }
    }

    public void Clear(string normalizedEmail)
    {
        lock (_gate)
        {
            cache.Remove(Key(normalizedEmail));
        }
    }

    private static string Key(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)));

    private sealed class Failures(DateTimeOffset until)
    {
        public int Count { get; set; }
        public DateTimeOffset Until { get; } = until;
    }
}
