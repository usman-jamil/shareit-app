namespace Share.Domain.Updates;

/// <summary>
/// Where the CLI looks for its own releases. The single source of these values — the bound
/// options read from here, so there is no second copy to drift.
/// </summary>
/// <remarks>
/// The repository is public, so the release listing is fetched unauthenticated. GitHub
/// rate-limits anonymous callers per IP, which is ample for a command a user runs by hand.
/// </remarks>
public static class UpdateDefaults
{
    public const string RepositoryOwner = "usman-jamil";

    public const string RepositoryName = "shareit-app";

    /// <summary>
    /// Releases of the CLI are tagged <c>sharecli-1.2.3</c>. The prefix is what separates
    /// them from any other release published in the same repository.
    /// </summary>
    public const string TagPrefix = "sharecli-";

    public const int TimeoutSeconds = 30;

    public static Uri ApiBaseUrl { get; } = new("https://api.github.com/");
}
