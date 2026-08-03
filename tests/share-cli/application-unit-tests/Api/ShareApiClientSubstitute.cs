using NSubstitute;
using Share.Application.Abstractions.Api;
using SharedKernel;

namespace Share.Application.UnitTests.Api;

/// <summary>
/// A substitute <see cref="IShareApiClient"/> whose every operation succeeds with the
/// canned data in <see cref="ShareApiData"/>. Handler tests take one of these and
/// re-arrange only the call they care about — usually to make it fail — so a test reads as
/// the one thing it is about rather than as four lines of setup.
/// </summary>
/// <remarks>
/// Nothing in the Application layer calls the API yet. This exists so the first use case
/// that does can be tested the moment it lands, and so the mocking convention is settled
/// in one place instead of being reinvented per test class.
/// </remarks>
internal static class ShareApiClientSubstitute
{
    public static IShareApiClient Create()
    {
        IShareApiClient client = Substitute.For<IShareApiClient>();

        client
            .GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(ShareApiData.User(call.ArgAt<Guid>(0))));

        client
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(ShareApiData.CreatedShare()));

        client
            .FinalizeShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        client
            .GetShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(ShareApiData.Share(call.ArgAt<Guid>(0))));

        return client;
    }

    /// <summary>
    /// Makes <see cref="IShareApiClient.GetUserAsync"/> fail with <paramref name="error"/>.
    /// </summary>
    public static IShareApiClient FailsGetUser(this IShareApiClient client, Error error)
    {
        ArgumentNullException.ThrowIfNull(client);

        client
            .GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDetails>(error));

        return client;
    }

    /// <summary>
    /// Makes <see cref="IShareApiClient.CreateShareAsync"/> fail with <paramref name="error"/>.
    /// </summary>
    public static IShareApiClient FailsCreateShare(this IShareApiClient client, Error error)
    {
        ArgumentNullException.ThrowIfNull(client);

        client
            .CreateShareAsync(Arg.Any<CreateShareRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CreatedShare>(error));

        return client;
    }

    /// <summary>
    /// Makes <see cref="IShareApiClient.FinalizeShareAsync"/> fail with <paramref name="error"/>.
    /// </summary>
    public static IShareApiClient FailsFinalizeShare(this IShareApiClient client, Error error)
    {
        ArgumentNullException.ThrowIfNull(client);

        client
            .FinalizeShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        return client;
    }

    /// <summary>
    /// Makes <see cref="IShareApiClient.GetShareAsync"/> fail with <paramref name="error"/>.
    /// </summary>
    public static IShareApiClient FailsGetShare(this IShareApiClient client, Error error)
    {
        ArgumentNullException.ThrowIfNull(client);

        client
            .GetShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ShareDetails>(error));

        return client;
    }
}
