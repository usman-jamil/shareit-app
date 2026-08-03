using Share.Application.Abstractions.Api;
using Share.Domain.Api;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Api;

/// <summary>
/// Pins the mocking seam handler tests will build on: a substitute
/// <see cref="IShareApiClient"/> hands back canned successes, and any single call can be
/// re-arranged to fail without disturbing the others.
/// </summary>
public class ShareApiClientSubstituteTests
{
    private readonly IShareApiClient _api = ShareApiClientSubstitute.Create();

    [Fact]
    public async Task Substitute_Should_SucceedForEveryOperation_ByDefault()
    {
        Result<UserDetails> user = await _api.GetUserAsync(
            ShareApiData.UserId,
            TestContext.Current.CancellationToken);
        Result<CreatedShare> created = await _api.CreateShareAsync(
            ShareApiData.CreateRequest(),
            TestContext.Current.CancellationToken);
        Result finalized = await _api.FinalizeShareAsync(
            ShareApiData.ShareId,
            TestContext.Current.CancellationToken);
        Result<ShareDetails> share = await _api.GetShareAsync(
            ShareApiData.ShareId,
            TestContext.Current.CancellationToken);

        user.IsSuccess.ShouldBeTrue();
        user.Value.Id.ShouldBe(ShareApiData.UserId);
        created.IsSuccess.ShouldBeTrue();
        created.Value.Files.ShouldHaveSingleItem().RelativePath.ShouldBe(ShareApiData.RelativePath);
        finalized.IsSuccess.ShouldBeTrue();
        share.IsSuccess.ShouldBeTrue();
        share.Value.IsFinalized.ShouldBeFalse();
    }

    [Fact]
    public async Task Substitute_Should_ReturnTheArrangedFailure_ForTheReArrangedCallOnly()
    {
        Error error = ShareApiErrors.Unreachable("connection refused");
        _api.FailsCreateShare(error);

        Result<CreatedShare> created = await _api.CreateShareAsync(
            ShareApiData.CreateRequest(),
            TestContext.Current.CancellationToken);
        Result<UserDetails> user = await _api.GetUserAsync(
            ShareApiData.UserId,
            TestContext.Current.CancellationToken);

        created.IsFailure.ShouldBeTrue();
        created.Error.ShouldBe(error);
        user.IsSuccess.ShouldBeTrue();
    }
}
