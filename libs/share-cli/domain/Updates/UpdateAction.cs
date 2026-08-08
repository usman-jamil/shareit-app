namespace Share.Domain.Updates;

/// <summary>
/// What moving to a particular release would do to the currently installed CLI.
/// </summary>
public enum UpdateAction
{
    /// <summary>The release is the version already installed.</summary>
    UpToDate = 0,

    /// <summary>The release is newer than the version installed.</summary>
    Upgrade = 1,

    /// <summary>The release is older than the version installed.</summary>
    Downgrade = 2
}
