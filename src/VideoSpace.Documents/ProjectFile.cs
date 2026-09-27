using VideoSpace.Core;

namespace VideoSpace.Documents;

public static class ProjectFile
{
    public const string Extension = ".videospace";
    public static string Save(VideoProject project) { ProjectValidation.Validate(project); return ProjectSnapshot.Write(project); }
    public static VideoProject Open(string text) => ProjectSnapshot.Read(text);
}
