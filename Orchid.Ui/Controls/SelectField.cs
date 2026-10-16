using System.Collections;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

public record ItemSelectedEventArgs(object Item);

/// <summary>
/// Replaces a platform picker so it looks the same everywhere: a button showing the selected item and a chevron that opens the items as a
/// list in the host's modal. The control keeps only the selection: bind <see cref="SelectedItem"/>; <see cref="SelectionChanged"/> is raised
/// for a choice the user makes, not for a value set from code.
/// </summary>
public class SelectField : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SelectField), "",
        propertyChanged: (bindable, _, _) => ((SelectField)bindable).UpdateText());

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(nameof(Items), typeof(IEnumerable), typeof(SelectField), null,
        propertyChanged: (bindable, _, _) => ((SelectField)bindable).UpdateText());

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SelectField), null,
        BindingMode.TwoWay, propertyChanged: (bindable, _, _) => ((SelectField)bindable).UpdateText());

    public static readonly BindableProperty ItemTextProperty = BindableProperty.Create(nameof(ItemText), typeof(Func<object, string>), typeof(SelectField), null,
        propertyChanged: (bindable, _, _) => ((SelectField)bindable).UpdateText());

    /// <summary>Shown while nothing is selected.</summary>
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SelectField), "",
        propertyChanged: (bindable, _, _) => ((SelectField)bindable).UpdateText());

    private readonly Button _button = new();

    public SelectField()
    {
        _button.SetDynamicResource(StyleProperty, "SecondaryButton");
        _button.SetDynamicResource(Button.ImageSourceProperty, "IconChevronDown");
        _button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Right, 8);
        _button.LineBreakMode = LineBreakMode.TailTruncation;
        _button.HorizontalOptions = LayoutOptions.Fill;
        _button.Clicked += (_, _) => Open();
        Content = _button;
        UpdateText();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public Func<object, string>? ItemText
    {
        get => (Func<object, string>?)GetValue(ItemTextProperty);
        set => SetValue(ItemTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public event EventHandler<ItemSelectedEventArgs>? SelectionChanged;

    private string TextOf(object item) => ItemText?.Invoke(item) ?? item.ToString() ?? "";

    private void UpdateText()
    {
        var text = SelectedItem is { } item ? TextOf(item) : Placeholder;
        _button.Text = text;
        var described = Display.Format(Strings.FieldValue, Title, text);
        SemanticProperties.SetDescription(_button, described);
        ToolTipProperties.SetText(_button, described);
    }

    private void Open()
    {
        if (Modal.Of(this) is not { } modal || Items is not { } items) return;
        var list = new VerticalStackLayout { Spacing = 8 };
        foreach (var item in items.Cast<object>())
        {
            var choice = new Button { Text = TextOf(item), HorizontalOptions = LayoutOptions.Fill };
            if (Equals(item, SelectedItem)) choice.ImageSource = ThemeResources.Get<ImageSource>("IconCheck");
            choice.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Right, 8);
            choice.Clicked += (_, _) =>
            {
                modal.Dismiss();
                SelectedItem = item;
                SelectionChanged?.Invoke(this, new ItemSelectedEventArgs(item));
            };
            list.Add(choice);
        }
        modal.Present(Title, list, () => _button.Focus());
    }
}
