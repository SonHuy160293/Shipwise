using System.ComponentModel;
using System.Diagnostics;

namespace Shipwise.Identity.Tests.Persistence;

public sealed class DockerPostgreSqlFactAttribute : PostgreSqlFactAttribute
{
    private static readonly Lazy<bool> _dockerAvailable = new(CheckDocker);

    public DockerPostgreSqlFactAttribute()
    {
        if (Skip is null && !_dockerAvailable.Value)
        {
            Skip = "Bootstrap role tests require a reachable Docker engine. 'docker version' must succeed within 5 seconds.";
        }
    }

    private static bool CheckDocker()
    {
        var start = new ProcessStartInfo("docker", "version")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                return process.ExitCode == 0;
            }

            process.Kill(entireProcessTree: true);
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // The CLI can exit between the timeout and the kill.
            return false;
        }
    }
}
