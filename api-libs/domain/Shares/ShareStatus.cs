namespace Domain.Shares;

public static class ShareStatus
{
    /// <summary>
    /// The share has been created and presigned upload URLs issued, but the client
    /// has not yet confirmed the upload via the finalise endpoint.
    /// </summary>
    public const string Pending = "pending";
    public const string Finalized = "finalized";
}
