namespace Share.Application.Abstractions.Progress;

/// <summary>
/// Reports how far the upload phase of <c>share create</c> has got, so the caller can show
/// it while the files are going up.
/// </summary>
/// <remarks>
/// Rendering is a presentation concern, so the reporter is supplied per invocation on
/// <c>CreateShareCommand</c> rather than injected: a progress display
/// exists only for the length of one command, and there is nothing for a second caller to
/// share. Callers that are not being watched by a human pass nothing and the handler uses
/// <see cref="NullUploadProgressReporter"/>.
/// <para>
/// Implementations are called from the thread doing the upload and must not throw — a
/// display that has broken is not a reason to fail an upload that is working.
/// </para>
/// </remarks>
public interface IUploadProgressReporter
{
    /// <summary>
    /// Called once before the first file, with what the whole share adds up to.
    /// </summary>
    void Starting(int fileCount, long totalBytes);

    /// <summary>
    /// Called before each file's bytes are sent.
    /// </summary>
    /// <param name="relativePath">The file's path within the share.</param>
    /// <param name="sizeInBytes">Its size, which is what it will report on completion.</param>
    void FileStarting(string relativePath, long sizeInBytes);

    /// <summary>
    /// How much of the file <see cref="FileStarting"/> last named has been sent, as a
    /// running total rather than an increment — a report that never arrives then cannot
    /// leave the display permanently short.
    /// </summary>
    void FileProgress(long bytesUploaded);

    /// <summary>
    /// Called once storage has accepted the file. Only ever called for a file that
    /// succeeded, so a reporter can treat it as "these bytes are done".
    /// </summary>
    void FileCompleted(string relativePath);
}
