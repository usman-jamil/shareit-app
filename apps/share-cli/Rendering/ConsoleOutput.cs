using System.Globalization;
using Spectre.Console;
using SharedKernel;

namespace Share.Cli.Rendering;

/// <summary>
/// The CLI's one way of writing to the terminal. Everything a human reads goes through
/// Spectre so it is styled the same way everywhere; everything that went wrong goes to
/// stderr, plainly.
/// </summary>
internal static class ConsoleOutput
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// A console of its own for failures. The shared <see cref="AnsiConsole"/> writes to
    /// stdout, which is what a caller redirects to a file or pipes into something else —
    /// errors have to stay out of it.
    /// </summary>
    private static readonly IAnsiConsole Stderr = AnsiConsole.Create(
        new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });

    /// <summary>
    /// Whether there is somebody to answer a question: a keyboard on one end, a terminal on
    /// the other. False under a pipe, in CI, and whenever input is redirected — which is
    /// exactly when a prompt would hang until the job timed out.
    /// </summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && AnsiConsole.Profile.Capabilities.Interactive;

    /// <summary>
    /// Whether the terminal can also be redrawn — cursor movement and colour, not just text.
    /// A selection list and a live progress bar both need it.
    /// </summary>
    /// <remarks>
    /// Checked separately because being interactive is not enough: <c>TERM=dumb</c>, and a
    /// Windows console without virtual terminal processing, can be typed into but not drawn
    /// on. Spectre throws rather than degrading when asked for a selection list there, so the
    /// caller has to know before it asks.
    /// </remarks>
    public static bool CanRedraw => IsInteractive && AnsiConsole.Profile.Capabilities.Ansi;

    /// <summary>
    /// Writes a failure to stderr and returns the exit code to leave with.
    /// </summary>
    public static int Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // A validation failure carries one message per broken rule; show them all.
        IEnumerable<string> messages = error is ValidationError validationError
            ? validationError.Errors.Select(inner => inner.Description)
            : [error.Description];

        foreach (string message in messages)
        {
            Stderr.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }

        return 1;
    }

    /// <summary>
    /// Writes something the user should see but that is not a failure — the command still
    /// succeeded. Goes to stderr so it cannot be mistaken for output.
    /// </summary>
    public static void Warn(string message) =>
        Stderr.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");

    public static void Success(string message) =>
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");

    /// <summary>
    /// A two-column grid of labels and values, which is what most of this CLI's output is.
    /// Values are escaped by the caller's use of <see cref="Value"/> or <see cref="Muted"/>.
    /// </summary>
    public static Grid Fields()
    {
        var grid = new Grid();

        grid.AddColumn(new GridColumn().PadRight(4));
        grid.AddColumn();

        return grid;
    }

    public static void Write(Grid fields) => AnsiConsole.Write(fields);

    public static string Label(string text) => $"[grey]{Markup.Escape(text)}[/]";

    public static string Value(string text) => Markup.Escape(text);

    public static string Muted(string text) => $"[dim]{Markup.Escape(text)}[/]";

    /// <summary>
    /// A byte count as a human reads it. Whole bytes below 1 KB, one decimal above.
    /// </summary>
    public static string Bytes(long bytes)
    {
        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < ByteUnits.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes.ToString(CultureInfo.InvariantCulture)} {ByteUnits[unit]}"
            : $"{size.ToString("0.#", CultureInfo.InvariantCulture)} {ByteUnits[unit]}";
    }

    /// <summary>
    /// Shortens a path to fit a fixed-width column, keeping the end: the file name says more
    /// than the directories above it.
    /// </summary>
    public static string Shorten(string path, int maximumLength) =>
        path.Length <= maximumLength
            ? path
            : "…" + path[^(maximumLength - 1)..];
}
