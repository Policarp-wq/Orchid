namespace Orchid.Ui.Theme;

public partial class Metrics : ResourceDictionary
{
    // Touch targets grow instead of the type shrinking (UI-SPEC "Adaptive behavior").
    private static readonly Dictionary<string, object> Touch = new()
    {
        ["ControlHeight"] = 44d,
        ["IconButtonSize"] = 44d,
        ["PagePadding"] = new Thickness(16),
        ["PanelPadding"] = new Thickness(12),
        ["RulerHeight"] = 32d,
        ["OverviewHeight"] = 56d,
        ["PedalStripHeight"] = 16d,
        ["BeatBlockHeight"] = 44d,
    };

    public Metrics()
    {
        InitializeComponent();
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop) return;
        foreach (var (key, value) in Touch) this[key] = value;
    }
}
