using System.Diagnostics;
using System.Net;
using Refit.Testing;
using Share.Api.Types;
using Share.Application.Abstractions.Api;
using Share.Domain.Api;
using Share.Domain.Shares;
using Share.Infrastructure.Api;
using SharedKernel;
using Shouldly;
using Xunit;
using CreateShareBody = Share.Api.Types.Request;

namespace Share.Infrastructure.UnitTests.Api;

/// <summary>
/// Covers the adapter that produces every <see cref="IShareApiClient"/> output: what the
/// CLI sends, what it makes of a successful response, and how each way the API can fail
/// turns into an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// The API is stubbed at the HTTP boundary with <see cref="StubHttp"/> (Refit.Testing), so
/// the real generated Refit client, its serializer and Refit's own exception behaviour are
/// all exercised — only the socket is replaced.
/// </remarks>
public class ShareApiClientTests
{
    private const string BaseUrl = "http://localhost:5080";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ShareApiClient ClientFor(StubHttp http) =>
        new(http.CreateGeneratedClient<IApiv1>(BaseUrl));

    private static CreateShareRequest CreateRequest() => new(
        ShareApiResponses.UserId,
        ShareApiResponses.TtlMinutes,
        [
            new FileUploadRequest(
                ShareApiResponses.RelativePath,
                ShareApiResponses.FileSize,
                ShareApiResponses.ContentType)
        ]);

    [Fact]
    public async Task GetUserAsync_Should_MapTheUser_WhenTheApiSucceeds()
    {
        using var http = new StubHttp
        {
            { Route.Get("/users/{userId}"), Reply.With(ShareApiResponses.User()) }
        };

        Result<UserDetails> result = await ClientFor(http).GetUserAsync(ShareApiResponses.UserId, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ShareApiResponses.UserId);
        result.Value.Name.ShouldBe("Usman");
        result.Value.Email.ShouldBe("test@test.com");
        result.Value.CreatedAt.ShouldBe(ShareApiResponses.CreatedAt);
        await http.VerifyAllCalledAsync();
    }

    [Fact]
    public async Task CreateShareAsync_Should_SendTheFileManifest()
    {
        using var http = new StubHttp
        {
            { Route.Post("/shares"), Reply.With(ShareApiResponses.CreatedShare()) }
        };

        await ClientFor(http).CreateShareAsync(CreateRequest(), Token);

        CreateShareBody? body = await http.LastRequestBodyAsync<CreateShareBody>();
        body.ShouldNotBeNull();
        body!.OwnerUserId.ShouldBe(ShareApiResponses.UserId);
        body.ConfiguredTtlMinutes.ShouldBe(ShareApiResponses.TtlMinutes);
        FileUpload file = body.Files.ShouldHaveSingleItem();
        file.RelativePath.ShouldBe(ShareApiResponses.RelativePath);
        file.Size.ShouldBe(ShareApiResponses.FileSize);
        file.ContentType.ShouldBe(ShareApiResponses.ContentType);
    }

