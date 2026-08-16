using System.Diagnostics;

namespace DynamicWin.LocalSend;

public static class LocalSendFirewall
{
    public const string InstallArg = "--install-localsend-firewall";
    const string TcpRuleName = "DynamicWin LocalSend (TCP)";
    const string UdpRuleName = "DynamicWin LocalSend (UDP)";

    /// <summary>
    /// Handles the elevated relaunch argument. Must run before the single-instance
    /// mutex is acquired, otherwise the elevated child would exit immediately.
    /// </summary>
    public static bool TryHandleInstallArg()
    {
        if (!Environment.GetCommandLineArgs().Contains(InstallArg)) return false;

        AddRules();
        return true;
    }

    /// <summary>
    /// Makes sure the inbound firewall rules exist. Returns false when the UAC
    /// prompt was declined (or the relaunch failed), so callers can stop asking.
    /// </summary>
    public static bool EnsureRule()
    {
        if (RuleExists()) return true;

        if (IsElevated())
        {
            AddRules();
            return RuleExists();
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe, InstallArg)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return process != null;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
        catch { return false; }
    }

    public static bool RuleExists()
    {
        var tcp = RunNetsh($"advfirewall firewall show rule name=\"{TcpRuleName}\"");
        var udp = RunNetsh($"advfirewall firewall show rule name=\"{UdpRuleName}\"");
        return HasRule(tcp) && HasRule(udp);
    }

    static bool HasRule(string? output) =>
        !string.IsNullOrWhiteSpace(output) && !output.Contains("No rules match");

    static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    static void AddRules()
    {
        RunNetsh($"advfirewall firewall add rule name=\"{TcpRuleName}\" dir=in action=allow protocol=TCP localport={LocalSendProtocol.DefaultHttpPort}");
        RunNetsh($"advfirewall firewall add rule name=\"{UdpRuleName}\" dir=in action=allow protocol=UDP localport={LocalSendProtocol.MulticastPort}");
    }

    static string? RunNetsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });
            if (process == null) return null;
            return process.StandardOutput.ReadToEnd();
        }
        catch { return null; }
    }
}
