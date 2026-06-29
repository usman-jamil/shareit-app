using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required(AllowEmptyStrings = false)]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ServiceUrl { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string BucketName { get; set; } = string.Empty;
}
