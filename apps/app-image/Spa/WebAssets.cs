using Microsoft.Extensions.FileProviders;

namespace AppImage.Host.Spa;

/// <summary>
/// The built React application on disk: the directory it lives in, the file provider static files
/// are served through, and the SPA document handed back for client-side routes.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="IDisposable"/>. The underlying <see cref="PhysicalFileProvider"/>
/// only allocates OS watchers when something calls <c>Watch</c>, which nothing here does, and the
/// instance is created during startup validation — before the DI container exists — and lives for
/// the lifetime of the process.
/// </remarks>
internal sealed class WebAssets
{
    public const string IndexFileName = "index.html";

    /// <summary>
    /// Directory holding fingerprinted build output. Vite names these files
    /// <c>assets/[name].[hash].[ext]</c>, so their contents can be cached indefinitely.
    /// </summary>
    public const string AssetsDirectoryName = "assets";

    public WebAssets(string rootPath)
    {
        RootPath = rootPath;
        IndexPath = Path.Combine(rootPath, IndexFileName);
        FileProvider = new PhysicalFileProvider(rootPath);
    }

    public string RootPath { get; }

    public string IndexPath { get; }

    public IFileProvider FileProvider { get; }

    /// <summary>
    /// Re-checked rather than cached, so a swapped or unmounted web root shows up in the readiness
    /// probe without needing a restart.
    /// </summary>
    public bool IndexExists => File.Exists(IndexPath);
}
