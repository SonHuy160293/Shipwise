using System.Diagnostics;
using Shipwise.Observability;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void DependencyTraceRemovesSensitiveTagsAndRawName()
    {
        using var activity = new Activity("SELECT private_value FROM customer").Start();
        activity.SetTag("db.statement", "private SQL");
        activity.SetTag("url.full", "https://service.test/?token=secret");
        activity.SetTag("http.request.method", "GET");
        activity.TraceStateString = "vendor=private";
        using var processor = new SafeTraceProcessor();
        processor.OnEnd(activity);
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.Equal("Dependency request", activity.DisplayName);
        Assert.Null(activity.TraceStateString);
    }
}
