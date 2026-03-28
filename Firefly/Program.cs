using System;
using Vanara.PInvoke;

namespace Firefly;

#if DISABLE_XAML_GENERATED_MAIN
/// <summary>
/// Program class
/// </summary>
public static partial class Program
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2602")]
    //[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.STAThreadAttribute]
    static void Main(string[] args)
    {
        if (Environment.OSVersion.Version < new Version("10.0.17763.0"))
        {
            User32.MessageBox(HWND.NULL, Firefly.Localization.Lang.SystemVersionTip, "Firefly");
            return;
        }

#if !MICROSOFT_WINDOWSAPPSDK_AUTOINITIALIZE_BOOTSTRAP
        WinAppRuntime.AccessWindowsAppSDK();
#endif

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

}
#endif
