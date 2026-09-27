namespace VideoSpace.Controls;

public sealed class SplitHandle : Border
{
    private Point _start;
    private double _before;
    private bool _dragging;
    public SplitHandle(Grid parent, int definition, bool vertical, double minimum = 180, double maximumFraction = .78)
    {
        Background = Studio.Brush(Studio.Line);
        PointerEntered += (_, _) => Background = Studio.Brush("#49617D");
        PointerExited += (_, _) => { if (!_dragging) Background = Studio.Brush(Studio.Line); };
        PointerPressed += (_, e) =>
        {
            _start = e.GetCurrentPoint(parent).Position;
            _before = vertical ? parent.ColumnDefinitions[definition].ActualWidth : parent.RowDefinitions[definition].ActualHeight;
            _dragging = CapturePointer(e.Pointer); e.Handled = true;
        };
        PointerMoved += (_, e) =>
        {
            if (!_dragging) return;
            var p = e.GetCurrentPoint(parent).Position;
            double total = vertical ? parent.ActualWidth : parent.ActualHeight;
            double size = Math.Clamp(_before + (vertical ? p.X - _start.X : p.Y - _start.Y), Math.Min(minimum, total * .3), Math.Max(minimum, total * maximumFraction));
            if (vertical) parent.ColumnDefinitions[definition].Width = new(size); else parent.RowDefinitions[definition].Height = new(size);
            e.Handled = true;
        };
        PointerReleased += (_, e) => { _dragging = false; ReleasePointerCapture(e.Pointer); Background = Studio.Brush(Studio.Line); e.Handled = true; };
        PointerCaptureLost += (_, _) => _dragging = false;
    }
}
