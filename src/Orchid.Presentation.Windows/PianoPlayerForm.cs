using System.Diagnostics;

namespace Orchid.Presentation.Windows;

internal sealed class PianoPlayerForm : Form
{
    private readonly Button openButton = new() { Text = "Open performance log", AutoSize = true };
    private readonly Button playPauseButton = new() { Text = "Play", AutoSize = true, Enabled = false };
    private readonly Button stopButton = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Label statusLabel = new() { AutoSize = true, Text = "Open an Orchid performance log to begin." };
    private readonly PianoKeyboardControl pianoKeyboard = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer playbackTimer = new() { Interval = 16 };
    private readonly Stopwatch playbackStopwatch = new();
    private IReadOnlyList<PerformanceNote> notes = [];
    private TimeSpan pausedPosition = TimeSpan.Zero;

    public PianoPlayerForm()
    {
        Text = "Orchid Piano Visualizer";
        MinimumSize = new Size(900, 360);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(12),
            WrapContents = false
        };

        toolbar.Controls.Add(openButton);
        toolbar.Controls.Add(playPauseButton);
        toolbar.Controls.Add(stopButton);
        toolbar.Controls.Add(statusLabel);

        Controls.Add(pianoKeyboard);
        Controls.Add(toolbar);

        openButton.Click += OnOpenButtonClick;
        playPauseButton.Click += OnPlayPauseButtonClick;
        stopButton.Click += OnStopButtonClick;
        playbackTimer.Tick += OnPlaybackTimerTick;
    }

    private void OnOpenButtonClick(object? sender, EventArgs eventArgs)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Orchid performance logs (*.orchid)|*.orchid|All files (*.*)|*.*",
            Title = "Open Orchid Performance Log"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            notes = PerformanceLogReader.Read(dialog.FileName);
            ResetPlayback();
            playPauseButton.Enabled = notes.Count > 0;
            stopButton.Enabled = notes.Count > 0;
            statusLabel.Text = $"Loaded {notes.Count} note events.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to open performance log", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnPlayPauseButtonClick(object? sender, EventArgs eventArgs)
    {
        if (playbackStopwatch.IsRunning)
        {
            PausePlayback();
            return;
        }

        if (pausedPosition >= GetPerformanceDuration())
        {
            ResetPlayback();
        }

        playbackStopwatch.Start();
        playbackTimer.Start();
        playPauseButton.Text = "Pause";
        statusLabel.Text = "Playing.";
    }

    private void OnStopButtonClick(object? sender, EventArgs eventArgs)
    {
        ResetPlayback();
        statusLabel.Text = "Stopped.";
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs eventArgs)
    {
        var currentPosition = pausedPosition + playbackStopwatch.Elapsed;
        UpdateKeyboard(currentPosition);

        if (currentPosition < GetPerformanceDuration())
        {
            return;
        }

        PausePlayback();
        statusLabel.Text = "Finished.";
    }

    private void PausePlayback()
    {
        pausedPosition += playbackStopwatch.Elapsed;
        playbackStopwatch.Reset();
        playbackTimer.Stop();
        playPauseButton.Text = "Play";
    }

    private void ResetPlayback()
    {
        playbackStopwatch.Reset();
        playbackTimer.Stop();
        pausedPosition = TimeSpan.Zero;
        playPauseButton.Text = "Play";
        UpdateKeyboard(pausedPosition);
    }

    private void UpdateKeyboard(TimeSpan currentPosition)
    {
        pianoKeyboard.SetActiveNotes(
            notes
                .Where(note => note.StartOffset <= currentPosition && currentPosition < note.EndOffset)
                .Select(note => note.MidiNoteNumber));
    }

    private TimeSpan GetPerformanceDuration()
    {
        return notes.Count == 0 ? TimeSpan.Zero : notes.Max(note => note.EndOffset);
    }
}
