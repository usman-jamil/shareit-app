using Application.IntegrationTests.Infrastructure;
using Application.Shares.GetById;
using Domain.Shares;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Application.IntegrationTests.Shares;

public class GetShareTests : BaseIntegrationTest
{
    private static readonly Guid ShareId = Guid.NewGuid();

    public GetShareTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetShare_ShouldReturnFailure_WhenShareIsNotFound()
    {
        // Arrange
        var query = new GetShareByIdQuery(ShareId);

        // Act
        Result<ShareResponse> result = await GetShareHandler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(ShareErrors.NotFound(ShareId));
    }
}
