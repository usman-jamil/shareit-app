using System.Globalization;
using Share.Api.Types;
using Share.Domain.Shares;
using ApiError = Share.Api.Types.Error;

namespace Share.Infrastructure.UnitTests.Api;

/// <summary>
/// The wire payloads the stubbed API replies with — built from the generated contract
/// types rather than hand-written JSON, so a regenerated contract breaks the build instead
/// of silently drifting from the tests.
/// </summary>
/// <remarks>
/// The ProblemDetails bodies are the exception: the API composes those from its own
/// <c>Error</c> and they are not part of the generated client, so they are written out
/// verbatim as the CLI actually receives them.
/// </remarks>
internal static class ShareApiResponses
{
    public static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ShareId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FileId = new("33333333-3333-3333-3333-333333333333");

    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly Uri UploadUrl = new("https://uploads.example/docs/report.pdf");

    public const string RelativePath = "docs/report.pdf";
    public const string ContentType = "application/pdf";
    public const int FileSize = 1024;
    public const int TtlMinutes = 60;

    private static ApiError NoError => new() { Code = string.Empty, Description = string.Empty };

    public static ResultOfUserResponse User() => new()
    {
        IsSuccess = true,
        Error = NoError,
        Value = new UserResponse
        {
            Id = UserId,
            Name = "Usman",
            Email = "test@test.com",
            CreatedAt = CreatedAt
        }
    };

    public static ResultOfCreateShareResponse CreatedShare() => new()
    {
        IsSuccess = true,
        Error = NoError,
        Value = new CreateShareResponse
        {
            ShareId = ShareId,
            Files = [new FileUploadUrl { RelativePath = RelativePath, UploadUrl = UploadUrl }]
        }
    };

    public static ResultOfShareResponse Share(string status = ShareStatus.Pending) => new()
    {
        IsSuccess = true,
        Error = NoError,
        Value = new ShareResponse
        {
            Id = ShareId,
            OwnerUserId = UserId,
            Status = status,
            CreatedAt = CreatedAt,
            UpdatedAt = CreatedAt,
            ExpiresAt = CreatedAt.AddMinutes(TtlMinutes),
            ConfiguredTtlMinutes = TtlMinutes,
            TotalBytes = FileSize,
            FileCount = 1,
            Files =
            [
                new FileResponse
                {
                    Id = FileId,
                    ShareId = ShareId,
                    RelativePath = RelativePath,
                    Sha256 = new string('0', 64),
                    ContentType = ContentType,
                    CreatedAt = CreatedAt,
                    UpdatedAt = CreatedAt,
                    Size = FileSize
                }
            ]
        }
    };

    /// <summary>
    /// A 2xx response whose envelope reports failure — the shape the CLI must degrade into
    /// a failure result rather than a wrong success.
    /// </summary>
    public static ResultOfShareResponse FailedShareEnvelope(string code, string description) => new()
    {
        IsSuccess = false,
        IsFailure = true,
        Error = new ApiError
        {
            Code = code,
            Description = description,
            Type = (int)SharedKernel.ErrorType.NotFound
        }
    };

    /// <summary>
    /// The ProblemDetails body the API returns for a handler-reported failure.
    /// </summary>
    public static string Problem(string title, string detail, int status) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""{"title":"{{title}}","detail":"{{detail}}","status":{{status}}}""");

    /// <summary>
    /// The ProblemDetails body for a validation failure, carrying every rule violation in
    /// the <c>errors</c> extension.
    /// </summary>
    public static string ValidationProblem(params (string Code, string Description)[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        string entries = string.Join(
            ',',
            errors.Select(error => string.Create(
                CultureInfo.InvariantCulture,
                $$"""{"code":"{{error.Code}}","description":"{{error.Description}}","type":{{(int)SharedKernel.ErrorType.Validation}}}""")));

        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"title":"Validation.General","detail":"One or more validation errors occurred","status":400,"errors":[{{entries}}]}""");
    }
}
