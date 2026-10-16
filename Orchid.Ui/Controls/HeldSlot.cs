using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

/// <summary>One held key as a chip: its note name, and in review its MIDI number, attack and duration.</summary>
public sealed record HeldChip(string Name, string? Details);

/// <summary>
/// The keys held at a moment: a chord name when a recognizer supplies one, and chips lowest first. The slot has a fixed height with an inner
/// scroll, so 0 to 10 notes, chord names and sustained notes never change its size. It keeps no data: bind the properties.
/// </summary>
public class HeldSlot : ContentView
{
    public static readonly BindableProperty NotesProperty = BindableProperty.Create(nameof(Notes), typeof(IReadOnlyList<HeldChip>), typeof(HeldSlot),
        Array.Empty<HeldChip>(), propertyChanged: (bindable, _, _) => ((HeldSlot)bindable).Rebuild());

    public static readonly BindableProperty SustainedNotesProperty = BindableProperty.Create(nameof(SustainedNotes), typeof(IReadOnlyList<HeldChip>), typeof(HeldSlot),
        Array.Empty<HeldChip>(), propertyChanged: (bindable, _, _) => ((HeldSlot)bindable).Rebuild());

    /// <summary>Null without a chord recognizer: the UI never infers a chord itself.</summary>
    public static readonly BindableProperty ChordNameProperty = BindableProperty.Create(nameof(ChordName), typeof(string), typeof(HeldSlot), null,
        propertyChanged: (bindable, _, _) => ((HeldSlot)bindable).Rebuild());

    /// <summary>Details make the chips compact rows (review); without them the chips are large (live view).</summary>
    public static readonly BindableProperty ShowDetailsProperty = BindableProperty.Create(nameof(ShowDetails), typeof(bool), typeof(HeldSlot), false,
        propertyChanged: (bindable, _, _) => ((HeldSlot)bindable).Rebuild());

    private readonly VerticalStackLayout _content = new() { Spacing = 8 };

    public HeldSlot()
    {
        SetDynamicResource(HeightRequestProperty, "HeldSlotHeight");
        Content = new ScrollView { Content = _content };
        Rebuild();
    }

    public IReadOnlyList<HeldChip> Notes
    {
        get => (IReadOnlyList<HeldChip>)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public IReadOnlyList<HeldChip> SustainedNotes
    {
        get => (IReadOnlyList<HeldChip>)GetValue(SustainedNotesProperty);
        set => SetValue(SustainedNotesProperty, value);
    }

    public string? ChordName
    {
        get => (string?)GetValue(ChordNameProperty);
        set => SetValue(ChordNameProperty, value);
    }

    public bool ShowDetails
    {
        get => (bool)GetValue(ShowDetailsProperty);
        set => SetValue(ShowDetailsProperty, value);
    }

    private void Rebuild()
    {
        _content.Children.Clear();
        if (ChordName is { Length: > 0 } chord)
        {
            var name = new Label { Text = chord };
            name.SetDynamicResource(StyleProperty, "TitleLabel");
            _content.Add(name);
        }
        if (Notes.Count == 0 && SustainedNotes.Count == 0)
        {
            var none = new Label { Text = Strings.HeldNone };
            none.SetDynamicResource(StyleProperty, "SecondaryLabel");
            _content.Add(none);
        }
        else
        {
            if (Notes.Count > 0) _content.Add(Chips(Notes, sustained: false));
            if (SustainedNotes.Count > 0)
            {
                var caption = new Label { Text = Strings.SustainedSection };
                caption.SetDynamicResource(StyleProperty, "SectionLabel");
                _content.Add(caption);
                _content.Add(Chips(SustainedNotes, sustained: true));
            }
        }
        SemanticProperties.SetDescription(this, string.Join(", ", Notes.Concat(SustainedNotes).Select(chip => chip.Name)) is { Length: > 0 } names ? names : Strings.HeldNone);
    }

    private View Chips(IReadOnlyList<HeldChip> chips, bool sustained)
    {
        var layout = ShowDetails
            ? (Layout)new VerticalStackLayout { Spacing = 6 }
            : new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
                JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center };
        foreach (var chip in chips) layout.Add(Chip(chip, sustained));
        return layout;
    }

    private Border Chip(HeldChip chip, bool sustained)
    {
        var text = new VerticalStackLayout { Spacing = 0 };
        var name = new Label { Text = chip.Name, FontAttributes = FontAttributes.Bold };
        if (!sustained) name.SetDynamicResource(StyleProperty, "HeldChipText");
        name.SetDynamicResource(Label.FontSizeProperty, ShowDetails ? "FontBody" : "FontTitle");
        text.Add(name);
        if (chip.Details is { } details)
        {
            var line = new Label { Text = details, LineBreakMode = LineBreakMode.TailTruncation };
            line.SetDynamicResource(StyleProperty, sustained ? "SecondaryLabel" : "HeldChipText");
            line.SetDynamicResource(Label.FontSizeProperty, "FontSecondary");
            text.Add(line);
            ToolTipProperties.SetText(text, $"{chip.Name} · {details}");
        }
        var border = new Border { Content = text, Margin = new Thickness(0, 0, 6, 6) };
        border.SetDynamicResource(StyleProperty, sustained ? "Chip" : "HeldChip");
        return border;
    }
}
