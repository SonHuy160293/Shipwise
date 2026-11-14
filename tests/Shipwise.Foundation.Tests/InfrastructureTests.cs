using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Shipwise.EntityFrameworkCore.Reliability;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class InfrastructureTests
{
    [PostgreSqlFact]
    public async Task PostgreSqlRollbackDoesNotPersistOutboxMessage()
    {
        var options = new DbContextOptionsBuilder<ReliabilityContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SHIPWISE_TEST_POSTGRES")).Options;
        await using var db = new ReliabilityContext(options);
        await db.Database.EnsureCreatedAsync();
        var message = OutboxMessage.Create(Guid.NewGuid(), "sample.v1", 1, Guid.NewGuid(), 1, "{}", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Add(message);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        db.ChangeTracker.Clear();
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(row => row.MessageId == message.MessageId));
    }

    [RabbitMqFact]
    public async Task RabbitMqRoundTripsThroughIsolatedQueue()
    {
        var factory = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("SHIPWISE_TEST_RABBITMQ")!) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queue = await channel.QueueDeclareAsync();
        byte[] payload = "shipwise-probe"u8.ToArray();
        await channel.BasicPublishAsync(string.Empty, queue.QueueName, payload);
        var result = await channel.BasicGetAsync(queue.QueueName, true);
        Assert.NotNull(result);
        Assert.Equal(payload, result.Body.ToArray());
    }

    private sealed class ReliabilityContext(DbContextOptions<ReliabilityContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyReliabilityConfigurations(ReliabilityDatabaseProvider.PostgreSql);
    }
}
