using System.Globalization;

namespace Shipwise.Messaging.Outbox;

public sealed class WorkerHeartbeat
{
    private readonly string _path;

    public WorkerHeartbeat(string? path = null)
    {
        _path = path
            ?? Environment.GetEnvironmentVariable("SHIPWISE_WORKER_HEARTBEAT_FILE")
            ?? Path.Combine(Path.GetTempPath(), "shipwise-worker.heartbeat");
    }

    public async Task CompleteScanAsync(CancellationToken cancellationToken)
    {
        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            await File.WriteAllTextAsync(temporaryPath, timestamp, cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
