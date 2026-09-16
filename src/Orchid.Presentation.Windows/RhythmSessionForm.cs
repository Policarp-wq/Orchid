using System.Diagnostics;
using Orchid.Application;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class RhythmSessionForm : Form
{
    private static readonly RhythmicValueOption[] RhythmicValueOptions =
    [
        new(RhythmicValue.Whole, "1/1"),
        new(RhythmicValue.Half, "1/2"),
        new(RhythmicValue.Quarter, "1/4"),
        new(RhythmicValue.Eighth, "1/8"),
        new(RhythmicValue.Sixteenth, "1/16")
    ];

    private readonly IMidiInput midiInput;
    private readonly IMetronome metronome;
    private readonly RhythmSessionRecorder sessionRecorder;
    private readonly ComboBox midiDeviceComboBox = CreateComboBox(220);
    private readonly Button refreshDevicesButton = new() { Text = "Refresh devices", AutoSize = true };
    private readonly NumericUpDown bpmInput = new() { Minimum = 1, Maximum = 400, Value = 40, Width = 70 };
    private readonly ComboBox pulseUnitComboBox = CreateComboBox(70);
    private readonly Button startSessionButton = new() { Text = "Start", AutoSize = true };
    private readonly Button stopSessionButton = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Label sessionStatusLabel = new() { AutoSize = true, Text = "Ready." };
    private readonly Button browseButton = new() { Text = "Browse...", AutoSize = true };
    private readonly TextBox logPathTextBox = new() { Width = 330 };
    private readonly Button loadButton = new() { Text = "Load", AutoSize = true };
    private readonly ComboBox gridUnitComboBox = CreateComboBox(70);
    private readonly Button playPauseButton = new() { Text = "Play", AutoSize = true, Enabled = false };
    private readonly Button stopPlaybackButton = new() { Text = "Stop playback", AutoSize = true, Enabled = false };
    private readonly Label performanceStatusLabel = new() { AutoSize = true, Text = "No performance loaded." };
    private readonly Label selectedNoteStatusLabel = new() { AutoSize = true, Text = "Click a note block to inspect it." };
    private readonly RhythmTimelineControl timeline = new() { Dock = DockStyle.Fill };
    private readonly PianoKeyboardControl pianoKeyboard = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer sessionUiTimer = new() { Interval = 50 };
    private readonly System.Windows.Forms.Timer playbackTimer = new() { Interval = 16 };
    private readonly Stopwatch playbackStopwatch = new();
    private readonly HashSet<int> livePressedNotes = [];
    private PerformanceSession? performanceSession;
    private TimeSpan pausedPlaybackPosition;
    private int recordedAttackCount;
    private bool isClosing;

    public RhythmSessionForm(
        IMidiInput midiInput,
        IMetronome metronome,
        RhythmSessionRecorder sessionRecorder)
    {
        this.midiInput = midiInput;
        this.metronome = metronome;
        this.sessionRecorder = sessionRecorder;

        Text = "Orchid Rhythm Session";
        MinimumSize = new Size(1_000, 620);
        Size = new Size(1_260, 720);
        StartPosition = FormStartPosition.CenterScreen;

        ConfigureSelectors();
        BuildLayout();
        BindEvents();
        RefreshMidiDevices();
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        isClosing = true;
        sessionUiTimer.Stop();
        playbackTimer.Stop();

        if (sessionRecorder.IsRecording)
        {
            StopSession(saveLog: true);
        }

        midiInput.Dispose();
        metronome.Dispose();
        base.OnFormClosing(eventArgs);
    }

    private static ComboBox CreateComboBox(int width)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = width
        };
    }

    private void ConfigureSelectors()
    {
        pulseUnitComboBox.Items.AddRange(RhythmicValueOptions);
        pulseUnitComboBox.SelectedItem = RhythmicValueOptions.Single(option => option.Value == RhythmicValue.Quarter);

        gridUnitComboBox.Items.AddRange(RhythmicValueOptions);
        gridUnitComboBox.SelectedItem = RhythmicValueOptions.Single(option => option.Value == RhythmicValue.Quarter);
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));

        rootLayout.Controls.Add(CreateSessionToolbar(), 0, 0);
        rootLayout.Controls.Add(CreateLogToolbar(), 0, 1);
        rootLayout.Controls.Add(timeline, 0, 2);
        rootLayout.Controls.Add(pianoKeyboard, 0, 3);
        Controls.Add(rootLayout);
    }

    private Control CreateSessionToolbar()
    {
        var toolbar = CreateToolbar();
        toolbar.Controls.Add(CreateLabel("MIDI input:"));
        toolbar.Controls.Add(midiDeviceComboBox);
        toolbar.Controls.Add(refreshDevicesButton);
        toolbar.Controls.Add(CreateLabel("BPM:"));
        toolbar.Controls.Add(bpmInput);
        toolbar.Controls.Add(CreateLabel("Pulse:"));
        toolbar.Controls.Add(pulseUnitComboBox);
        toolbar.Controls.Add(CreateLabel("Time signature: 4/4"));
        toolbar.Controls.Add(startSessionButton);
        toolbar.Controls.Add(stopSessionButton);
        toolbar.Controls.Add(sessionStatusLabel);
        return toolbar;
    }

    private Control CreateLogToolbar()
    {
        var toolbar = CreateToolbar();
        toolbar.Controls.Add(CreateLabel("Performance:"));
        toolbar.Controls.Add(browseButton);
        toolbar.Controls.Add(logPathTextBox);
        toolbar.Controls.Add(loadButton);
        toolbar.Controls.Add(CreateLabel("Grid:"));
        toolbar.Controls.Add(gridUnitComboBox);
        toolbar.Controls.Add(playPauseButton);
        toolbar.Controls.Add(stopPlaybackButton);
        toolbar.Controls.Add(performanceStatusLabel);
        toolbar.Controls.Add(selectedNoteStatusLabel);
        return toolbar;
    }

    private static FlowLayoutPanel CreateToolbar()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 10, 4),
            WrapContents = true
        };
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Margin = new Padding(8, 7, 3, 0),
            Text = text
        };
    }

    private void BindEvents()
    {
        refreshDevicesButton.Click += (_, _) => RefreshMidiDevices();
        startSessionButton.Click += (_, _) => StartSession();
        stopSessionButton.Click += (_, _) => StopSession(saveLog: true);
        browseButton.Click += (_, _) => BrowseForLog();
        loadButton.Click += (_, _) => LoadPerformanceLog(logPathTextBox.Text);
        gridUnitComboBox.SelectedIndexChanged += (_, _) => RefreshTimeline();
        playPauseButton.Click += (_, _) => TogglePlayback();
        stopPlaybackButton.Click += (_, _) => StopPlayback();
        sessionUiTimer.Tick += (_, _) => UpdateRunningStatus();
        playbackTimer.Tick += (_, _) => UpdatePlayback();
        midiInput.NoteReceived += OnMidiNoteReceived;
        timeline.NoteSelected += OnTimelineNoteSelected;
    }

    private void RefreshMidiDevices()
    {
        var previousSelection = midiDeviceComboBox.SelectedItem as string;

        try
        {
            var deviceNames = midiInput.GetDeviceNames();
            midiDeviceComboBox.Items.Clear();
            midiDeviceComboBox.Items.AddRange(deviceNames.Cast<object>().ToArray());

            midiDeviceComboBox.SelectedItem = deviceNames.Contains(previousSelection, StringComparer.Ordinal)
                ? previousSelection
                : deviceNames.FirstOrDefault();

            sessionStatusLabel.Text = deviceNames.Count == 0
                ? "No MIDI input devices found."
                : $"Found {deviceNames.Count} MIDI input device(s).";
        }
        catch (Exception exception)
        {
            ShowError("Unable to enumerate MIDI input devices", exception);
        }
    }

    private void StartSession()
    {
        if (midiDeviceComboBox.SelectedItem is not string deviceName)
        {
            sessionStatusLabel.Text = "Select a MIDI input device first.";
            return;
        }

        var tempo = new Tempo(bpmInput.Value, GetSelectedValue(pulseUnitComboBox));

        try
        {
            StopPlayback();
            midiInput.Start(deviceName);
            sessionRecorder.Start(tempo);
            metronome.Start(tempo);
            recordedAttackCount = 0;
            livePressedNotes.Clear();
            pianoKeyboard.SetActiveNotes(livePressedNotes);
            SetSessionControls(isRunning: true);
            sessionUiTimer.Start();
            UpdateRunningStatus();
        }
        catch (Exception exception)
        {
            metronome.Stop();
            midiInput.Stop();

            if (sessionRecorder.IsRecording)
            {
                sessionRecorder.Stop();
            }

            SetSessionControls(isRunning: false);
            ShowError("Unable to start the rhythm session", exception);
        }
    }

    private void StopSession(bool saveLog)
    {
        if (!sessionRecorder.IsRecording)
        {
            return;
        }

        sessionUiTimer.Stop();
        var session = sessionRecorder.Stop();
        metronome.Stop();
        midiInput.Stop();
        livePressedNotes.Clear();
        pianoKeyboard.SetActiveNotes(livePressedNotes);

        performanceSession = PerformanceSession.FromRhythmSession(session);
        ApplyPerformanceSession();
        SetSessionControls(isRunning: false);

        if (!saveLog)
        {
            return;
        }

        try
        {
            var logPath = PerformanceLogWriter.Write(session, Path.Combine(AppContext.BaseDirectory, "logs"));
            logPathTextBox.Text = logPath;
            sessionStatusLabel.Text = "Session stopped.";
            performanceStatusLabel.Text = $"Displayed and saved {session.Notes.Count} note(s): {Path.GetFileName(logPath)}";
        }
        catch (Exception exception)
        {
            ShowError("Unable to save the rhythm session", exception);
        }
    }

    private void OnMidiNoteReceived(object? sender, MidiNoteEventArgs eventArgs)
    {
        sessionRecorder.Record(eventArgs);

        if (isClosing || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() => UpdateLiveKeyboard(eventArgs));
    }

    private void UpdateLiveKeyboard(MidiNoteEventArgs eventArgs)
    {
        if (!sessionRecorder.IsRecording)
        {
            return;
        }

        if (eventArgs.IsPressed)
        {
            livePressedNotes.Add(eventArgs.MidiNoteNumber);
            recordedAttackCount++;
        }
        else
        {
            livePressedNotes.Remove(eventArgs.MidiNoteNumber);
        }

        pianoKeyboard.SetActiveNotes(livePressedNotes);
    }

    private void UpdateRunningStatus()
    {
        sessionStatusLabel.Text = $"Recording: {sessionRecorder.Elapsed:mm\\:ss\\.fff} | Note attacks: {recordedAttackCount}";
    }

    private void SetSessionControls(bool isRunning)
    {
        midiDeviceComboBox.Enabled = !isRunning;
        refreshDevicesButton.Enabled = !isRunning;
        bpmInput.Enabled = !isRunning;
        pulseUnitComboBox.Enabled = !isRunning;
        startSessionButton.Enabled = !isRunning;
        stopSessionButton.Enabled = isRunning;
        browseButton.Enabled = !isRunning;
        loadButton.Enabled = !isRunning;
    }

    private void BrowseForLog()
    {
        using var dialog = new OpenFileDialog
        {
            AutoUpgradeEnabled = false,
            Filter = "Orchid performance logs (*.orchid)|*.orchid|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            RestoreDirectory = true,
            Title = "Open Orchid Performance Log"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        logPathTextBox.Text = dialog.FileName;
        LoadPerformanceLog(dialog.FileName);
    }

    private void LoadPerformanceLog(string filePath)
    {
        filePath = filePath.Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(filePath))
        {
            sessionStatusLabel.Text = "Choose a performance log or enter its path.";
            return;
        }

        if (!File.Exists(filePath))
        {
            performanceStatusLabel.Text = "The entered performance log path does not exist.";
            return;
        }

        try
        {
            StopPlayback();
            performanceSession = PerformanceLogReader.Read(filePath);
            ApplyPerformanceSession();
            sessionStatusLabel.Text = "Performance log loaded.";
            performanceStatusLabel.Text = performanceSession.Notes.Count == 0
                ? $"Loaded {Path.GetFileName(filePath)}, but it contains no notes."
                : $"Loaded {performanceSession.Notes.Count} note(s): {Path.GetFileName(filePath)}";
        }
        catch (Exception exception)
        {
            performanceStatusLabel.Text = "The performance log could not be loaded.";
            ShowError("Unable to open the performance log", exception);
        }
    }

    private void ApplyPerformanceSession()
    {
        pausedPlaybackPosition = TimeSpan.Zero;
        playPauseButton.Text = "Play";
        playPauseButton.Enabled = performanceSession?.Notes.Count > 0;
        stopPlaybackButton.Enabled = performanceSession?.Notes.Count > 0;
        RefreshTimeline();
        UpdatePlaybackKeyboard(TimeSpan.Zero);
    }

    private void RefreshTimeline()
    {
        timeline.SetSession(performanceSession, GetSelectedValue(gridUnitComboBox));
        selectedNoteStatusLabel.Text = performanceSession?.Notes.Count > 0
            ? "Click a note block to inspect it."
            : "No note is available for inspection.";
    }

    private void TogglePlayback()
    {
        if (performanceSession is null)
        {
            return;
        }

        if (playbackStopwatch.IsRunning)
        {
            PausePlayback();
            return;
        }

        if (pausedPlaybackPosition >= performanceSession.Duration)
        {
            pausedPlaybackPosition = TimeSpan.Zero;
        }

        playbackStopwatch.Start();
        playbackTimer.Start();
        playPauseButton.Text = "Pause";
    }

    private void UpdatePlayback()
    {
        if (performanceSession is null)
        {
            StopPlayback();
            return;
        }

        var currentPosition = pausedPlaybackPosition + playbackStopwatch.Elapsed;
        UpdatePlaybackKeyboard(currentPosition);

        if (currentPosition < performanceSession.Duration)
        {
            return;
        }

        PausePlayback();
        pausedPlaybackPosition = performanceSession.Duration;
    }

    private void PausePlayback()
    {
        pausedPlaybackPosition += playbackStopwatch.Elapsed;
        playbackStopwatch.Reset();
        playbackTimer.Stop();
        playPauseButton.Text = "Play";
    }

    private void StopPlayback()
    {
        playbackStopwatch.Reset();
        playbackTimer.Stop();
        pausedPlaybackPosition = TimeSpan.Zero;
        playPauseButton.Text = "Play";
        UpdatePlaybackKeyboard(TimeSpan.Zero);
    }

    private void UpdatePlaybackKeyboard(TimeSpan position)
    {
        var activeNotes = performanceSession?.Notes
            .Where(note => note.StartOffset <= position && position < note.EndOffset)
            .Select(note => note.MidiNoteNumber) ?? [];

        pianoKeyboard.SetActiveNotes(activeNotes);
    }

    private void OnTimelineNoteSelected(object? sender, PerformanceNoteSelectedEventArgs eventArgs)
    {
        StopPlayback();
        pianoKeyboard.SetActiveNotes([eventArgs.Note.MidiNoteNumber]);

        var deviationText = eventArgs.Deviations.Count == 0
            ? "rhythm deviation unavailable"
            : string.Join(" | ", eventArgs.Deviations.Select(RhythmDeviationFormatter.Format));

        selectedNoteStatusLabel.Text =
            $"Selected {MidiNoteName.Get(eventArgs.Note.MidiNoteNumber)} | " +
            $"Start {eventArgs.Note.StartOffset.TotalMilliseconds:0.#} ms | {deviationText}";
    }

    private static RhythmicValue GetSelectedValue(ComboBox comboBox)
    {
        return comboBox.SelectedItem is RhythmicValueOption option
            ? option.Value
            : RhythmicValue.Quarter;
    }

    private void ShowError(string title, Exception exception)
    {
        sessionStatusLabel.Text = exception.Message;

        if (!isClosing)
        {
            MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
