using System.Text.Json;
using Xunit;

namespace Shipwise.Identity.Tests.Registration;

public sealed class OpenApiContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    public async Task ServedContractMatchesCommittedDocument()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        string actual = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(actual);
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty("/api/v1/auth/register").GetProperty("post");
        Assert.Empty(operation.GetProperty("security").EnumerateArray());
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Contains(schemas.GetProperty("ApiSuccessResponseOfRegisterUserResponse").GetProperty("required").EnumerateArray(), property => property.GetString() == "data");
        Assert.Contains(schemas.GetProperty("RegisterUserRequest").GetProperty("required").EnumerateArray(), property => property.GetString() == "password");
        string formatted = JsonSerializer.Serialize(document.RootElement, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string path = Path.Combine(root, "contracts", "openapi", "shipwise.v1.json");
        if (Environment.GetEnvironmentVariable("SHIPWISE_UPDATE_CONTRACT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, formatted);
        }

        Assert.True(File.Exists(path), "Set SHIPWISE_UPDATE_CONTRACT=1 and rerun this test to generate the contract.");
        Assert.Equal((await File.ReadAllTextAsync(path)).Replace("\r\n", "\n", StringComparison.Ordinal), formatted);
    }
}
