using Microsoft.Maui.Controls.Shapes;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

/// <summary>
/// App-agnostic modal: a dimmed scrim with one card holding a title, a close button and any content. Up to 640 px wide it is a sheet attached
/// to the bottom edge. Put it as the last child of a full-window Grid so it overlays the page. It knows nothing about what it hosts.
/// Platform-neutral contract: docs/orchid-design/UI-SPEC.md, "Shared components". Escape and the focus trap are in the Windows partial.
/// </summary>
[ContentProperty(nameof(Body))]
public partial class ModalDialog : ContentView
{
    private const double SheetMaxWidth = 640, ScreenMargin = 24;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title),
        typeof(string), typeof(ModalDialog), string.Empty);

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen),
        typeof(bool), typeof(ModalDialog), false,
        propertyChanged: (bindable, _, open) => ((ModalDialog)bindable).OnOpenChanged((bool)open));

    private readonly ContentView _body = new();
    private readonly Border _card = new();
    private readonly Button _close = new();

    public ModalDialog()
    {
        IsVisible = false;
        var scrim = new BoxView { Color = Colors.Black, Opacity = .5 };
        scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Close) });

        var title = new Label { VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        title.SetDynamicResource(StyleProperty, "TitleLabel");
        title.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        _close.SetDynamicResource(StyleProperty, "IconButton");
        _close.SetDynamicResource(Button.ImageSourceProperty, "IconClose");
        _close.Clicked += (_, _) => Close();
        SemanticProperties.SetDescription(_close, Strings.Close);
        ToolTipProperties.SetText(_close, Strings.Close);

        var header = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 12 };
        header.Add(title);
        header.Add(_close, 1);
        var content = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)], RowSpacing = 16 };
        content.Add(header);
        content.Add(new ScrollView { Content = _body }, 0, 1);
        _card.Content = content;

        SizeChanged += (_, _) => Arrange();
        Content = new Grid { Children = { scrim, _card } };
    }

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

    /// <summary>Raised after the dialog closes, whether by the close button, the scrim, Escape or code.</summary>
    public event EventHandler? Closed;

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    partial void OnOpenedPlatform();

    partial void OnClosedPlatform();

    private void OnOpenChanged(bool open)
    {
        IsVisible = open;
        if (open)
        {
            Arrange();
            // Focus can move only once the card is visible and laid out.
            Dispatcher.Dispatch(() => OnOpenedPlatform());
            return;
        }
        OnClosedPlatform();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Arrange()
    {
        if (Width <= 0) return;
        var sheet = Width <= SheetMaxWidth;
        _card.HorizontalOptions = sheet ? LayoutOptions.Fill : LayoutOptions.Center;
        _card.VerticalOptions = sheet ? LayoutOptions.End : LayoutOptions.Center;
        _card.WidthRequest = sheet ? -1 : Math.Min(ThemeResources.Get<double>("ModalMaxWidth"), Width - 2 * ScreenMargin);
        _card.Margin = sheet ? new Thickness(0) : new Thickness(ScreenMargin);
        _card.MaximumHeightRequest = Math.Max(0, Height - ScreenMargin * 2);
        _card.StrokeShape = new RoundRectangle { CornerRadius = sheet ? new CornerRadius(12, 12, 0, 0) : new CornerRadius(8) };
    }
}
