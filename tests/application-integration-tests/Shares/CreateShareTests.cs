using Application.Files.Create;
using Application.IntegrationTests.Infrastructure;
using Application.Shares.Create;
using Domain.Shares;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Application.IntegrationTests.Shares;

public class CreateShareTests : BaseIntegrationTest
{
    public CreateShareTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateShare_ShouldReturnFailure_WhenOwnerDoesNotExist()
    {
        // Arrange
        var missingOwnerId = Guid.NewGuid();
        var command = new CreateShareCommand(
            missingOwnerId,
            ConfiguredTtlMinutes: 60,
            Files: [new FileUpload("docs/report.pdf", Size: 1024, ContentType: "application/pdf")]);

        // Act
        Result<CreateShareResponse> result =
            await CreateShareHandler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(UserErrors.NotFound(missingOwnerId));
    }

    [Fact]
    public async Task CreateShare_ShouldPersistShareAndFiles_WhenOwnerExists()
    {
        // Arrange
        User owner = await SeedOwnerAsync();

        const int ttlMinutes = 120;
        FileUpload[] files =
        [
            new FileUpload("docs/report.pdf", Size: 2048, ContentType: "application/pdf"),
            new FileUpload("images/logo.png", Size: 512, ContentType: "image/png")
        ];

        var command = new CreateShareCommand(owner.Id, ttlMinutes, files);

        // Act
        Result<CreateShareResponse> result =
            await CreateShareHandler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShareId.ShouldNotBe(Guid.Empty);

        // One presigned upload URL is returned per requested file, matched by path.
        result.Value.Files.Count.ShouldBe(files.Length);
        result.Value.Files.Select(f => f.RelativePath)
            .ShouldBe(files.Select(f => f.RelativePath), ignoreOrder: true);
        result.Value.Files.ShouldAllBe(f => f.UploadUrl.AbsoluteUri.Length > 0);

        // The share is persisted with the derived aggregate values and pending status.
        Share? share = await DbContext.Shares
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.Id == result.Value.ShareId,
                TestContext.Current.CancellationToken);

        share.ShouldNotBeNull();
        share.OwnerUserId.ShouldBe(owner.Id);
        share.Status.ShouldBe(ShareStatus.Pending);
        share.ConfiguredTtlMinutes.ShouldBe(ttlMinutes);
        share.FileCount.ShouldBe(files.Length);
        share.TotalBytes.ShouldBe(files.Sum(f => (long)f.Size));
        share.ExpiresAt.ShouldBe(share.CreatedAt.AddMinutes(ttlMinutes));

        // A file row is persisted for every requested upload.
        List<Domain.Files.File> persistedFiles = await DbContext.Files
            .AsNoTracking()
            .Where(f => f.ShareId == result.Value.ShareId)
            .ToListAsync(TestContext.Current.CancellationToken);

        persistedFiles.Count.ShouldBe(files.Length);
        persistedFiles.Select(f => f.RelativePath)
            .ShouldBe(files.Select(f => f.RelativePath), ignoreOrder: true);
    }

    private async Task<User> SeedOwnerAsync()
    {
        var owner = new User(Guid.NewGuid(), "Test Owner", "owner@share.test")
        {
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Users.Add(owner);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return owner;
    }
}
