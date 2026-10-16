using Orchid.Ui.Contracts;
using Orchid.Ui.Theme;

namespace Orchid.Ui.State;

/// <summary>Configuration of the next session. Locked while a session runs; review uses the recorded settings, not these.</summary>
public sealed class SetupState : ObservableObject
{
    public const int MinBpm = 30, MaxBpm = 240;
    public static IReadOnlyList<int> PulseOptions { get; } = [1, 2, 4, 8, 16];
    public static IReadOnlyList<int> CountInOptions { get; } = [0, 1, 2];

    // The scope is fixed 4/4 for now (UI-SPEC "Screen composition").
    private const int MeterNumerator = 4, MeterDenominator = 4;

    private IReadOnlyList<MidiInputInfo> _inputs = [];
    private MidiInputInfo? _selectedInput;
    private int _bpm = 120;
    private int _pulse = 4;
    private IReadOnlyList<bool> _strongBeats = DefaultStrongBeats(4);
    private int _countInBars = 1;
    private bool _isLocked;

    public IReadOnlyList<MidiInputInfo> Inputs
    {
        get => _inputs;
        private set
        {
            if (!Set(ref _inputs, value)) return;
            Raise(nameof(HasInputs));
            Raise(nameof(HasNoInputs));
        }
    }

    public bool HasInputs => Inputs.Count > 0;

    public bool HasNoInputs => Inputs.Count == 0;

    public MidiInputInfo? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (IsLocked || (value is not null && !Inputs.Contains(value))) return;
            if (Set(ref _selectedInput, value)) Raise(nameof(CanStart));
        }
    }

    public int Bpm
    {
        get => _bpm;
        set
        {
            if (!IsLocked) Set(ref _bpm, Math.Clamp(value, MinBpm, MaxBpm));
        }
    }

    /// <summary>A new pulse changes the number of beats in the measure, so the pattern restarts with only beat 1 strong.</summary>
    public int PulseDenominator
    {
        get => _pulse;
        set
        {
            if (IsLocked || !PulseOptions.Contains(value) || !Set(ref _pulse, value)) return;
            StrongBeats = DefaultStrongBeats(ClicksPerMeasure);
        }
    }

    public int ClicksPerMeasure => MeterNumerator * PulseDenominator / MeterDenominator;

    /// <summary>One flag per click of the measure. A pattern of another length is rejected, never trimmed or padded.</summary>
    public IReadOnlyList<bool> StrongBeats
    {
        get => _strongBeats;
        set
        {
            if (IsLocked) return;
            if (value.Count != ClicksPerMeasure)
                throw new ArgumentException($"The pattern needs {ClicksPerMeasure} beats, not {value.Count}.", nameof(value));
            if (Set(ref _strongBeats, [.. value])) Raise(nameof(StrongBeatsSummary));
        }
    }

    public string StrongBeatsSummary => Display.StrongBeats(StrongBeats);

    public string MeterText => Display.Format(Strings.MeterValue, MeterNumerator, MeterDenominator);

    public int CountInBars
    {
        get => _countInBars;
        set
        {
            if (!IsLocked && CountInOptions.Contains(value)) Set(ref _countInBars, value);
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (Set(ref _isLocked, value))
            {
                Raise(nameof(IsUnlocked));
                Raise(nameof(CanStart));
            }
        }
    }

    public bool IsUnlocked => !IsLocked;

    public bool CanStart => SelectedInput is not null && !IsLocked;

    public static bool[] DefaultStrongBeats(int clicks) => [.. Enumerable.Range(0, clicks).Select(click => click == 0)];

    /// <summary>Reloads the inputs and keeps the selection when the same device is still present.</summary>
    public async Task RefreshAsync(IMidiInputCatalog catalog, CancellationToken cancellationToken)
    {
        if (IsLocked) return;
        var inputs = await catalog.GetInputsAsync(cancellationToken);
        var keep = inputs.FirstOrDefault(input => input.Id == SelectedInput?.Id);
        Inputs = inputs;
        if (Set(ref _selectedInput, keep ?? inputs.FirstOrDefault(), nameof(SelectedInput))) Raise(nameof(CanStart));
    }

    public SessionSettings ToSettings() =>
        new(Bpm, PulseDenominator, MeterNumerator, MeterDenominator, [.. StrongBeats], CountInBars);
}
