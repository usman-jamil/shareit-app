namespace Share.Domain.Shares;

/// <summary>
/// The status values the API reports for a share. Mirrors the API's own vocabulary —
/// keep in step with it.
/// </summary>
public static class ShareStatus
{
    /// <summary>
    /// The share has been created and presigned upload URLs issued, but the client has
    /// not yet confirmed the upload via finalize.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// Every file has been uploaded and confirmed.
    /// </summary>
    public const string Finalized = "finalized";
}
