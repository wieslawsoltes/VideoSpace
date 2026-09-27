using System.Text.Json;
using VideoSpace.Core;
using VideoSpace.Effects;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoSpace.Media;

/// <summary>UI-safe platform boundary. Video bytes stay in the browser's media store, outside the managed heap.</summary>
public sealed class MediaServices : IDisposable
{
    private readonly Queue<MediaMessage> _queue = new();
    public bool Browser => OperatingSystem.IsBrowser();
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ProjectSnapshot.Options);
    private static string Quote(string value) => JsonSerializer.Serialize(value);
#if __WASM__
    private static string Js(string script) => global::Uno.Foundation.WebAssemblyRuntime.InvokeJS(script);
#endif
    public async Task InitializeAsync()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.init();'ok'");
#else
        try
        {
            var folder = ApplicationData.Current.LocalFolder;
            if (await folder.TryGetItemAsync("recovery.videospace") is StorageFile file)
                _queue.Enqueue(new() { Type = "project", Text = await FileIO.ReadTextAsync(file) });
        }
        catch (Exception ex) { _queue.Enqueue(new() { Type = "error", Text = "Recovery was not loaded: " + ex.Message }); }
        _queue.Enqueue(new() { Type = "ready" });
#endif
    }
    public MediaMessage[] Poll()
    {
#if __WASM__
        var json = Js("window.VideoSpaceMedia.drain()");
        return JsonSerializer.Deserialize<MediaMessage[]>(json, ProjectSnapshot.Options) ?? [];
#else
        var result = _queue.ToArray(); _queue.Clear(); return result;
#endif
    }
    public async Task ImportAsync()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.pickMedia();'ok'");
#else
        var picker = new FileOpenPicker();
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }) picker.FileTypeFilter.Add(ext);
        foreach (var file in await picker.PickMultipleFilesAsync())
        {
            using var stream = await file.OpenStreamForReadAsync();
            if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("Image imports are limited to 64 MB.");
            using var memory = new MemoryStream(); await stream.CopyToAsync(memory);
            var asset = new MediaAsset { Name = file.Name, Kind = MediaKind.Image, DurationSeconds = 10, Bin = "Imported", ByteLength = stream.Length };
            _queue.Enqueue(new() { Type = "asset", Asset = asset, Bytes = memory.ToArray() });
        }
#endif
    }
    public async Task OpenAsync()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.pickProject();'ok'");
#else
        var picker = new FileOpenPicker(); foreach (var ext in new[] { ".videospace", ".json", ".srt" }) picker.FileTypeFilter.Add(ext);
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        var text = await FileIO.ReadTextAsync(file); _queue.Enqueue(new() { Type = file.FileType == ".srt" ? "captions" : "project", Text = text, Name = file.Name });
#endif
    }
    public void Present(string id, FramePlan plan, double x, double y, double width, double height, bool playing, double rate, bool guides)
    {
#if __WASM__
        if (width < 2 || height < 2) return;
        Js("window.VideoSpaceMedia.present(" + Json(new { id, plan, x, y, width, height, playing, rate, guides }) + ");'ok'");
#endif
    }
    public void HideMonitors(bool hidden)
    {
#if __WASM__
        Js($"window.VideoSpaceMedia.hide({(hidden ? "true" : "false")});'ok'");
#endif
    }
    public void UnlockAudio()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.unlockAudio();'ok'");
#endif
    }
    public async Task SaveTextAsync(string text, string name, bool autosave = false)
    {
#if __WASM__
        if (autosave) Js("window.VideoSpaceMedia.autosave(" + Quote(text) + ");'ok'");
        else Js("window.VideoSpaceMedia.downloadText(" + Quote(text) + "," + Quote(name) + ");'ok'");
#else
        if (autosave)
        {
            var folder = ApplicationData.Current.LocalFolder;
            var temp = await folder.CreateFileAsync("recovery.tmp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temp, text); await temp.RenameAsync("recovery.videospace", NameCollisionOption.ReplaceExisting); return;
        }
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) };
        picker.FileTypeChoices.Add("VideoSpace export", new List<string> { Path.GetExtension(name) });
        var file = await picker.PickSaveFileAsync(); if (file is not null) await FileIO.WriteTextAsync(file, text);
#endif
    }
    public async Task SaveBytesAsync(byte[] bytes, string name)
    {
#if __WASM__
        Js("window.VideoSpaceMedia.downloadBase64(" + Quote(Convert.ToBase64String(bytes)) + "," + Quote(name) + ");'ok'");
#else
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) };
        picker.FileTypeChoices.Add("VideoSpace export", new List<string> { Path.GetExtension(name) });
        var file = await picker.PickSaveFileAsync(); if (file is not null) await FileIO.WriteBytesAsync(file, bytes);
#endif
    }
    public void ExportVideo(VideoProject project, int height = 720)
    {
#if __WASM__
        Js("window.VideoSpaceMedia.exportVideo(" + Json(project) + "," + height + ");'ok'");
#else
        _queue.Enqueue(new() { Type = "error", Text = "Video encoding is available in the browser host. The native host currently exports project, EDL, captions, PCM WAV and still images." });
#endif
    }
    public void ExportStill()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.exportStill();'ok'");
#endif
    }
    public void CancelExport()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.cancelExport();'ok'");
#endif
    }
    public void SetProject(VideoProject project)
    {
#if __WASM__
        Js("window.VideoSpaceMedia.setProject(" + Json(project) + ");'ok'");
#endif
    }
    public void Diagnostics(object state)
    {
#if __WASM__
        Js("window.videoSpaceState=" + Json(state) + ";'ok'");
#endif
    }
    public void Dispose()
    {
#if __WASM__
        Js("window.VideoSpaceMedia.dispose();'ok'");
#endif
    }
}
