using System.Globalization;
using Windows.System;

namespace VideoSpace.Controls;

/// <summary>Editable numeric field with drag-to-scrub label. Each committed gesture produces one edit transaction.</summary>
public sealed class ValueField : Grid
{
    private readonly TextBox _input;
    private readonly TextBlock _label;
    private double _value, _startValue, _startX;
    private bool _dragging, _updating;
    public double Value => _value;
    public event Action<double>? Committed;
    public ValueField(string name, double value, double min, double max, double step = 1, string suffix = "")
    {
        ColumnDefinitions.Add(new() { Width = Studio.Star() }); ColumnDefinitions.Add(new() { Width = new(84) });
        Height = 31; _value = value;
        _label = Studio.Text(name, 12, Studio.Muted); _label.Margin = new(9, 0, 6, 0);
        _input = Studio.Input(value.ToString("0.###", CultureInfo.InvariantCulture)); _input.TextAlignment = TextAlignment.Right; _input.Margin = new(2, 2, 8, 2); _input.Foreground = Studio.Brush(Studio.Accent); Studio.Name(_input, name); ToolTipService.SetToolTip(_label, "Drag to adjust " + name + suffix);
        Studio.At(this, _label); Studio.At(this, _input, column: 1);
        void Commit()
        {
            if (_updating) return;
            if (!double.TryParse(_input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var next) || !double.IsFinite(next)) { SetValue(_value); return; }
            next = Math.Clamp(next, min, max); if (Math.Abs(next - _value) < 1e-8) return; _value = next; Committed?.Invoke(next); SetValue(next);
        }
        _input.LostFocus += (_, _) => Commit();
        _input.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; } if (e.Key == VirtualKey.Escape) { SetValue(_value); e.Handled = true; } };
        _label.PointerPressed += (_, e) => { _startX = e.GetCurrentPoint(null).Position.X; _startValue = _value; _dragging = _label.CapturePointer(e.Pointer); e.Handled = true; };
        _label.PointerMoved += (_, e) => { if (_dragging) { _input.Text = Math.Clamp(_startValue + (e.GetCurrentPoint(null).Position.X - _startX) * step, min, max).ToString("0.###", CultureInfo.InvariantCulture); e.Handled = true; } };
        _label.PointerReleased += (_, e) => { if (!_dragging) return; _dragging = false; _label.ReleasePointerCapture(e.Pointer); Commit(); e.Handled = true; };
        _label.PointerCaptureLost += (_, _) => { if (_dragging) { _dragging = false; SetValue(_value); } };
    }
    public void SetValue(double value) { _updating = true; _value = value; _input.Text = value.ToString("0.###", CultureInfo.InvariantCulture); _updating = false; }
}
