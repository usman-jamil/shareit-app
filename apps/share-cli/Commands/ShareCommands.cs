using System.Globalization;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Share.Application.Abstractions.Messaging;
using Share.Application.Shares.Create;
using SharedKernel;

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

        Result<CreateShareResponse> result = await handler.Handle(
            new CreateShareCommand(path ?? Directory.GetCurrentDirectory(), userId, ttlMinutes),
            cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Write(result.Value);

        return 0;
    }

    private static void Write(CreateShareResponse share)
    {
        Console.WriteLine($"Share      {share.ShareId}");
        Console.WriteLine($"Folder     {share.Root}");
        Console.WriteLine(
            $"Uploaded   {share.FileCount.ToString(CultureInfo.InvariantCulture)} " +
            $"{(share.FileCount == 1 ? "file" : "files")}, {Describe(share.TotalBytes)}");
    }

    private static string Describe(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes.ToString(CultureInfo.InvariantCulture)} {units[unit]}"
            : $"{size.ToString("0.#", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private static int Fail(Error error)
    {
        // A validation failure carries one message per broken rule; show them all.
        if (error is ValidationError validationError)
        {
            foreach (Error inner in validationError.Errors)
            {
                Console.Error.WriteLine(inner.Description);
            }
        }
        else
        {
            Console.Error.WriteLine(error.Description);
        }

        return 1;
    }
}
