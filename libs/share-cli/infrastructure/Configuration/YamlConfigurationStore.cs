using System.Globalization;
using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;
using SharedKernel;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// Reads and writes <c>~/.shareit/config.yaml</c>. Writes go through a temporary file so an
/// interrupted save cannot leave a half-written config behind; the workspaces the write is
/// not about, and any unrelated keys in the file, are carried over untouched.
/// </summary>
internal sealed class YamlConfigurationStore : IConfigurationStore
{
    private const string BaseUrlKey = "baseUrl";
    private const string ApiKeyKey = "apiKey";
    private const string UserIdKey = "userId";
    private const string TimeoutSecondsKey = "timeoutSeconds";

    private static readonly string Header =
        "# Share CLI configuration — the source of truth for how the CLI reaches the API." +
        Environment.NewLine +
        "# Each root-level section is a workspace; `active_workspace` picks the one in use." +
        Environment.NewLine +
        "# Edit by hand or run `share config set`. Note that `config set` rewrites this" +
        Environment.NewLine +
        "# file, which does not preserve comments you add." +
        Environment.NewLine;

    public YamlConfigurationStore() => Location = CliConfigurationPath.Resolve();

    public string Location { get; }

    public bool Exists => File.Exists(Location);

    public async Task<Result<ActiveWorkspace>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        Result<WorkspaceDocument> document = await LoadAsync(cancellationToken);

        if (document.IsFailure)
        {
            return Result.Failure<ActiveWorkspace>(document.Error);
        }

        string active = document.Value.ActiveWorkspace;

        // A file pointing at a workspace it does not define is a mistake worth stopping on:
        // silently defaulting would aim the next command at localhost with no key.
        if (!document.Value.Contains(active))
        {
            return Result.Failure<ActiveWorkspace>(
                ConfigurationErrors.WorkspaceNotFound(Location, active));
        }

        Result<ShareApiSettings> settings = BuildSettings(
            active,
            YamlConfigurationParser.Flatten(document.Value.Read(active)));

