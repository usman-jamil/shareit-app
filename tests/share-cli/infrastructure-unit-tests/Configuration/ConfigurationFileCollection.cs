namespace Share.Infrastructure.UnitTests.Configuration;

/// <summary>
/// Both configuration-file test classes redirect <c>SHARE_CLI_CONFIG</c>, which is
/// process-wide. Sharing a collection keeps them off each other's environment — xUnit runs
/// classes in parallel otherwise, and they would take turns pointing the store at the wrong
/// file.
/// </summary>
public static class ConfigurationFileCollection
{
    public const string Name = "configuration file";
}
