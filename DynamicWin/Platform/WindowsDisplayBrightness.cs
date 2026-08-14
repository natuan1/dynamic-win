using System;
using System.Management;

namespace DynamicWin.Platform;

internal sealed class WindowsDisplayBrightness : IDisplayBrightness
{
    private bool isUnsupported;

    public int Get()
    {
        if (isUnsupported) return 100;
        try
        {
            using var managementClass = new ManagementClass("WmiMonitorBrightness") { Scope = new ManagementScope(@"\\.\root\wmi") };
            using var instances = managementClass.GetInstances();
            foreach (ManagementObject instance in instances) return (byte)instance.GetPropertyValue("CurrentBrightness");
        }
        catch (ManagementException) { isUnsupported = true; return 100; }
        return 0;
    }

    public void Set(int brightness)
    {
        try
        {
            using var managementClass = new ManagementClass("WmiMonitorBrightnessMethods") { Scope = new ManagementScope(@"\\.\root\wmi") };
            using var instances = managementClass.GetInstances();
            var arguments = new object[] { 1, brightness };
            foreach (ManagementObject instance in instances) instance.InvokeMethod("WmiSetBrightness", arguments);
        }
        catch (Exception exception) { Console.WriteLine(exception); }
    }
}
