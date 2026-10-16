using Orchid.Ui.Contracts;
using Orchid.Ui.Controls;
using Orchid.Ui.Theme;

namespace Orchid.Ui.State;

/// <summary>The latest live snapshot as bindable properties. Every property is assigned only when it changes.</summary>
public sealed class LiveState : ObservableObject
{
    private IReadOnlyList<bool> _strongBeats = [];
    private bool _isCountIn;
    private int _currentBeat = -1;
    private string _elapsedText = Display.Elapsed(TimeSpan.Zero);
    private string _barText = "";
    private string _beatText = "";
    private string _countText = "";
    private IReadOnlyList<HeldKey> _held = [];
    private IReadOnlyList<HeldChip> _heldChips = [];
    private bool _pedalDown;
    private string _settingsText = "";

    public IReadOnlyList<bool> StrongBeats { get => _strongBeats; private set => Set(ref _strongBeats, value); }
    public bool IsCountIn
    {
        get => _isCountIn;
        private set
        {
            if (Set(ref _isCountIn, value)) Raise(nameof(IsRecordingPhase));
        }
    }

    public bool IsRecordingPhase => !IsCountIn;

    /// <summary>The lit beat of the beat bars, from 0.</summary>
    public int CurrentBeat { get => _currentBeat; private set => Set(ref _currentBeat, value); }

    public string ElapsedText { get => _elapsedText; private set => Set(ref _elapsedText, value); }
    public string BarText { get => _barText; private set => Set(ref _barText, value); }
    public string BeatText { get => _beatText; private set => Set(ref _beatText, value); }

    /// <summary>The large count shown instead of the bar number during the count-in.</summary>
    public string CountText { get => _countText; private set => Set(ref _countText, value); }

    public IReadOnlyList<HeldChip> HeldChips { get => _heldChips; private set => Set(ref _heldChips, value); }

    public bool PedalDown
    {
        get => _pedalDown;
        private set
        {
            if (Set(ref _pedalDown, value)) Raise(nameof(PedalText));
        }
    }

    public string PedalText => PedalDown ? Strings.PedalDown : Strings.PedalUp;

    /// <summary>The locked settings shown in the header: BPM, pulse and meter.</summary>
    public string SettingsText { get => _settingsText; private set => Set(ref _settingsText, value); }

    public void Begin(SessionSettings settings)
    {
        StrongBeats = settings.StrongBeats;
        SettingsText = string.Join(" · ", Display.Format(Strings.BpmValue, settings.Bpm),
            Display.Format(Strings.PulseValue, settings.PulseDenominator),
            Display.Format(Strings.MeterValue, settings.MeterNumerator, settings.MeterDenominator));
        _held = [];
        HeldChips = [];
        CurrentBeat = -1;
    }

    public void Apply(LiveSnapshot snapshot, SessionSettings settings)
    {
        IsCountIn = snapshot.Phase == LivePhase.CountIn;
        CurrentBeat = snapshot.Beat - 1;
        ElapsedText = Display.Elapsed(snapshot.Elapsed);
        BarText = Display.Format(Strings.LiveBar, snapshot.Bar);
        BeatText = Display.Format(Strings.LiveBeat, snapshot.Beat, settings.ClicksPerMeasure);
        CountText = snapshot.Beat.ToString(System.Globalization.CultureInfo.CurrentCulture);
        PedalDown = snapshot.PedalDown;
        if (!_held.SequenceEqual(snapshot.Held))
        {
            _held = snapshot.Held;
            HeldChips = [.. snapshot.Held.OrderBy(key => key.Midi).Select(key => new HeldChip(Display.NoteName(key.Midi), null))];
        }
    }
}
