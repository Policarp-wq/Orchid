using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Timeline.Drawing;

/// <summary>
/// The whole recording in miniature: the lowest and highest sounding pitch per pixel column, the visible part as a frame and the playhead.
/// The columns are computed when the recording or the width changes and kept as one path; a frame only draws the frame and the playhead.
/// </summary>
public sealed class OverviewDrawable(ReviewState review) : IDrawable
{
    private const float Padding = 4;
    private NoteIndex? _notes;
    private int _width;
    private float _height;
    private PathF _columns = new();

    /// <summary>The frame of the visible time range in a strip of this width.</summary>
    public static (float Left, float Right) Frame(ReviewState review, float width)
    {
        var duration = review.Duration.TotalSeconds;
        if (duration <= 0) return (0, width);
        var camera = review.Camera;
        var left = (float)(camera.TimeOffset / duration * width);
        var right = (float)((camera.TimeOffset + camera.VisibleSeconds) / duration * width);
        return (Math.Clamp(left, 0, width), Math.Clamp(right, 0, width));
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var palette = Palette.Current;
        float width = dirtyRect.Width, height = dirtyRect.Height;
        canvas.FillColor = palette.SubtleSurface;
        canvas.FillRoundedRectangle(0, 0, width, height, 4);
        EnsureColumns(width, height);
        canvas.FillColor = palette.NoteStroke;
        canvas.FillPath(_columns);

        var (left, right) = Frame(review, width);
        canvas.FillColor = palette.GridBar.WithAlpha(.12f);
        canvas.FillRectangle(left, 0, Math.Max(2, right - left), height);
        canvas.StrokeColor = palette.GridBar;
        canvas.StrokeSize = 1.5f;
        canvas.DrawRectangle(left, 0.75f, Math.Max(2, right - left), height - 1.5f);

        var duration = review.Duration.TotalSeconds;
        var x = duration <= 0 ? 0 : (float)(review.Position.TotalSeconds / duration * width);
        canvas.StrokeColor = palette.Accent;
        canvas.StrokeSize = 2;
        canvas.DrawLine(x, 0, x, height);
    }

    private void EnsureColumns(float width, float height)
    {
        var columns = (int)width;
        if (ReferenceEquals(_notes, review.Notes) && columns == _width && height == _height) return;
        _notes = review.Notes;
        (_width, _height) = (columns, height);
        _columns = new PathF();
        if (_notes.Lowest is not { } low || _notes.Highest is not { } high) return;
        var data = OverviewColumns.Compute(_notes, review.Duration.TotalSeconds, columns);
        var span = high - low + 1;
        var usable = height - 2 * Padding;
        for (var column = 0; column < data.Width; column++)
        {
            if (data.Low[column] < 0) continue;
            var y = Padding + (high - data.High[column]) / (float)span * usable;
            var bottom = Padding + (high - data.Low[column] + 1) / (float)span * usable;
            _columns.AppendRectangle(column, y, 1, Math.Max(2, bottom - y));
        }
    }
}
