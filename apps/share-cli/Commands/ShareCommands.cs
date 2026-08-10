using System.Globalization;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Share.Application.Abstractions.Messaging;
using Share.Application.Shares.Create;
using Share.Cli.Rendering;
using SharedKernel;
using Spectre.Console;

namespace Share.Cli.Commands;

/// <summary>
/// Creating shares from the file system.
/// </summary>
public class ShareCommands(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Share a whole folder: every file under it is uploaded and the share is finalized.
    /// </summary>
    /// <param name="path">-p, Folder to share. Defaults to the current directory.</param>
    /// <param name="userId">-u, Owner of the share. Defaults to the configured user id.</param>
    /// <param name="ttlMinutes">-t, How long the share should live. Defaults to the API's own setting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("create")]
    public async Task<int> Create(
        string? path = null,
        Guid? userId = null,
        int? ttlMinutes = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<CreateShareCommand, CreateShareResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateShareCommand, CreateShareResponse>>();

        var command = new CreateShareCommand(
            path ?? Directory.GetCurrentDirectory(),
            userId,
            ttlMinutes);

        // A live bar needs a terminal it can redraw. Piped into a file or run in CI it would
        // write a screenful of escape codes per second, so there it simply uploads quietly.
        Result<CreateShareResponse> result = ConsoleOutput.CanRedraw
            ? await UploadWithProgressAsync(handler, command, cancellationToken)
            : await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return ConsoleOutput.Fail(result.Error);
        }

        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Runs the upload inside a live progress display, handing the handler somewhere to
    /// report to.
    /// </summary>
    private static async Task<Result<CreateShareResponse>> UploadWithProgressAsync(
        ICommandHandler<CreateShareCommand, CreateShareResponse> handler,
        CreateShareCommand command,
        CancellationToken cancellationToken)
    {
        return await AnsiConsole.Progress()
            // The finished bar is left on screen: it is the record of what was uploaded, and
            // the summary below it reads as a continuation of it.
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(UploadProgressDisplay.Columns())
            .StartAsync(async context =>
            {
                var display = new UploadProgressDisplay(context);

                Result<CreateShareResponse> result =
                    await handler.Handle(command with { Progress = display }, cancellationToken);

                if (result.IsSuccess)
                {
                    display.Complete();
                }

                return result;
            });
    }

    private static void Write(CreateShareResponse share)
    {
        Grid fields = ConsoleOutput.Fields();

        fields.AddRow(ConsoleOutput.Label("Share"), $"[bold]{ConsoleOutput.Value(share.ShareId.ToString())}[/]");
        fields.AddRow(ConsoleOutput.Label("Folder"), ConsoleOutput.Value(share.Root));
        fields.AddRow(
            ConsoleOutput.Label("Uploaded"),
            ConsoleOutput.Value(
                $"{share.FileCount.ToString(CultureInfo.InvariantCulture)} " +
                $"{(share.FileCount == 1 ? "file" : "files")}, {ConsoleOutput.Bytes(share.TotalBytes)}"));

        AnsiConsole.WriteLine();
        ConsoleOutput.Write(fields);
    }
}
