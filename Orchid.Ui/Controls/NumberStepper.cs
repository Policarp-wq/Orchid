using System.Diagnostics;
using System.Globalization;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Controls;

/// <summary>
/// A number with minus and plus buttons; holding a button repeats after 400 ms every 80 ms (as the phone remote does). On desktop the value is
/// also typed. The control keeps only the value and its limits: bind <see cref="Value"/>, listen to <see cref="ValueChanged"/>.
/// </summary>
public class NumberStepper : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(int), typeof(NumberStepper), 0,
        BindingMode.TwoWay, coerceValue: (bindable, value) => ((NumberStepper)bindable).Coerce((int)value),
        propertyChanged: (bindable, _, value) => ((NumberStepper)bindable).OnValueChanged((int)value));

    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(int), typeof(NumberStepper), 0,
        propertyChanged: (bindable, _, _) => ((NumberStepper)bindable).LimitsChanged());

    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(int), typeof(NumberStepper), 100,
        propertyChanged: (bindable, _, _) => ((NumberStepper)bindable).LimitsChanged());

    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(int), typeof(NumberStepper), 1);

    /// <summary>The accessible name of the value, such as "Tempo".</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(NumberStepper), "",
        propertyChanged: (bindable, _, _) => ((NumberStepper)bindable).UpdateNames());

    private readonly Button _decrease = new(), _increase = new();
    private readonly View _readout;
    private readonly Stopwatch _held = new();
    private IDispatcherTimer? _timer;
    private int _direction, _applied;
    private bool _repeated;

    public NumberStepper()
    {
        foreach (var (button, icon) in new[] { (_decrease, "IconMinus"), (_increase, "IconPlus") })
        {
            button.SetDynamicResource(StyleProperty, "IconButton");
            button.SetDynamicResource(Button.ImageSourceProperty, icon);
        }
        _decrease.Pressed += (_, _) => BeginHold(-1);
        _increase.Pressed += (_, _) => BeginHold(1);
        _decrease.Released += (_, _) => EndHold();
        _increase.Released += (_, _) => EndHold();
        _decrease.Clicked += (_, _) => Click(-1);
        _increase.Clicked += (_, _) => Click(1);

        // Typing is for a keyboard: touch devices step with the buttons only.
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            var entry = new Entry { Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.Center, WidthRequest = 72, VerticalOptions = LayoutOptions.Center };
            entry.Completed += (_, _) => Commit(entry);
            entry.Unfocused += (_, _) => Commit(entry);
            _readout = entry;
        }
        else
        {
            _readout = new Label { HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center, WidthRequest = 72 };
        }

        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 6 };
        grid.Add(_decrease);
        grid.Add(_readout, 1);
        grid.Add(_increase, 2);
        Content = grid;
        UpdateReadout(Value);
        UpdateNames();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public event EventHandler<int>? ValueChanged;

    private int Coerce(int value) => Math.Clamp(value, Minimum, Math.Max(Minimum, Maximum));

    private void LimitsChanged()
    {
        CoerceValue(ValueProperty);
        UpdateReadout(Value);
    }

    private void OnValueChanged(int value)
    {
        UpdateReadout(value);
        ValueChanged?.Invoke(this, value);
    }

    private void UpdateReadout(int value)
    {
        var text = value.ToString(CultureInfo.CurrentCulture);
        if (_readout is Entry entry) entry.Text = text;
        else if (_readout is Label label) label.Text = text;
        _decrease.IsEnabled = value > Minimum;
        _increase.IsEnabled = value < Maximum;
    }

    private void UpdateNames()
    {
        SemanticProperties.SetDescription(_decrease, Display.Format(Strings.DecreaseValue, Title));
        SemanticProperties.SetDescription(_increase, Display.Format(Strings.IncreaseValue, Title));
        ToolTipProperties.SetText(_decrease, Display.Format(Strings.DecreaseValue, Title));
        ToolTipProperties.SetText(_increase, Display.Format(Strings.IncreaseValue, Title));
        SemanticProperties.SetDescription(_readout, Title);
    }

    private void Commit(Entry entry)
    {
        if (int.TryParse(entry.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var typed)) Value = typed;
        UpdateReadout(Value);
    }

    // A quick click steps once from Clicked (which also serves the keyboard); holding past the delay repeats from the timer, and the
    // Clicked that follows the release is then ignored.
    private void Click(int direction)
    {
        if (_repeated)
        {
            _repeated = false;
            return;
        }
        Value = StepperRepeat.Move(Value, direction, Step, Minimum, Maximum);
    }

    private void BeginHold(int direction)
    {
        _direction = direction;
        _applied = 0;
        _repeated = false;
        _held.Restart();
        _timer ??= CreateTimer();
        _timer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(20);
        timer.Tick += (_, _) =>
        {
            // A button that disabled itself at a limit never raises Released.
            if (!(_direction < 0 ? _decrease : _increase).IsEnabled)
            {
                timer.Stop();
                _repeated = false;
                return;
            }
            var due = StepperRepeat.RepeatsAfter(_held.Elapsed) - _applied;
            if (due <= 0) return;
            _applied += due;
            _repeated = true;
            Value = StepperRepeat.Move(Value, _direction * due, Step, Minimum, Maximum);
        };
        return timer;
    }

    private void EndHold() => _timer?.Stop();
}
