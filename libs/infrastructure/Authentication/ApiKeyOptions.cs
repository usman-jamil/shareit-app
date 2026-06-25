using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Authentication;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    [Required(AllowEmptyStrings = false)]
    public string Pepper { get; set; } = string.Empty;
}
