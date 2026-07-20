using Application.Abstractions.Data;
using Application.Files.Create;
using Application.Shares.Create;
using Application.Shares.Finalize;
using Application.UnitTests.Shares;
using Application.UnitTests.Users;
using Domain.Files;
using Domain.Shares;
using Domain.Users;
using NSubstitute;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Application.UnitTests;

public class ShareTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly CreateShareCommand Command = new(
        Guid.NewGuid(),
        60,
        []);

    private readonly CreateShareCommandHandler _handler;
    private readonly FinalizeShareCommandHandler _finalizeHandler;

    private readonly IShareRepository _shareRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStorageService _storageService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ShareTests()
    {
        _shareRepository = Substitute.For<IShareRepository>();
        _fileRepository = Substitute.For<IFileRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _storageService = Substitute.For<IStorageService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _dateTimeProvider.UtcNow.Returns(UtcNow);

        // Default: hand back a valid presigned URL so the handler can construct a Uri.
        _storageService
            .GeneratePresignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => $"https://uploads.example/{callInfo.ArgAt<string>(0)}");

        _handler = new CreateShareCommandHandler(
            _shareRepository,
            _fileRepository,
            _userRepository,
            _storageService,
            _dateTimeProvider,
            _unitOfWork);

        _finalizeHandler = new FinalizeShareCommandHandler(
            _shareRepository,
            _dateTimeProvider,
            _unitOfWork);
    }

    private static CreateShareCommand CommandFor(User owner, params FileUpload[] files) =>
        new(owner.Id, 60, files);

    private User ArrangeExistingOwner()
    {
        User owner = UserData.Create();
        _userRepository
            .GetByIdAsync(owner.Id, Arg.Any<CancellationToken>())
            .Returns(owner);
        return owner;
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenUserIsNull()
    {
        _userRepository
            .GetByIdAsync(Command.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Result<CreateShareResponse> result = await _handler.Handle(Command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(Command.OwnerUserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenOwnerExists()
    {
        User owner = ArrangeExistingOwner();
        CreateShareCommand command = CommandFor(
            owner,
            new FileUpload("docs/report.pdf", 1024, "application/pdf"));

        Result<CreateShareResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShareId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_Should_PersistShareWithExpectedState()
    {
        User owner = ArrangeExistingOwner();
        CreateShareCommand command = CommandFor(
            owner,
            new FileUpload("a.txt", 100, "text/plain"),
            new FileUpload("nested/b.bin", 200, null));

        Share? added = null;
        _shareRepository.Add(Arg.Do<Share>(s => added = s));

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        added.ShouldNotBeNull();
        added!.OwnerUserId.ShouldBe(owner.Id);
        added.Status.ShouldBe(ShareStatus.Pending);
        added.CreatedAt.ShouldBe(UtcNow);
        added.UpdatedAt.ShouldBe(UtcNow);
        added.ExpiresAt.ShouldBe(UtcNow.AddMinutes(command.ConfiguredTtlMinutes));
        added.ConfiguredTtlMinutes.ShouldBe(command.ConfiguredTtlMinutes);
        added.FileCount.ShouldBe(2);
        added.TotalBytes.ShouldBe(300);
    }

    [Fact]
    public async Task Handle_Should_AddAFilePerUpload_AndSaveOnce()
    {
        User owner = ArrangeExistingOwner();
        CreateShareCommand command = CommandFor(
            owner,
            new FileUpload("a.txt", 100, "text/plain"),
            new FileUpload("nested/b.bin", 200, null));

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        _fileRepository.Received(2).Add(Arg.Any<Domain.Files.File>());
        _shareRepository.Received(1).Add(Arg.Any<Share>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAPresignedUploadUrlPerFile()
    {
        User owner = ArrangeExistingOwner();
        var upload = new FileUpload("docs/report.pdf", 1024, "application/pdf");
        CreateShareCommand command = CommandFor(owner, upload);

        Result<CreateShareResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        FileUploadUrl url = result.Value.Files.ShouldHaveSingleItem();
        url.RelativePath.ShouldBe(upload.RelativePath);
        // Key is namespaced under the share id and normalised to forward slashes.
        url.UploadUrl.ShouldBe(new Uri($"https://uploads.example/{result.Value.ShareId}/docs/report.pdf"));
    }

    [Fact]
    public async Task Finalize_Should_MarkShareFinalized_AndSave_WhenPending()
    {
        Share share = ShareData.Create();
        share.Status = ShareStatus.Pending;
        _shareRepository
            .GetByIdAsync(share.Id, Arg.Any<CancellationToken>())
            .Returns(share);

        Result result = await _finalizeHandler.Handle(
            new FinalizeShareCommand(share.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        share.Status.ShouldBe(ShareStatus.Finalized);
        share.UpdatedAt.ShouldBe(UtcNow);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalize_Should_ReturnConflict_AndNotSave_WhenAlreadyFinalized()
    {
        Share share = ShareData.Create();
        share.Status = ShareStatus.Finalized;
        _shareRepository
            .GetByIdAsync(share.Id, Arg.Any<CancellationToken>())
            .Returns(share);

        Result result = await _finalizeHandler.Handle(
            new FinalizeShareCommand(share.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ShareErrors.AlreadyFinalized(share.Id));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
