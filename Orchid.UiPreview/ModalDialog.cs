namespace Orchid.UiPreview;

/// <summary>
/// App-agnostic modal: a dimmed scrim with a centered card holding a title, a close button and any content.
/// Put it as the last child of a full-window Grid so it overlays the page. It knows nothing about what it hosts, so the same
/// settings content can move to another screen or device. Platform-neutral contract: docs/orchid-design/UI-SPEC.md, "Shared components".
/// </summary>
[ContentProperty(nameof(Body))]
public class ModalDialog : ContentView
{
    private const double MaxCardWidth = 560, ScreenMargin = 24;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title),
        typeof(string), typeof(ModalDialog), string.Empty);

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen),
        typeof(bool), typeof(ModalDialog), false,
        propertyChanged: (bindable, _, open) => ((ModalDialog)bindable).OnOpenChanged((bool)open));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public View? Body
    {
        get => _body.Content;
        set => _body.Content = value;
    }

    /// <summary>Raised after the dialog closes, whether by the close button, the scrim or code.</summary>
    public event EventHandler? Closed;

    private readonly ContentView _body = new();
    private readonly Border _card = new() { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

    public ModalDialog()
    {
        IsVisible = false;
        var scrim = new BoxView { Color = Colors.Black, Opacity = .5 };
        scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Close) });

        var title = new Label { FontSize = ThemeResources.Get<double>("FontTitle"), VerticalOptions = LayoutOptions.Center };
        title.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        var close = new Button { Style = ThemeResources.Get<Style>("IconButton"), ImageSource = ThemeResources.Get<ImageSource>("IconClose") };
        close.Clicked += (_, _) => Close();
        SemanticProperties.SetDescription(close, "Close");
        ToolTipProperties.SetText(close, "Close");

        var header = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
        header.Add(title);
        header.Add(close, 1);
        _card.Content = new VerticalStackLayout { Spacing = ThemeResources.Get<double>("SpaceL"), Children = { header, _body } };

        SizeChanged += (_, _) => _card.WidthRequest = Math.Max(0, Math.Min(MaxCardWidth, Width - 2 * ScreenMargin));
        Content = new Grid { Children = { scrim, _card } };
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    private void OnOpenChanged(bool open)
    {
        IsVisible = open;
        if (!open) Closed?.Invoke(this, EventArgs.Empty);
    }
}
