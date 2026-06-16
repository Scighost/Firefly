using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vanara.PInvoke;


namespace Firefly;


[JsonSerializable(typeof(List<Live2dWindowState>))]
internal partial class Live2dWindowStateJsonContext : JsonSerializerContext { }


internal class Live2dWindowState
{

    public string Id { get; set; } = "";

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Borderless { get; set; }

    public bool Flipped { get; set; }

    public double Opacity { get; set; } = 1.0;

    public string MonitorDevice { get; set; } = "";


    private const string RegistryKey = @"HKEY_CURRENT_USER\Software\Firefly";
    private const string RegistryValue = "Live2dWindows";

    private static DispatcherQueueTimer? _saveTimer;
    private static Live2dWindow? _saveWindow;


    public static void Save(Live2dWindow window)
    {
        try
        {
            nint hwnd = (nint)window.AppWindow.Id.Value;
            User32.GetWindowRect(hwnd, out var rect);
            string monitorDevice = GetMonitorDeviceName(hwnd);

            var state = new Live2dWindowState
            {
                Id = window.WindowId,
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Borderless = window.Borderless,
                Flipped = window.ViewFlipped,
                Opacity = window.PanelOpacity,
                MonitorDevice = monitorDevice,
            };

            var list = LoadAllInternal();
            int index = list.FindIndex(s => s.Id == window.WindowId);
            if (index >= 0)
                list[index] = state;
            else
                list.Add(state);
            Registry.SetValue(RegistryKey, RegistryValue, JsonSerializer.Serialize(list, Live2dWindowStateJsonContext.Default.ListLive2dWindowState));
        }
        catch
        {
        }
    }


    public static void SaveDebounced(DispatcherQueue dispatcherQueue, Live2dWindow window)
    {
        _saveWindow = window;
        _saveTimer?.Stop();
        if (_saveTimer is null)
        {
            _saveTimer = dispatcherQueue.CreateTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                if (_saveWindow is not null)
                    Save(_saveWindow);
            };
        }
        _saveTimer.Start();
    }


    public static void CancelPendingSave()
    {
        _saveTimer?.Stop();
    }


    public static void SaveAll(IEnumerable<Live2DWindowInfo> windows)
    {
        _saveTimer?.Stop();
        try
        {
            var list = new List<Live2dWindowState>();
            foreach (var info in windows)
            {
                if (info.Window.AppWindow is null)
                    continue;
                nint hwnd = (nint)info.Window.AppWindow.Id.Value;
                User32.GetWindowRect(hwnd, out var rect);
                string monitorDevice = GetMonitorDeviceName(hwnd);
                list.Add(new Live2dWindowState
                {
                    Id = info.Id,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    Borderless = info.Window.Borderless,
                    Flipped = info.Window.ViewFlipped,
                    Opacity = info.Window.PanelOpacity,
                    MonitorDevice = monitorDevice,
                });
            }
            if (list.Count > 0)
                Registry.SetValue(RegistryKey, RegistryValue, JsonSerializer.Serialize(list, Live2dWindowStateJsonContext.Default.ListLive2dWindowState));
            else
                DeleteAll();
        }
        catch
        {
        }
    }


    public static List<Live2dWindowState> LoadAll()
    {
        return LoadAllInternal();
    }


    public static void DeleteAll()
    {
        _saveTimer?.Stop();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Firefly", true);
            key?.DeleteValue(RegistryValue, false);
        }
        catch
        {
        }
    }


    public static void DeleteById(string id)
    {
        try
        {
            var list = LoadAllInternal();
            int removed = list.RemoveAll(s => s.Id == id);
            if (removed > 0)
            {
                if (list.Count > 0)
                    Registry.SetValue(RegistryKey, RegistryValue, JsonSerializer.Serialize(list, Live2dWindowStateJsonContext.Default.ListLive2dWindowState));
                else
                    DeleteAll();
            }
        }
        catch
        {
        }
    }


    private static List<Live2dWindowState> LoadAllInternal()
    {
        try
        {
            if (Registry.GetValue(RegistryKey, RegistryValue, null) is string json)
            {
                return JsonSerializer.Deserialize(json, Live2dWindowStateJsonContext.Default.ListLive2dWindowState) ?? [];
            }
        }
        catch
        {
        }
        return [];
    }


    private static string GetMonitorDeviceName(nint hwnd)
    {
        HMONITOR hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
            return "";

        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(hMonitor, ref info))
            return "";

        return info.szDevice ?? "";
    }


    public static bool IsMonitorAvailable(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
            return false;

        var dev = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref dev, 0); i++)
        {
            if (string.Equals(dev.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)
                && (dev.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
            {
                return true;
            }
        }
        return false;
    }



    #region Win32 Native


    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }


    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern HMONITOR MonitorFromWindow(HWND hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(HMONITOR hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);


    #endregion

}
