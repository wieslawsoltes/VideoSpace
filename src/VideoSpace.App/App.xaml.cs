using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using VideoSpace.Controls;
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Media;
using VideoSpace.Workbench;
using Windows.Storage;

namespace VideoSpace.App;

public partial class App : Application
{
    private Window? _window;
    private StudioWorkbench? _workbench;
    public App() { InitializeComponent(); RequestedTheme = ApplicationTheme.Dark; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "VideoSpace" };
        _window.Content = new Grid { Background = Studio.Brush(Studio.Background), Children = { new TextBlock { Text = "VideoSpace", FontSize = 28, Foreground = Studio.Brush("#C8C5FF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        _window.Activate();
        try
        {
            SKTypeface? typeface = null;
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/Inter.ttf"));
                using var input = await file.OpenStreamForReadAsync(); using var bytes = new MemoryStream(); await input.CopyToAsync(bytes);
                using var data = SKData.CreateCopy(bytes.ToArray()); typeface = SKTypeface.FromData(data);
                Studio.Font = new FontFamily("ms-appx:///Assets/Fonts/Inter.ttf#Inter"); if (typeface is not null) Studio.Typeface = typeface;
            }
            catch (Exception ex) { Console.WriteLine("Optional font: " + ex.Message); }
            var session = new EditorSession(SampleProject.Create());
            _workbench = new StudioWorkbench(session, new MediaServices()); if (typeface is not null) _workbench.SetTypeface(typeface);
            _window.Content = _workbench;
            _window.Closed += (_, _) => _workbench.Dispose();
            _window.Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) { session.Playing = false; session.Notify(); } };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _window.Content = new ScrollViewer { Content = new TextBlock { Text = "VideoSpace could not start.\n\n" + ex + "\n\nSaved local media has not been deleted. Reload to retry.", Foreground = Studio.Brush(Studio.Ink), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(32), FontSize = 14 } };
        }
    }
}
