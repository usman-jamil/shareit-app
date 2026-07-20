namespace Infrastructure.Options;

public class ShareOptions
{
    public const string SectionName = "Share";

    public int ConfiguredTtlMinutes { get; init; }
}
