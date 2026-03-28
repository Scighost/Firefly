using Firefly.Localization;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vanara.Extensions;
using Vanara.PInvoke;

namespace Firefly;

internal partial class WinAppRuntime
{

    private const double MB = 1 << 20;

    private const string Url = "https://aka.ms/windowsappsdk/1.8/1.8.260317003/windowsappruntimeinstall-x64.exe";


    private const uint MajorMinorVersion = 0x00010008;

    private const ulong MinVersion = 0x1F40032608CC0000;

    internal static void AccessWindowsAppSDK()
    {
        var minVersion = new PackageVersion(MinVersion);
        var options = Bootstrap.InitializeOptions.None;
        if (!Bootstrap.TryInitialize(MajorMinorVersion, "", minVersion, options, out int hr))
        {
            new WinAppRuntime().InstallAsync().GetAwaiter().GetResult();
            Environment.Exit(hr);
        }
    }


    public long TotalBytes { get; set; }

    public long DownloadBytes { get; set; }

    public bool InstallSuccess { get; set; }


    private Task downloadTask;


    public async Task InstallAsync()
    {
        try
        {
            downloadTask = DownloadAndInstallAsync();

            int result = CreateDialog();
            if (result == 1001)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Url,
                    UseShellExecute = true,
                });
            }
            else if (result == 2)
            {
                return;
            }
            else
            {
                Process.Start(Environment.ProcessPath!);
            }
        }
        catch { }
    }



    private async Task DownloadAndInstallAsync()
    {
        try
        {
            string file = Path.GetTempFileName() + ".exe";

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using var fs = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    using var client = new HttpClient();
                    var response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    TotalBytes = response.Content.Headers.ContentLength ?? 0;
                    DownloadBytes = 0;
                    using var hs = await response.Content.ReadAsStreamAsync();
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = await hs.ReadAsync(buffer)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read));
                        DownloadBytes += read;
                    }
                    break;
                }
                catch (Exception)
                {
                    if (i >= 2)
                    {
                        throw;
                    }
                }
            }

            var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true,
                Verb = "runas",
            });
            await p!.WaitForExitAsync();

            InstallSuccess = p.ExitCode is 0;
        }
        catch { }
    }




    private int CreateDialog()
    {
        var button = new ComCtl32.TASKDIALOG_BUTTON { nButtonID = 1001, pszButtonText = StringHelper.AllocString(Lang.DownloadManually) };
        GCHandle handle = GCHandle.Alloc(button, GCHandleType.Pinned);
        using var config = new ComCtl32.TASKDIALOGCONFIG()
        {
            hwndParent = IntPtr.Zero,
            dwFlags = ComCtl32.TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN
                    | ComCtl32.TASKDIALOG_FLAGS.TDF_SHOW_PROGRESS_BAR
                    | ComCtl32.TASKDIALOG_FLAGS.TDF_CALLBACK_TIMER,
            dwCommonButtons = ComCtl32.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON,
            WindowTitle = "Firefly",
            MainInstruction = Lang.InstallRequiredComponents,
            Content = Lang.DownloadingWindowsAppRuntime + "\n",
            mainIcon = User32.LoadIcon(Kernel32.GetModuleHandle(), "#32512").DangerousGetHandle(),
            cButtons = 1,
            pButtons = handle.AddrOfPinnedObject(),
            nDefaultButton = 1001,
            pfCallbackProc = DialogCallback,
        };

        ComCtl32.TaskDialogIndirect(config, out int result, out _, out _);
        return result;
    }





    public HRESULT DialogCallback(HWND hwnd, ComCtl32.TaskDialogNotification msg, IntPtr wParam, IntPtr lParam, IntPtr refData)
    {
        switch (msg)
        {
            case ComCtl32.TaskDialogNotification.TDN_TIMER:
                if (downloadTask.IsCompleted)
                {
                    if (InstallSuccess)
                    {
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_CLICK_BUTTON, (IntPtr)1, IntPtr.Zero);
                    }
                    else
                    {
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_SET_ELEMENT_TEXT, (IntPtr)ComCtl32.TASKDIALOG_ELEMENTS.TDE_CONTENT, Lang.DownloadOrInstallationFailed + "\n");
                    }
                }
                else
                {
                    if (TotalBytes > 0)
                    {
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_SET_PROGRESS_BAR_POS, (IntPtr)(DownloadBytes * 100 / TotalBytes), IntPtr.Zero);
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_SET_ELEMENT_TEXT, (IntPtr)ComCtl32.TASKDIALOG_ELEMENTS.TDE_CONTENT, $"{Lang.DownloadingWindowsAppRuntime}\n{DownloadBytes / MB:F2}/{TotalBytes / MB:F2} MB");
                    }
                    else if (DownloadBytes > 0)
                    {
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_SET_ELEMENT_TEXT, (IntPtr)ComCtl32.TASKDIALOG_ELEMENTS.TDE_CONTENT, $"{Lang.DownloadingWindowsAppRuntime}\n{DownloadBytes / MB:F2} MB");
                    }
                    else
                    {
                        User32.SendMessage(hwnd, ComCtl32.TaskDialogMessage.TDM_SET_ELEMENT_TEXT, (IntPtr)ComCtl32.TASKDIALOG_ELEMENTS.TDE_CONTENT, $"{Lang.DownloadingWindowsAppRuntime}\n");
                    }
                }
                break;
            default:
                break;
        }
        return 0;
    }



}
