using DynamicWin.Main;

namespace DynamicWin.Tests.Runtime;

public class ApplicationRuntimeTests
{
    [Fact]
    public void Start_starts_components_in_registration_order_and_stops_them_in_reverse_order()
    {
        var calls = new List<string>();
        using var runtime = new ApplicationRuntime(
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
        using var runtime = new ApplicationRuntime(
            new RecordingComponent("settings", calls),
            new RecordingComponent("input", calls, throwsOnStart: true),
            new RecordingComponent("window", calls));

        Assert.Throws<InvalidOperationException>(runtime.Start);

        Assert.Equal(["start:settings", "start:input", "stop:settings"], calls);
    }

    private sealed class RecordingComponent(string name, List<string> calls, bool throwsOnStart = false) : IApplicationComponent
    {
        public void Start()
        {
            calls.Add($"start:{name}");
            if (throwsOnStart)
                throw new InvalidOperationException();
        }

        public void Stop() => calls.Add($"stop:{name}");
    }
}
