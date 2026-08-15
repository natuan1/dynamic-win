using DynamicWin.Main;

namespace DynamicWin.Tests.Runtime;

public class ApplicationLifetimeTests
{
    [Fact]
    public void Start_starts_components_in_registration_order_and_stops_them_in_reverse_order()
    {
        var calls = new List<string>();
        using var runtime = new ApplicationLifetime(
            new RecordingComponent("settings", calls),
            new RecordingComponent("input", calls),
            new RecordingComponent("window", calls));

        runtime.Start();

        Assert.Equal(["start:settings", "start:input", "start:window"], calls);
    }

    [Fact]
    public void Dispose_stops_only_started_components_when_startup_fails()
    {
        var calls = new List<string>();
        using var runtime = new ApplicationLifetime(
            new RecordingComponent("settings", calls),
            new RecordingComponent("input", calls, throwsOnStart: true),
            new RecordingComponent("window", calls));

        Assert.Throws<InvalidOperationException>(runtime.Start);

        Assert.Equal(["start:settings", "start:input", "stop:settings"], calls);
    }

    [Fact]
    public void Dispose_stops_every_started_component_when_one_stop_fails()
    {
        var calls = new List<string>();
        var runtime = new ApplicationLifetime(
            new RecordingComponent("settings", calls),
            new RecordingComponent("input", calls, throwsOnStop: true),
            new RecordingComponent("window", calls));
        runtime.Start();

        Assert.Throws<AggregateException>(runtime.Dispose);

        Assert.Equal(
            ["start:settings", "start:input", "start:window", "stop:window", "stop:input", "stop:settings"],
            calls);

        runtime.Dispose();
        Assert.Equal(6, calls.Count);
    }

    [Fact]
    public void Start_preserves_startup_and_cleanup_failures()
    {
        var runtime = new ApplicationLifetime(
            new RecordingComponent("settings", [], throwsOnStop: true),
            new RecordingComponent("input", [], throwsOnStart: true));

        var exception = Assert.Throws<AggregateException>(runtime.Start);

        Assert.Equal(2, exception.InnerExceptions.Count);
    }

    private sealed class RecordingComponent(string name, List<string> calls, bool throwsOnStart = false, bool throwsOnStop = false) : IApplicationComponent
    {
        public void Start()
        {
            calls.Add($"start:{name}");
            if (throwsOnStart)
                throw new InvalidOperationException();
        }

        public void Stop()
        {
            calls.Add($"stop:{name}");
            if (throwsOnStop)
                throw new InvalidOperationException();
        }
    }
}
