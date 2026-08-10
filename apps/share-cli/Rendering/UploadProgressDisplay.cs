using System.Globalization;
using Share.Application.Abstractions.Progress;
using Spectre.Console;

namespace Share.Cli.Rendering;

/// <summary>
/// Draws the upload phase of <c>share create</c> as a single bar measured in bytes, with the
/// file currently going up as its label.
/// </summary>
/// <remarks>
/// One bar for the whole share rather than one per file: a folder can hold thousands of
/// files, and what the user wants to know is how long the whole thing has left. The bar is
/// weighted by size, so a 2 GB video does not tick past at the same rate as a README.
/// <para>
/// Only ever used inside <c>AnsiConsole.Progress().StartAsync</c> — the
/// <see cref="ProgressContext"/> it is built from is alive for exactly that long.
/// </para>
/// </remarks>
internal sealed class UploadProgressDisplay(ProgressContext context) : IUploadProgressReporter
{
    private const int MaximumLabelLength = 34;
    private const int BarWidth = 26;

    private ProgressTask? _task;
    private long _totalBytes;
    private long _completedBytes;
    private long _currentFileSize;

    /// <summary>
    /// The columns to draw it with. Percentage and a bar for the shape of it, transferred and
    /// speed for the detail, remaining time because that is the actual question.
    /// </summary>
    /// <remarks>
    /// The bar is given a fixed width because it would otherwise be sized from whatever space
    /// the current file's name left over, and so would grow and shrink as the upload moved
    /// from one file to the next. The label is shortened for the same reason — see
    /// <see cref="MaximumLabelLength"/>.
    /// </remarks>
    public static ProgressColumn[] Columns() =>
    [
        new TaskDescriptionColumn { Alignment = Justify.Left },
        new ProgressBarColumn { Width = BarWidth },
        new PercentageColumn(),
        new DownloadedColumn(),
        new TransferSpeedColumn(),
        new RemainingTimeColumn(),
        new SpinnerColumn()
    ];

    public void Starting(int fileCount, long totalBytes)
    {
        // The total has to be positive: a share of nothing but empty files is legitimate, and
        // a bar of 0/0 renders as NaN.
        _totalBytes = Math.Max(totalBytes, 1);

        _task = context.AddTask(
            Label($"{fileCount.ToString(CultureInfo.InvariantCulture)} files"),
            new ProgressTaskSettings { MaxValue = _totalBytes });
    }

    public void FileStarting(string relativePath, long sizeInBytes)
    {
        _currentFileSize = sizeInBytes;
        _task?.Description = Label(relativePath);
    }

    public void FileProgress(long bytesUploaded) => Set(_completedBytes + bytesUploaded);

    public void FileCompleted(string relativePath)
    {
        // Counted from the file's own size rather than from the last report, so the bar lands
        // on the file boundary exactly even if the final read went unreported.
        _completedBytes += _currentFileSize;

        Set(_completedBytes);
    }

    /// <summary>
    /// Fills the bar. Called once the whole share has been finalized: the last file's bytes
    /// leave the process slightly before storage has acknowledged them, so a run that
    /// succeeded can otherwise finish a hair short of 100%.
    /// </summary>
    public void Complete() => Set(_totalBytes);

    /// <summary>
    /// Clamped rather than trusted: the count comes from bytes read out of the file, and a
    /// file that grew since it was scanned would otherwise push the bar past its end.
    /// </summary>
    private void Set(long value) => _task?.Value = Math.Min(value, _totalBytes);

    /// <summary>
    /// Shortened so a deep path cannot push the bar off the edge, and escaped so a path
    /// containing '[' is not read as markup.
    /// </summary>
    private static string Label(string text) =>
        Markup.Escape(ConsoleOutput.Shorten(text, MaximumLabelLength));
}