        return settings.IsFailure
            ? Result.Failure<ActiveWorkspace>(settings.Error)
            : Result.Success(new ActiveWorkspace(active, settings.Value));
    }

    public async Task<Result> SaveAsync(
        ShareApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Result<WorkspaceDocument> document = await LoadAsync(cancellationToken);

        if (document.IsFailure)
        {
            return Result.Failure(document.Error);
        }

        string active = document.Value.ActiveWorkspace;

        if (!document.Value.Contains(active))
        {
            return Result.Failure(ConfigurationErrors.WorkspaceNotFound(Location, active));
        }

        YamlMappingNode workspace = document.Value.GetOrAdd(active);

        SetScalar(workspace, BaseUrlKey, settings.BaseUrl?.ToString());
        SetScalar(workspace, ApiKeyKey, settings.ApiKey);
        SetScalar(workspace, UserIdKey, settings.UserId?.ToString());
        SetScalar(
            workspace,
            TimeoutSecondsKey,
            settings.TimeoutSeconds?.ToString(CultureInfo.InvariantCulture));

        return await WriteAsync(document.Value, cancellationToken);
    }

    public async Task<Result<WorkspaceList>> ListWorkspacesAsync(
        CancellationToken cancellationToken = default)
    {
        Result<WorkspaceDocument> document = await LoadAsync(cancellationToken);

        // Deliberately does not check that the active workspace exists: listing is how the
        // user diagnoses a file that names one that does not.
        return document.IsFailure
            ? Result.Failure<WorkspaceList>(document.Error)
            : Result.Success(
                new WorkspaceList(document.Value.ActiveWorkspace, document.Value.Workspaces));
    }

    public async Task<Result> CreateWorkspaceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        Result<WorkspaceDocument> document = await LoadAsync(cancellationToken);

        if (document.IsFailure)
        {
            return Result.Failure(document.Error);
        }

        if (document.Value.Contains(name))
        {
            return Result.Failure(ConfigurationErrors.WorkspaceAlreadyExists(Location, name));
        }

        document.Value.GetOrAdd(name);
        document.Value.SetActive(name);

        return await WriteAsync(document.Value, cancellationToken);
    }

    public async Task<Result> ActivateWorkspaceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        Result<WorkspaceDocument> document = await LoadAsync(cancellationToken);

        if (document.IsFailure)
        {
            return Result.Failure(document.Error);
        }

        if (!document.Value.Contains(name))
        {
            return Result.Failure(ConfigurationErrors.WorkspaceNotFound(Location, name));
        }

        document.Value.SetActive(name);

        return await WriteAsync(document.Value, cancellationToken);
    }

    private Result<ShareApiSettings> BuildSettings(
        string workspace,
        IDictionary<string, string?> values)
    {
        Uri? baseUrl = null;

        if (TryGet(values, BaseUrlKey, out string? rawBaseUrl) &&
            !Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out baseUrl))
        {
            return Result.Failure<ShareApiSettings>(ConfigurationErrors.InvalidValue(
                Location,
                $"{workspace}.{BaseUrlKey}",
                $"'{rawBaseUrl}' is not an absolute URL"));
        }

        int? timeoutSeconds = null;

        if (TryGet(values, TimeoutSecondsKey, out string? rawTimeout))
        {
            if (!int.TryParse(rawTimeout, CultureInfo.InvariantCulture, out int parsed))
            {
                return Result.Failure<ShareApiSettings>(ConfigurationErrors.InvalidValue(
                    Location,
                    $"{workspace}.{TimeoutSecondsKey}",
                    $"'{rawTimeout}' is not a whole number"));
            }

            timeoutSeconds = parsed;
        }

        // Nothing to validate: any non-blank string is a plausible key, and a wrong one is
        // the API's business — it comes back as a ShareApi.Unauthorized failure result.
        string? apiKey = TryGet(values, ApiKeyKey, out string? rawApiKey) ? rawApiKey : null;

        Guid? userId = null;

        if (TryGet(values, UserIdKey, out string? rawUserId))
        {
            if (!Guid.TryParse(rawUserId, CultureInfo.InvariantCulture, out Guid parsedUserId))
            {
                return Result.Failure<ShareApiSettings>(ConfigurationErrors.InvalidValue(
                    Location,
                    $"{workspace}.{UserIdKey}",
                    $"'{rawUserId}' is not a valid id"));
            }

            userId = parsedUserId;
        }

        return Result.Success(new ShareApiSettings(baseUrl, timeoutSeconds, apiKey, userId));
    }

    private static bool TryGet(
        IDictionary<string, string?> values,
        string key,
        out string? value) =>
        values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);

    private async Task<Result<WorkspaceDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!Exists)
        {
            return Result.Success(WorkspaceDocument.Empty());
        }

        try
        {
            string content = await File.ReadAllTextAsync(Location, cancellationToken);

            using var reader = new StringReader(content);

            return Result.Success(WorkspaceDocument.Load(reader));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<WorkspaceDocument>(
                ConfigurationErrors.Unreadable(Location, exception.Message));
        }
        catch (YamlException exception)
        {
            // Refuse to overwrite a file we could not understand — it may be hand-edited
            // and contain more than we know about.
            return Result.Failure<WorkspaceDocument>(
                ConfigurationErrors.Unparseable(Location, exception.Message));
        }
    }

    private async Task<Result> WriteAsync(
        WorkspaceDocument document,
        CancellationToken cancellationToken)
    {
        string temporaryPath = Location + ".tmp";

        try
        {
            string? directory = Path.GetDirectoryName(Location);

            if (!string.IsNullOrEmpty(directory))
            {
                CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(temporaryPath, Serialize(document), cancellationToken);
            RestrictToOwner(temporaryPath);

            File.Move(temporaryPath, Location, overwrite: true);

            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);

            return Result.Failure(ConfigurationErrors.Unwritable(Location, exception.Message));
        }
    }

    private static string Serialize(WorkspaceDocument document)
    {
        var stream = new YamlStream(new YamlDocument(document.ToYaml()));

        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);

        return Header + StripDocumentEndMarker(writer.ToString());
    }

    /// <summary>
    /// YamlDotNet always terminates a saved document with an explicit <c>...</c> marker.
    /// It is valid YAML but noise in a file users are meant to read and edit.
    /// </summary>
    private static string StripDocumentEndMarker(string yaml)
    {
        string trimmed = yaml.TrimEnd();
        int lastNewLine = trimmed.LastIndexOf('\n');
        string lastLine = lastNewLine < 0 ? trimmed : trimmed[(lastNewLine + 1)..];

        if (lastLine == "...")
        {
            trimmed = lastNewLine < 0 ? string.Empty : trimmed[..lastNewLine].TrimEnd();
        }

        return trimmed + Environment.NewLine;
    }

    private static void CreateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        if (!OperatingSystem.IsWindows())
        {
            // rwx------ : the config lives in the user's home directory and is theirs alone.
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void RestrictToOwner(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            // rw------- : Windows inherits the home directory's ACL instead.
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort only — the save has already failed and is being reported.
        }
    }

    private static void SetScalar(YamlMappingNode parent, string key, string? value)
    {
        var scalarKey = new YamlScalarNode(key);

        parent.Children.Remove(scalarKey);

        if (value is not null)
        {
            parent.Children.Add(scalarKey, new YamlScalarNode(value));
        }
    }
}
