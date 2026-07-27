using System.Globalization;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Ping;

internal sealed class PingQueryHandler(IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PingQuery, string>
{
    public Task<Result<string>> Handle(PingQuery query, CancellationToken cancellationToken)
    {
        string timestamp = dateTimeProvider.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        return Task.FromResult(Result.Success($"pong {timestamp}"));
    }
}
