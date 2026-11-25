using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

public sealed class SessionMigrationTests(PostgreSqlDatabaseFixture database) : IClassFixture<PostgreSqlDatabaseFixture>
{
    [PostgreSqlFact]
    public async Task MigrationPersistsSessionsAndEnforcesRevocationAndUserRelationship()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(
            database.ConnectionString,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));
        await using var db = new IdentityDbContext(options.Options);
        await db.Database.MigrateAsync();
        var user = User.Create("migration@example.test", "Session");
        var session = Session.Start(user.Id, new string('a', 64), DateTimeOffset.UtcNow, TimeSpan.FromDays(7), TimeSpan.FromDays(30));
        db.Add(user);
        db.Add(session);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(user.Id, (await db.Set<Session>().SingleAsync()).UserId);
        Assert.Equal(1, await database.ScalarAsync("""SELECT count(*) FROM pg_indexes WHERE schemaname = 'identity' AND indexname = 'IX_Sessions_UserId'"""));
        async Task CheckAsync(FormattableString sql, string constraint)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(sql));
            Assert.Equal(constraint, error.ConstraintName);
        }

        await CheckAsync($"""UPDATE identity."Sessions" SET "UserId" = {Guid.NewGuid()} WHERE "Id" = {session.Id}""", "FK_Sessions_Users_UserId");
        await CheckAsync($"""UPDATE identity."Sessions" SET "RevokedAt" = now() WHERE "Id" = {session.Id}""", "CK_Sessions_Revocation");
        await CheckAsync($"""UPDATE identity."Sessions" SET "RevokedAt" = now(), "RevocationReason" = 7 WHERE "Id" = {session.Id}""", "CK_Sessions_RevocationReason");
        await db.Database.ExecuteSqlAsync($"""DELETE FROM identity."Users" WHERE "Id" = {user.Id}""");
        Assert.False(await db.Set<Session>().AnyAsync());
    }
}
