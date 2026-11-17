using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.Identity.Tests.Persistence;
using Xunit;

namespace Shipwise.Identity.Tests.Registration;

public sealed class RegistrationDatabaseTests(PostgreSqlDatabaseFixture database) : IClassFixture<PostgreSqlDatabaseFixture>
{
    [PostgreSqlFact]
    public async Task RegistrationPersistsHashAndHandlesDuplicatesAndRaces()
    {
        await using var factory = new RegistrationHostFactory(database.ConnectionString);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        }

        using var client = factory.CreateClient();
        const string password = "test-password-123";
        var first = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = " Ana@Example.com ", displayName = " Ana ", password });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(first.Headers.Contains("Set-Cookie"));
        var responseBody = (await first.Content.ReadFromJsonAsync<JsonObject>())!;
        var responseData = responseBody["data"] as JsonObject;
        Assert.NotNull(responseData);
        Assert.Single(responseData);
        Assert.True(Guid.TryParse(responseData["userId"]?.GetValue<string>(), out Guid returnedUserId));
        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "ana@example.com", displayName = "Ana", password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = (await duplicate.Content.ReadFromJsonAsync<JsonObject>())!;
        JsonNode duplicateError = duplicateBody["error"] !;
        Assert.Equal("Registration.EmailTaken", duplicateError["code"] !.GetValue<string>());
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/v1/auth/register", new { email = "race@example.com", displayName = "Race", password })));
        Assert.Equal(1, concurrent.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, concurrent.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Set<User>().SingleAsync(item => item.NormalizedEmail == "ana@example.com");
        Assert.Equal(returnedUserId, user.Id);
        var credential = await db.Set<PasswordCredential>().SingleAsync(item => item.UserId == user.Id);
        Assert.Equal("Ana@Example.com", user.Email);
        Assert.Equal("Ana", user.DisplayName);
        Assert.NotEqual(password, credential.PasswordHash);
        string serialized = responseBody.ToJsonString();
        Assert.DoesNotContain(password, serialized);
        Assert.DoesNotContain(credential.PasswordHash, serialized);
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<User>().VerifyHashedPassword(user, credential.PasswordHash, password));
        Assert.Equal(2, await db.Set<User>().CountAsync());
        Assert.Equal(2, await db.Set<PasswordCredential>().CountAsync());

        db.Add(User.Create("ANA@example.com", "Duplicate"));
        var duplicateUser = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_Users_NormalizedEmail", Assert.IsType<PostgresException>(duplicateUser.InnerException).ConstraintName);
        db.ChangeTracker.Clear();
        db.Add(PasswordCredential.Create(user.Id, credential.PasswordHash));
        var duplicateCredential = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_PasswordCredentials_UserId", Assert.IsType<PostgresException>(duplicateCredential.InnerException).ConstraintName);
    }
}
