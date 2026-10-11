using Microsoft.UI.Dispatching;

namespace Orchid.UiPreview.WinUI;

internal static class Program
{
    internal static readonly CrashLog Log = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orchid", "UiPreview", "Logs"));

    [STAThread]
    private static int Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.TryWrite($"AppDomain (terminating: {args.IsTerminating})", args.ExceptionObject.ToString() ?? "Unknown exception");
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Log.TryWrite("UnobservedTaskException", args.Exception.ToString());

        try
        {
            Log.TryWrite("Startup", $"Runtime: {Environment.Version}\nOS: {Environment.OSVersion}\nApp: {AppContext.BaseDirectory}");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                new App();
            });
            Log.TryWrite("Shutdown", "Window closed normally.");
            return 0;
        }
        catch (Exception exception)
        {
            Log.TryWrite("Main", exception.ToString());
            return 1;
        }
    }
}
