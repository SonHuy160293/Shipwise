using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHIPWISE_TEST_RABBITMQ")))
        {
            Skip = "Set SHIPWISE_TEST_RABBITMQ to an isolated RabbitMQ test broker URI.";
        }
    }
}
