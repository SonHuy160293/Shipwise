using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shipwise.Identity.Infrastructure.DependencyInjection;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

public sealed class PasswordFileTests
{
    [Theory]
    [InlineData(" value;123 ", " value;123 ")]
    [InlineData("value\";Host=other", "value\";Host=other")]
    [InlineData("value\n", "value")]
    [InlineData("value\r\n", "value")]
    [InlineData("value\n\n", "value\n")]
    public void FileOverridesInlinePasswordAndRemovesOneLineEnding(string contents, string expected)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, contents);
            Assert.Equal(expected, ResolvePassword(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnconfiguredFileKeepsInlinePassword() => Assert.Equal("inline", ResolvePassword(null));

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void EmptyPasswordFailsDuringRegistration(string contents)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, contents);
            Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPersistence(Configuration(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingFileFailsDuringRegistration()
    {
        string missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Assert.Throws<FileNotFoundException>(() => new ServiceCollection().AddPersistence(Configuration(missing)));
    }

    private static string? ResolvePassword(string? path)
    {
        using var provider = new ServiceCollection().AddLogging().AddPersistence(Configuration(path)).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString()).Password;
    }

    private static IConfiguration Configuration(string? path) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Password=inline",
            ["Database:PasswordFile"] = path,
        }).Build();
}
