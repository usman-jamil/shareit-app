using Share.Application.Abstractions.Progress;
using Share.Application.Abstractions.Storage;

namespace Share.Application.Shares.Create;

/// <summary>
/// Passes the byte counts <see cref="IFileUploader"/> reports on to the caller's progress
/// reporter.
/// </summary>
/// <remarks>
/// A direct adapter rather than <see cref="Progress{T}"/>, which hands each callback to the
/// thread pool: the values are a running total, so delivering two of them out of order
/// would make a progress bar jump backwards.
/// </remarks>
internal sealed class FileUploadProgress(IUploadProgressReporter reporter) : IProgress<long>
{
    public void Report(long value) => reporter.FileProgress(value);
}