    [Fact]
    public async Task CreateShareAsync_Should_MapAnUploadTargetPerFile()
    {
        using var http = new StubHttp
        {
            { Route.Post("/shares"), Reply.With(ShareApiResponses.CreatedShare()) }
        };

        Result<CreatedShare> result = await ClientFor(http).CreateShareAsync(CreateRequest(), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShareId.ShouldBe(ShareApiResponses.ShareId);
        FileUploadTarget target = result.Value.Files.ShouldHaveSingleItem();
        target.RelativePath.ShouldBe(ShareApiResponses.RelativePath);
        target.UploadUrl.ShouldBe(ShareApiResponses.UploadUrl);
    }

    [Fact]
    public async Task GetShareAsync_Should_MapTheShareAndItsFiles()
    {
        using var http = new StubHttp
        {
            {
                Route.Get("/shares/{shareId}"),
                Reply.With(ShareApiResponses.Share(ShareStatus.Finalized))
            }
        };

        Result<ShareDetails> result = await ClientFor(http).GetShareAsync(ShareApiResponses.ShareId, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ShareApiResponses.ShareId);
        result.Value.IsFinalized.ShouldBeTrue();
        result.Value.TotalBytes.ShouldBe(ShareApiResponses.FileSize);
        ShareFile file = result.Value.Files.ShouldHaveSingleItem();
        file.RelativePath.ShouldBe(ShareApiResponses.RelativePath);
        file.ContentType.ShouldBe(ShareApiResponses.ContentType);
    }

    [Fact]
    public async Task FinalizeShareAsync_Should_Succeed_WhenTheApiAccepts()
    {
        using var http = new StubHttp
        {
            { Route.Put("/shares/{shareId}/finalize"), Reply.Status(HttpStatusCode.NoContent) }
        };

        Result result = await ClientFor(http).FinalizeShareAsync(ShareApiResponses.ShareId, Token);

        result.IsSuccess.ShouldBeTrue();
        await http.VerifyAllCalledAsync();
    }

    [Fact]
    public async Task GetShareAsync_Should_ReturnNotFound_WhenTheApiReturns404()
    {
        using var http = new StubHttp
        {
            {
                Route.Get("/shares/{shareId}"),
                Reply.Json(
                    ShareApiResponses.Problem("Shares.NotFound", "The share was not found", 404),
                    HttpStatusCode.NotFound)
            }
        };

        Result<ShareDetails> result = await ClientFor(http).GetShareAsync(ShareApiResponses.ShareId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Shares.NotFound");
        result.Error.Description.ShouldBe("The share was not found");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task FinalizeShareAsync_Should_ReturnConflict_WhenTheShareIsAlreadyFinalized()
    {
        using var http = new StubHttp
        {
            {
                Route.Put("/shares/{shareId}/finalize"),
                Reply.Json(
                    ShareApiResponses.Problem("Shares.AlreadyFinalized", "Already finalized", 409),
                    HttpStatusCode.Conflict)
            }
        };

        Result result = await ClientFor(http).FinalizeShareAsync(ShareApiResponses.ShareId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Shares.AlreadyFinalized");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateShareAsync_Should_KeepEveryRuleViolation_WhenTheApiReturns400()
    {
        using var http = new StubHttp
        {
            {
                Route.Post("/shares"),
                Reply.Json(
                    ShareApiResponses.ValidationProblem(
                        ("Files.Empty", "At least one file is required"),
                        ("Ttl.OutOfRange", "TTL must be positive")),
                    HttpStatusCode.BadRequest)
            }
        };

        Result<CreatedShare> result = await ClientFor(http).CreateShareAsync(CreateRequest(), Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        ValidationError validationError = result.Error.ShouldBeOfType<ValidationError>();
        validationError.Errors.Select(error => error.Code)
            .ShouldBe(["Files.Empty", "Ttl.OutOfRange"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetUserAsync_Should_ReturnUnauthorized_WhenTheApiRejectsTheApiKey(
        HttpStatusCode statusCode)
    {
        using var http = new StubHttp
        {
            { Route.Get("/users/{userId}"), Reply.Status(statusCode) }
        };

        Result<UserDetails> result = await ClientFor(http).GetUserAsync(ShareApiResponses.UserId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ShareApiErrors.Unauthorized());
    }

    [Fact]
    public async Task GetUserAsync_Should_ReturnUnexpected_WhenTheApiFailsWithoutProblemDetails()
    {
        using var http = new StubHttp
        {
            { Route.Get("/users/{userId}"), Reply.Status(HttpStatusCode.InternalServerError) }
        };

        Result<UserDetails> result = await ClientFor(http).GetUserAsync(ShareApiResponses.UserId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ShareApi.Unexpected");
    }

    [Fact]
    public async Task GetShareAsync_Should_ReturnUnreachable_WhenTheRequestNeverGetsAResponse()
    {
        using var http = new StubHttp
        {
            {
                Route.Get("/shares/{shareId}"),
                Reply.From((Func<HttpRequestMessage, HttpResponseMessage>)(
                    _ => throw new HttpRequestException("Connection refused")))
            }
        };

        Result<ShareDetails> result = await ClientFor(http).GetShareAsync(ShareApiResponses.ShareId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ShareApi.Unreachable");
    }

    [Fact]
    public async Task GetShareAsync_Should_Fail_WhenAnOkResponseCarriesAFailedEnvelope()
    {
        using var http = new StubHttp
        {
            {
                Route.Get("/shares/{shareId}"),
                Reply.With(ShareApiResponses.FailedShareEnvelope("Shares.NotFound", "Gone"))
            }
        };

        Result<ShareDetails> result = await ClientFor(http).GetShareAsync(ShareApiResponses.ShareId, Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Shares.NotFound");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetShareAsync_Should_Propagate_WhenTheCallerCancelsAnInFlightRequest()
    {
        // The API never answers, so the call is still in flight when the caller cancels.
        using var http = new StubHttp
        {
            {
                Route.Get("/shares/{shareId}"),
                Reply.From((Func<HttpRequestMessage, Task<HttpResponseMessage>>)(async _ =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, Token);
                    throw new UnreachableException();
                }))
            }
        };
        using var cancelled = new CancellationTokenSource();

        Task<Result<ShareDetails>> pending = ClientFor(http)
            .GetShareAsync(ShareApiResponses.ShareId, cancelled.Token);
        await cancelled.CancelAsync();

        // The caller's own cancellation is the one failure that is not a Result — a CLI
        // being interrupted should unwind, not print an error.
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
    }
}
