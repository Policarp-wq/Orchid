using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

/// <summary>
/// A device, audio, save or load problem: alert icon, text and an action button on a neutral surface. It is not magenta, which is reserved for
/// "now". The control keeps no state: bind <see cref="Text"/> and <see cref="ActionText"/>, listen to <see cref="ActionInvoked"/>.
/// </summary>
public class StatusBanner : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusBanner), "",
        propertyChanged: (bindable, _, _) => ((StatusBanner)bindable).Update());

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(nameof(ActionText), typeof(string), typeof(StatusBanner), "",
        propertyChanged: (bindable, _, _) => ((StatusBanner)bindable).Update());

    private readonly Label _text = new() { LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2, VerticalOptions = LayoutOptions.Center };
    private readonly Button _action = new() { VerticalOptions = LayoutOptions.Center };

    public StatusBanner()
    {
        var icon = new Image { VerticalOptions = LayoutOptions.Center };
        icon.SetDynamicResource(Image.SourceProperty, "IconAlert");
        _action.Clicked += (_, _) => ActionInvoked?.Invoke(this, EventArgs.Empty);
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 12 };
        grid.Add(icon);
        grid.Add(_text, 1);
        grid.Add(_action, 2);
        var border = new Border { Content = grid };
        border.SetDynamicResource(StyleProperty, "Banner");
        Content = border;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public event EventHandler? ActionInvoked;

    private void Update()
    {
        _text.Text = Text;
        ToolTipProperties.SetText(_text, Text);
        SemanticProperties.SetDescription(this, Text);
        _action.Text = ActionText;
        _action.IsVisible = !string.IsNullOrEmpty(ActionText);
    }
}
