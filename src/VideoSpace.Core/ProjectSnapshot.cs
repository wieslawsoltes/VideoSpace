using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoSpace.Core;

public static class ProjectSnapshot
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 128,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Write(VideoProject project) => JsonSerializer.Serialize(project, Options);
    public static VideoProject Read(string json)
    {
        if (json.Length > 32 * 1024 * 1024) throw new InvalidDataException("Project manifest exceeds 32 MB.");
        var project = JsonSerializer.Deserialize<VideoProject>(json, Options) ?? throw new InvalidDataException("Empty project.");
        ProjectValidation.Validate(project);
        return project;
    }
    public static VideoProject Clone(VideoProject project) => Read(Write(project));
    public static TimelineClip CloneClip(TimelineClip clip) => JsonSerializer.Deserialize<TimelineClip>(JsonSerializer.Serialize(clip, Options), Options)!;
}
