namespace Share.Application.Abstractions.Progress;

/// <summary>
/// The reporter used when the caller asked for no progress — a redirected terminal, a
/// script, a test. Lets the handler report unconditionally instead of guarding every call.
/// </summary>
public sealed class NullUploadProgressReporter : IUploadProgressReporter
{
    private NullUploadProgressReporter()
    {
        // Nothing to construct: the type is stateless and exists once.
    }

    public static NullUploadProgressReporter Instance { get; } = new();

    public void Starting(int fileCount, long totalBytes)
    {
        // Deliberately empty: nothing is watching.
    }

    public void FileStarting(string relativePath, long sizeInBytes)
    {
        // Deliberately empty: nothing is watching.
    }

    public void FileProgress(long bytesUploaded)
    {
        // Deliberately empty: nothing is watching.
    }

    public void FileCompleted(string relativePath)
    {
        // Deliberately empty: nothing is watching.
    }
}
