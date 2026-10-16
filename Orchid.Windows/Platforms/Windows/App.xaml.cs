namespace Orchid.Windows.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        UnhandledException += (_, args) => Program.Log.TryWrite("WinUI", $"{args.Message}\n{args.Exception}");
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
