using VideoSpace.Core;
using Windows.System;

namespace VideoSpace.Controls;

public sealed class TimecodeField : Grid
{
    private readonly TextBox _input;
    private long _frame;
    public FrameRate Rate { get; set; } = FrameRate.Film;
    public event Action<long>? Committed;
    public TimecodeField(string name)
    {
        _input = Studio.Input("00:00:00:00"); _input.Width = 108; _input.Foreground = Studio.Brush(Studio.Accent); _input.FontFamily = new FontFamily("Consolas"); _input.BorderThickness = new(0); _input.Background = Studio.Brush("#00000000"); Studio.Name(_input, name); Children.Add(_input);
        _input.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { try { Committed?.Invoke(Timecode.Parse(_input.Text, Rate)); } catch { Update(_frame, true); } e.Handled = true; } };
        _input.LostFocus += (_, _) => Update(_frame, true);
    }
    public void Update(long frame, bool force = false) { _frame = frame; if (force || _input.FocusState == FocusState.Unfocused) _input.Text = Timecode.Format(Math.Max(0, frame), Rate); }
}
