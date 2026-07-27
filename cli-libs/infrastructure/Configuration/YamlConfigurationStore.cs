using System.Globalization;
using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;
using SharedKernel;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// Reads and writes <c>~/.share/config.yaml</c>. Writes go through a temporary file so an
/// interrupted save cannot leave a half-written config behind, and unrelated keys in the
/// file are carried over untouched.
/// </summary>
internal sealed class YamlConfigurationStore : IConfigurationStore
{
    private const string SectionName = "shareApi";
    private const string BaseUrlKey = "baseUrl";
    private const string TimeoutSecondsKey = "timeoutSeconds";

    private static readonly string Header =
        "# Share CLI configuration — the source of truth for how the CLI reaches the API." +
        Environment.NewLine +
        "# Edit by hand or run `share config set`. Note that `config set` rewrites this" +
        Environment.NewLine +
        "# file, which does not preserve comments you add." +
        Environment.NewLine;

    public YamlConfigurationStore() => Location = CliConfigurationPath.Resolve();

    public string Location { get; }

    public bool Exists => File.Exists(Location);

    public async Task<Result<ShareApiSettings>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Exists)
        {
            return Result.Success(ShareApiSettings.Empty);
        }

        string content;

        try
        {
            content = await File.ReadAllTextAsync(Location, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<ShareApiSettings>(
                ConfigurationErrors.Unreadable(Location, exception.Message));
        }

        IDictionary<string, string?> values;

        try
        {
            using var reader = new StringReader(content);
            values = YamlConfigurationParser.Parse(reader);
        }
        catch (YamlException exception)
        {
            return Result.Failure<ShareApiSettings>(
                ConfigurationErrors.Unparseable(Location, exception.Message));
        }

        return BuildSettings(values);
    }

    public async Task<Result> SaveAsync(
        ShareApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Result<YamlMappingNode> root = await LoadRootAsync(cancellationToken);

        if (root.IsFailure)
        {
            return Result.Failure(root.Error);
        }

        YamlMappingNode section = GetOrAddMapping(root.Value, SectionName);

        SetScalar(section, BaseUrlKey, settings.BaseUrl?.ToString());
        SetScalar(
            section,
            TimeoutSecondsKey,
            settings.TimeoutSeconds?.ToString(CultureInfo.InvariantCulture));

        if (section.Children.Count == 0)
        {
            root.Value.Children.Remove(new YamlScalarNode(SectionName));
        }

        return await WriteAsync(root.Value, cancellationToken);
    }

    private Result<ShareApiSettings> BuildSettings(IDictionary<string, string?> values)
    {
        Uri? baseUrl = null;

        if (TryGet(values, BaseUrlKey, out string? rawBaseUrl) &&
            !Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out baseUrl))
        {
            return Result.Failure<ShareApiSettings>(ConfigurationErrors.InvalidValue(
                Location,
                $"{SectionName}.{BaseUrlKey}",
                $"'{rawBaseUrl}' is not an absolute URL"));
        }

        int? timeoutSeconds = null;

        if (TryGet(values, TimeoutSecondsKey, out string? rawTimeout))
        {
            if (!int.TryParse(rawTimeout, CultureInfo.InvariantCulture, out int parsed))
            {
                return Result.Failure<ShareApiSettings>(ConfigurationErrors.InvalidValue(
                    Location,
                    $"{SectionName}.{TimeoutSecondsKey}",
                    $"'{rawTimeout}' is not a whole number"));
            }

            timeoutSeconds = parsed;
        }

        return Result.Success(new ShareApiSettings(baseUrl, timeoutSeconds));
    }

    private static bool TryGet(
        IDictionary<string, string?> values,
        string key,
        out string? value) =>
        values.TryGetValue($"{SectionName}:{key}", out value) &&
        !string.IsNullOrWhiteSpace(value);

    private async Task<Result<YamlMappingNode>> LoadRootAsync(CancellationToken cancellationToken)
    {
        if (!Exists)
        {
            return Result.Success(new YamlMappingNode());
        }

        try
        {
            string content = await File.ReadAllTextAsync(Location, cancellationToken);

            var yaml = new YamlStream();
            using var reader = new StringReader(content);
            yaml.Load(reader);

            return Result.Success(
                yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode mapping
                    ? mapping
                    : new YamlMappingNode());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<YamlMappingNode>(
                ConfigurationErrors.Unreadable(Location, exception.Message));
        }
        catch (YamlException exception)
        {
            // Refuse to overwrite a file we could not understand — it may be hand-edited
            // and contain more than we know about.
            return Result.Failure<YamlMappingNode>(
                ConfigurationErrors.Unparseable(Location, exception.Message));
        }
    }

    private async Task<Result> WriteAsync(YamlMappingNode root, CancellationToken cancellationToken)
    {
        string temporaryPath = Location + ".tmp";

        try
        {
            string? directory = Path.GetDirectoryName(Location);

            if (!string.IsNullOrEmpty(directory))
            {
                CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(temporaryPath, Serialize(root), cancellationToken);
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

    private static string Serialize(YamlMappingNode root)
    {
        var stream = new YamlStream(new YamlDocument(root));

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

    private static YamlMappingNode GetOrAddMapping(YamlMappingNode parent, string key)
    {
        var scalarKey = new YamlScalarNode(key);

        if (parent.Children.TryGetValue(scalarKey, out YamlNode? existing) &&
            existing is YamlMappingNode mapping)
        {
            return mapping;
        }

        var created = new YamlMappingNode();
        parent.Children.Remove(scalarKey);
        parent.Children.Add(scalarKey, created);

        return created;
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
