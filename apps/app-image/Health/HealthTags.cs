namespace AppImage.Host.Health;

/// <summary>
/// Splits liveness from readiness. <c>/health</c> runs no checks at all — answering it proves the
/// process is up and serving — while <c>/health/ready</c> runs everything tagged
/// <see cref="Ready"/>.
/// </summary>
internal static class HealthTags
{
    public const string Ready = "ready";
}
