using System.Text.Json;
using VideoSpace.Core;
using VideoSpace.Effects;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoSpace.Media;

/// <summary>Platform boundary. Compressed browser media stays outside the managed heap.</summary>
public sealed class MediaServices : IDisposable
{
    private readonly Queue<MediaMessage> _queue = new();
    public bool Browser => OperatingSystem.IsBrowser();
#if __WASM__
    private static string Call(string operation, object? payload = null) => BrowserInterop.Call(operation, JsonSerializer.Serialize(payload, ProjectSnapshot.Options));
#endif
    public async Task InitializeAsync()
    {
#if __WASM__
        Call("init");
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
        await Task.CompletedTask;
    }
    public MediaMessage[] Poll()
    {
#if __WASM__
        return JsonSerializer.Deserialize<MediaMessage[]>(Call("drain"), ProjectSnapshot.Options) ?? [];
#else
        var result = _queue.ToArray(); _queue.Clear(); return result;
#endif
    }
    public async Task ImportAsync()
    {
#if __WASM__
        Call("pickMedia");
#else
        var picker = new FileOpenPicker();
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }) picker.FileTypeFilter.Add(ext);
        foreach (var file in await picker.PickMultipleFilesAsync())
        {
            using var stream = await file.OpenStreamForReadAsync();
            if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("Image imports are limited to 64 MB.");
            using var memory = new MemoryStream(); await stream.CopyToAsync(memory);
            _queue.Enqueue(new() { Type = "asset", Asset = new MediaAsset { Name = file.Name, Kind = MediaKind.Image, DurationSeconds = 10, Bin = "Imported", ByteLength = stream.Length }, Bytes = memory.ToArray() });
        }
#endif
        await Task.CompletedTask;
    }
    public async Task OpenAsync()
    {
#if __WASM__
        Call("pickProject");
#else
        var picker = new FileOpenPicker(); foreach (var ext in new[] { ".videospace", ".json", ".srt" }) picker.FileTypeFilter.Add(ext);
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        _queue.Enqueue(new() { Type = file.FileType == ".srt" ? "captions" : "project", Text = await FileIO.ReadTextAsync(file), Name = file.Name });
#endif
        await Task.CompletedTask;
    }
    public void Present(string id, FramePlan plan, double x, double y, double width, double height, bool playing, double rate, bool guides)
    {
#if __WASM__
        if (width >= 2 && height >= 2) Call("present", new { id, plan, x, y, width, height, playing, rate, guides });
#endif
    }
    public void HideMonitors(bool hidden)
    {
#if __WASM__
        Call("hide", hidden);
#endif
    }
    public void UnlockAudio()
    {
#if __WASM__
        Call("unlockAudio");
#endif
    }
    public async Task SaveTextAsync(string text, string name, bool autosave = false)
    {
#if __WASM__
        if (autosave) Call("autosave", text); else Call("downloadText", new { text, name });
#else
        if (autosave)
        {
            var temp = await ApplicationData.Current.LocalFolder.CreateFileAsync("recovery.tmp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temp, text); await temp.RenameAsync("recovery.videospace", NameCollisionOption.ReplaceExisting); return;
        }
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) };
        picker.FileTypeChoices.Add("VideoSpace export", new List<string> { Path.GetExtension(name) });
        var file = await picker.PickSaveFileAsync(); if (file is not null) await FileIO.WriteTextAsync(file, text);
#endif
        await Task.CompletedTask;
    }
    public async Task SaveBytesAsync(byte[] bytes, string name)
    {
#if __WASM__
        Call("downloadBase64", new { bytes = Convert.ToBase64String(bytes), name });
#else
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) };
        picker.FileTypeChoices.Add("VideoSpace export", new List<string> { Path.GetExtension(name) });
        var file = await picker.PickSaveFileAsync(); if (file is not null) await FileIO.WriteBytesAsync(file, bytes);
#endif
        await Task.CompletedTask;
    }
    public void ExportVideo(VideoProject project, int height = 720)
    {
#if __WASM__
        Call("exportVideo", new { project, height });
#else
        _queue.Enqueue(new() { Type = "error", Text = "Video encoding is available in the browser host. Native exports include projects, EDL, captions, generated PCM WAV and still images." });
#endif
    }
    public void ExportStill()
    {
#if __WASM__
        Call("exportStill");
#endif
    }
    public void CancelExport()
    {
#if __WASM__
        Call("cancelExport");
#endif
    }
    public void SetProject(VideoProject project)
    {
#if __WASM__
        Call("setProject", project);
#endif
    }
    public void Diagnostics(object state)
    {
#if __WASM__
        Call("diagnostics", state);
#endif
    }
    public void Dispose()
    {
#if __WASM__
        Call("dispose");
#endif
    }
}
