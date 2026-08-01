using NSubstitute;
using Share.Application.Abstractions.Api;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.FileSystem;
using Share.Application.Abstractions.Storage;
using Share.Application.Shares.Create;
using Share.Application.UnitTests.Api;
using Share.Domain.Api;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Shares;

/// <summary>
/// Covers the three-step upload conversation: what is sent to the API, which local file
/// goes to which presigned URL, and where each way it can fail leaves the run.
/// </summary>
public class CreateShareCommandHandlerTests
{
    private const string Root = "/work/report";

    private static readonly Guid ConfiguredUserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly LocalFile Readme = new("README.md", $"{Root}/README.md", 12, "text/markdown");
    private static readonly LocalFile Logo = new("docs/logo.png", $"{Root}/docs/logo.png", 2048, "image/png");

    private readonly IFileScanner _scanner = Substitute.For<IFileScanner>();
    private readonly IFileUploader _uploader = Substitute.For<IFileUploader>();
    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly IShareApiClient _api = ShareApiClientSubstitute.Create();

    public CreateShareCommandHandlerTests()
    {
        _scanner.Scan(Arg.Any<string>()).Returns(Result.Success(Scanned(Readme, Logo)));

        _uploader
            .UploadAsync(Arg.Any<Uri>(), Arg.Any<LocalFile>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ShareApiSettings(null, null, null, ConfiguredUserId)));

        Created(Readme, Logo);
    }

    private static ScannedDirectory Scanned(params LocalFile[] files) => new(Root, files);

    /// <summary>
    /// Arranges the API to hand back one upload target per file, in reverse order — the
    /// handler must match them by relative path rather than by position.
    /// </summary>
    private void Created(params LocalFile[] files)
    {
        FileUploadTarget[] targets =
        [
            .. files
                .Reverse()
                .Select(file => new FileUploadTarget(
                    file.RelativePath,
                    new Uri($"https://storage.example/{file.RelativePath}")))
        ];

        _api
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreatedShare(ShareApiData.ShareId, targets)));
    }

    private CreateShareCommandHandler Handler() => new(_scanner, _api, _uploader, _store);

    private Task<Result<CreateShareResponse>> Handle(Guid? ownerUserId = null, int? ttlMinutes = null) =>
        Handler().Handle(
            new CreateShareCommand(Root, ownerUserId, ttlMinutes),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_Should_SendEveryScannedFileAsTheManifest()
    {
        CreateShareRequest? sent = null;
        _api
            .CreateShareAsync(
                Arg.Do<CreateShareRequest>(request => sent = request),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreatedShare(
                ShareApiData.ShareId,
                [
                    new FileUploadTarget(Readme.RelativePath, new Uri("https://storage.example/readme")),
                    new FileUploadTarget(Logo.RelativePath, new Uri("https://storage.example/logo"))
                ])));

        await Handle(ttlMinutes: 30);

        sent.ShouldNotBeNull();
        sent!.OwnerUserId.ShouldBe(ConfiguredUserId);
        sent.ConfiguredTtlMinutes.ShouldBe(30);
        sent.Files.Select(file => file.RelativePath)
            .ShouldBe([Readme.RelativePath, Logo.RelativePath]);
        sent.Files.Select(file => file.Size).ShouldBe([12, 2048]);
        sent.Files.Select(file => file.ContentType).ShouldBe(["text/markdown", "image/png"]);
    }

    [Fact]
    public async Task Handle_Should_UploadEachFileToItsOwnUrl_MatchedByRelativePath()
    {
        await Handle();

        await _uploader.Received(1).UploadAsync(
            new Uri($"https://storage.example/{Readme.RelativePath}"),
            Readme,
            Arg.Any<CancellationToken>());
        await _uploader.Received(1).UploadAsync(
            new Uri($"https://storage.example/{Logo.RelativePath}"),
            Logo,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_FinalizeTheShare_AndReportWhatWasUploaded()
    {
        Result<CreateShareResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShareId.ShouldBe(ShareApiData.ShareId);
        result.Value.Root.ShouldBe(Root);
        result.Value.FileCount.ShouldBe(2);
        result.Value.TotalBytes.ShouldBe(2060);
        await _api.Received(1).FinalizeShareAsync(ShareApiData.ShareId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_PreferTheCommandsOwner_OverTheConfiguredOne()
    {
        var explicitOwner = Guid.NewGuid();
        CreateShareRequest? sent = null;
        _api
            .CreateShareAsync(
                Arg.Do<CreateShareRequest>(request => sent = request),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreatedShare(ShareApiData.ShareId, [])));

        await Handle(ownerUserId: explicitOwner);

        sent.ShouldNotBeNull();
        sent!.OwnerUserId.ShouldBe(explicitOwner);
        await _store.DidNotReceive().ReadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenNoOwnerIsConfiguredOrGiven()
    {
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(ShareApiSettings.Empty));

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.OwnerNotConfigured");
        await _api.DidNotReceive()
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_NotCallTheApi_WhenTheDirectoryCannotBeScanned()
    {
        Error error = Domain.Shares.ShareErrors.DirectoryNotFound(Root);
        _scanner.Scan(Arg.Any<string>()).Returns(Result.Failure<ScannedDirectory>(error));

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _api.DidNotReceive()
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenAFileIsTooLargeForTheManifest()
    {
        var huge = new LocalFile("huge.bin", $"{Root}/huge.bin", (long)int.MaxValue + 1, null);
        _scanner.Scan(Arg.Any<string>()).Returns(Result.Success(Scanned(huge)));

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.FileTooLarge");
        result.Error.Description.ShouldContain("huge.bin");
        await _api.DidNotReceive()
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_NotUpload_WhenTheShareCannotBeCreated()
    {
        Error error = ShareApiErrors.Unauthorized();
        _api.FailsCreateShare(error);

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _uploader.DidNotReceive()
            .UploadAsync(Arg.Any<Uri>(), Arg.Any<LocalFile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheApiOmitsAnUploadUrl()
    {
        _api
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreatedShare(
                ShareApiData.ShareId,
                [new FileUploadTarget(Readme.RelativePath, new Uri("https://storage.example/readme"))])));

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.MissingUploadUrl");
        result.Error.Description.ShouldContain(Logo.RelativePath);
        await _api.DidNotReceive().FinalizeShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_StopAtTheFirstFailedUpload_AndNotFinalize()
    {
        Error error = Domain.Shares.ShareErrors.UploadRejected(Readme.RelativePath, 403);
        _uploader
            .UploadAsync(Arg.Any<Uri>(), Readme, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _uploader.DidNotReceive()
            .UploadAsync(Arg.Any<Uri>(), Logo, Arg.Any<CancellationToken>());
        await _api.DidNotReceive().FinalizeShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnTheApiFailure_WhenFinalizeFails()
    {
        Error error = ShareApiErrors.Timeout();
        _api.FailsFinalizeShare(error);

        Result<CreateShareResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}
