using System.Globalization;
using NSubstitute;
using Share.Application.Ping;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Ping;

public class PingQueryHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly PingQueryHandler _handler;

    public PingQueryHandlerTests()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow);

        _handler = new PingQueryHandler(dateTimeProvider);
    }

    [Fact]
    public async Task Handle_Should_ReturnPongWithTheCurrentTimestamp()
    {
        Result<string> result = await _handler.Handle(
            new PingQuery(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe($"pong {UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
    }
}
