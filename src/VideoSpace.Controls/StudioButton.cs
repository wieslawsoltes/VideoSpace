namespace VideoSpace.Controls;

/// <summary>Compact, keyboard-accessible command control with an original vector icon and a custom Uno template.</summary>
public sealed class StudioButton : Button
{
    private readonly IconView? _icon;
    private readonly TextBlock? _label;
    private bool? _active;
    public string CommandId { get; }
    public StudioButton(string label, Action action, string? icon = null, string? commandId = null)
    {
        CommandId = commandId ?? label; Style = (Style)Application.Current.Resources["StudioButtonStyle"]; FontFamily = Studio.Font;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) { _icon = new IconView { Glyph = icon }; content.Children.Add(_icon); }
        if (label.Length > 0) { _label = Studio.Text(label); content.Children.Add(_label); }
        Content = content; Studio.Name(this, CommandId); UiRegistry.Register(CommandId, this); ToolTipService.SetToolTip(this, CommandId); Click += (_, _) => action();
    }
    public void SetActive(bool value)
    {
        if (_active == value) return; _active = value;
        Background = Studio.Brush(value ? "#35435A" : "#00000000");
        if (_icon is not null) { _icon.Color = value ? Studio.Accent : Studio.Ink; _icon.Invalidate(); }
        if (_label is not null) _label.Foreground = Studio.Brush(value ? Studio.Accent : Studio.Ink);
    }
    public void SetText(string text) { if (_label is not null) _label.Text = text; }
    public void SetIcon(string glyph) { if (_icon is not null && _icon.Glyph != glyph) { _icon.Glyph = glyph; _icon.Invalidate(); } }
}
