using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Orchid.Application;
using Orchid.Core;
using Orchid.Infrastructure;

namespace Orchid.Platform.Windows;

public sealed class WindowsRecordingSession : IDisposable, ITimeProvider
{
    public event Action<int, KeyInteraction, long>? NoteReceived;
    private readonly IInputDevice _device;
    private readonly IBeatSoundPlayer _sound;
    private readonly ITimeProvider _clock;
    private readonly TimingStorage _storage;
    private readonly DryWetMidiInput _input;
    private readonly MusicPlayThrough _metronome;
    private readonly TaskCompletionSource _deviceFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _startedAt;
    private long? _endedAt;
    private bool _started;
    private bool _disposed;

    internal WindowsRecordingSession(IInputDevice device, IBeatSoundPlayer sound,
        IDelayProvider delay, ITimeProvider clock, int bpm, int pulseDenominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bpm);
        if (pulseDenominator is not (1 or 2 or 4 or 8 or 16))
            throw new ArgumentOutOfRangeException(nameof(pulseDenominator));

        _device = device;
        _sound = sound;
        _clock = clock;
        _startedAt = clock.GetCurrentTimeMilliseconds();
        _storage = new TimingStorage(this);
        _input = new DryWetMidiInput(device, new KeyListener(new MusicSound(MusicalNote.C, 4),
            new MidiKeyConverterProvider(new MidiNoteKeyConverter()), _storage));
        // A 4/4 measure contains pulseDenominator selected pulses.
        var signature = TimeSignature.CommonTime with { BeatsPerMeasure = pulseDenominator, BeatUnit = pulseDenominator };
        _metronome = new MusicPlayThrough(delay, new MusicScoreDefinition(signature, bpm, MusicalNote.C, 4), bpm, sound);
        if (device is InputDevice nativeDevice) nativeDevice.ErrorOccurred += OnDeviceError;
    }

    public static (int Index, string Name)[] GetDevices()
    {
        var devices = new List<(int, string)>();
        for (var index = 0; index < InputDevice.GetDevicesCount(); index++)
        {
            using var device = InputDevice.GetByIndex(index);
            devices.Add((index, device.Name));
        }
        return devices.ToArray();
    }

    public static WindowsRecordingSession Open(int deviceIndex, int bpm, int pulseDenominator)
    {
        var device = InputDevice.GetByIndex(deviceIndex);
        try
        {
            var sound = new NAudioBeatSoundPlayer();
            try
            {
                return new WindowsRecordingSession(device, sound, new TaskDelayProvider(),
                    new StopwatchTimeProvider(), bpm, pulseDenominator);
            }
            catch { sound.Dispose(); throw; }
        }
        catch { device.Dispose(); throw; }
    }

    public long ElapsedMilliseconds => _endedAt ?? Math.Max(0, _clock.GetCurrentTimeMilliseconds() - _startedAt);
    long ITimeProvider.GetCurrentTimeMilliseconds() => ElapsedMilliseconds;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) throw new InvalidOperationException("This recording session has already started.");
        cancellationToken.ThrowIfCancellationRequested();
        _started = true;
        _startedAt = _clock.GetCurrentTimeMilliseconds();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _device.EventReceived += OnLiveNote;
        var inputTask = _input.RunAsync(stop.Token);
        var metronomeTask = inputTask.IsFaulted ? Task.CompletedTask : _metronome.Start(stop.Token);
        try
        {
            await await Task.WhenAny(inputTask, metronomeTask, _deviceFailure.Task).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            _endedAt = ElapsedMilliseconds;
            _device.EventReceived -= OnLiveNote;
            stop.Cancel();
            try { await Task.WhenAll(inputTask, metronomeTask).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    // shortcut: existing MIDI callbacks may outlive Stop; drain them if a real recording exposes this race.
    public KeyPressTiming[] GetTimings() => _storage.GetTimings().ToArray();

    private void OnDeviceError(object? sender, ErrorOccurredEventArgs args) =>
        _deviceFailure.TrySetException(args.Exception);

    private void OnLiveNote(object? sender, MidiEventReceivedEventArgs args)
    {
        switch (args.Event)
        {
            case NoteOnEvent note:
                NoteReceived?.Invoke((int)note.NoteNumber,
                    (int)note.Velocity == 0 ? KeyInteraction.Release : KeyInteraction.Press, ElapsedMilliseconds);
                break;
            case NoteOffEvent note:
                NoteReceived?.Invoke((int)note.NoteNumber, KeyInteraction.Release, ElapsedMilliseconds);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_device is InputDevice nativeDevice) nativeDevice.ErrorOccurred -= OnDeviceError;
        try { _device.Dispose(); }
        finally { if (_sound is IDisposable disposableSound) disposableSound.Dispose(); }
    }
}
