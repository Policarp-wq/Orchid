namespace Orchid.Ui.Views;

public partial class MomentPanel : ContentView
{
    /// <summary>A one-line row for the space below the timeline instead of the full panel beside it.</summary>
    public static readonly BindableProperty IsCompactProperty = BindableProperty.Create(nameof(IsCompact), typeof(bool), typeof(MomentPanel), false,
        propertyChanged: (bindable, _, compact) =>
        {
            var panel = (MomentPanel)bindable;
            panel.Full.IsVisible = !(bool)compact;
            panel.Row.IsVisible = (bool)compact;
        });

    public MomentPanel() => InitializeComponent();

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
}
