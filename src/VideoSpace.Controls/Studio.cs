using Microsoft.UI.Xaml.Automation;
using SkiaSharp;

namespace VideoSpace.Controls;

public static class Studio
{
    public const string Background = "#18191B", Panel = "#232427", Raised = "#2C2D30", Line = "#121315", Ink = "#D4D5D8", Muted = "#93969E", Accent = "#65A8F6";
    public static FontFamily Font { get; set; } = new("Arial");
    public static SKTypeface Typeface { get; set; } = SKTypeface.Default;
    public static SolidColorBrush Brush(string hex)
    {
        var c = VideoSpace.Rendering.Palette.Parse(hex);
        return new(Windows.UI.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
    }
    public static TextBlock Text(string text, double size = 12, string color = Ink) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontFamily = Font, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public static TextBox Input(string text = "", string placeholder = "") => new()
    {
        Text = text, PlaceholderText = placeholder, FontSize = 12, FontFamily = Font, Foreground = Brush(Ink), Background = Brush("#1B1C1F"), BorderBrush = Brush("#42454B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), MinHeight = 26, Padding = new Thickness(7, 3, 7, 3), SelectionHighlightColor = Brush("#32649A")
    };
    public static Grid Rows(params GridLength[] sizes)
    {
        var grid = new Grid(); foreach (var size in sizes) grid.RowDefinitions.Add(new() { Height = size }); return grid;
    }
    public static Grid Columns(params GridLength[] sizes)
    {
        var grid = new Grid(); foreach (var size in sizes) grid.ColumnDefinitions.Add(new() { Width = size }); return grid;
    }
    public static GridLength Star(double n = 1) => new(n, GridUnitType.Star);
    public static T At<T>(Grid grid, T child, int row = 0, int column = 0, int rowSpan = 1, int columnSpan = 1) where T : UIElement
    {
        Grid.SetRow(child, row); Grid.SetColumn(child, column); Grid.SetRowSpan(child, rowSpan); Grid.SetColumnSpan(child, columnSpan); grid.Children.Add(child); return child;
    }
    public static StackPanel Row(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children) panel.Children.Add(child); return panel;
    }
    public static void Name(DependencyObject control, string value) => AutomationProperties.SetName(control, value);
    public static Rect Bounds(FrameworkElement control)
    {
        try { var p = control.TransformToVisual(null).TransformPoint(new Point()); return new(p.X, p.Y, control.ActualWidth, control.ActualHeight); }
        catch { return new(); }
    }
}
