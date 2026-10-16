using Orchid.Ui.Theme;

namespace Orchid.Ui.Views;

public partial class LiveView : ContentView
{
    public LiveView()
    {
        InitializeComponent();
        // A centered column of bounded width: it shrinks with a narrow window and stops growing at LiveMaxWidth.
        SizeChanged += (_, _) => Column.WidthRequest = Math.Max(0, Math.Min(ThemeResources.Get<double>("LiveMaxWidth"), Width));
    }
}
