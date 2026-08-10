using System.Globalization;
using Share.Application.Abstractions.Progress;

namespace Share.Application.UnitTests.Shares;

/// <summary>
/// A progress reporter that writes down what it was told instead of drawing it, so a test can
/// assert on the sequence the handler produces.
/// </summary>
/// <remarks>
/// Records calls as text rather than as a set of counters: what matters about progress is the
/// order — a file reported complete before it started, or after the next one began, would be
/// a bar that jumps about.
/// </remarks>
internal sealed class RecordingUploadProgressReporter : IUploadProgressReporter
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Starting(int fileCount, long totalBytes) =>
        _calls.Add(
            $"starting {fileCount.ToString(CultureInfo.InvariantCulture)} " +
            $"{totalBytes.ToString(CultureInfo.InvariantCulture)}");

    public void FileStarting(string relativePath, long sizeInBytes) =>
        _calls.Add($"start {relativePath} {sizeInBytes.ToString(CultureInfo.InvariantCulture)}");

    public void FileProgress(long bytesUploaded) =>
        _calls.Add($"bytes {bytesUploaded.ToString(CultureInfo.InvariantCulture)}");

    public void FileCompleted(string relativePath) => _calls.Add($"done {relativePath}");
}
