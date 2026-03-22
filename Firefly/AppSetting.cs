using Microsoft.Win32;
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Firefly;

internal static class AppSetting
{


    public static string AppVersion { get; private set; }

    public static Guid DeviceId { get; private set; }

    public static Guid SessionId { get; private set; }


    static AppSetting()
    {
        AppVersion = typeof(AppSetting).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        string? systemBiosVersion = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System", "SystemBiosVersion", null) as string;
        string? machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
        DeviceId = new(MD5.HashData(Encoding.UTF8.GetBytes($"{systemBiosVersion}{machineGuid}{Environment.MachineName}")));
        SessionId = Guid.CreateVersion7();
    }




    public static bool Live2dWindowTeachingTipDismissed
    {
        get => GetValue(nameof(Live2dWindowTeachingTipDismissed), 0) is 1;
        set => SetValue(nameof(Live2dWindowTeachingTipDismissed), value ? 1 : 0);
    }






    private static object? GetValue(string key, object? defaultValue = default)
    {
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Firefly", key, defaultValue);
    }


    private static void SetValue(string key, object value)
    {
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Firefly", key, value);
    }


}
