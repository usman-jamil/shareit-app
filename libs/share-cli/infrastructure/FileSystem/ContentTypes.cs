namespace Share.Infrastructure.FileSystem;

/// <summary>
/// Maps a file extension to a MIME type. Deliberately a short list of types worth getting
/// right — a browser downloading a shared file behaves very differently for
/// <c>text/html</c> than for <c>application/octet-stream</c>. Anything not listed is
/// reported as unknown, and the uploader falls back to <c>application/octet-stream</c>.
/// </summary>
internal static class ContentTypes
{
    private static readonly Dictionary<string, string> ByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".7z"] = "application/x-7z-compressed",
            [".bmp"] = "image/bmp",
            [".css"] = "text/css",
            [".csv"] = "text/csv",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".gif"] = "image/gif",
            [".gz"] = "application/gzip",
            [".htm"] = "text/html",
            [".html"] = "text/html",
            [".ico"] = "image/x-icon",
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".js"] = "text/javascript",
            [".json"] = "application/json",
            [".log"] = "text/plain",
            [".md"] = "text/markdown",
            [".mp3"] = "audio/mpeg",
            [".mp4"] = "video/mp4",
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".svg"] = "image/svg+xml",
            [".tar"] = "application/x-tar",
            [".txt"] = "text/plain",
            [".wav"] = "audio/wav",
            [".webp"] = "image/webp",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".xml"] = "application/xml",
            [".yaml"] = "application/yaml",
            [".yml"] = "application/yaml",
            [".zip"] = "application/zip"
        };

    public static string? ForFile(string path)
    {
        string extension = Path.GetExtension(path);

        return extension.Length > 0 && ByExtension.TryGetValue(extension, out string? contentType)
            ? contentType
            : null;
    }
}
