using Refit;
using Share.Api.Types;
using Share.Application.Abstractions.Api;
using Share.Domain.Api;
using SharedKernel;

namespace Share.Infrastructure.Api;

/// <summary>
/// Adapts the generated Refit client to <see cref="IShareApiClient"/>: unwraps the API's
/// <c>Result</c> envelope, maps wire types to Application models, and turns every
/// transport failure into a failure <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Cancellation is applied to the <i>await</i> rather than to the request, because the
/// generated interface does not take a <see cref="CancellationToken"/>. A cancelled token
/// therefore returns control immediately while the HTTP request finishes in the
/// background — correct for a CLI that is exiting, but it does not abort the request.
/// To cancel for real, regenerate with cancellation tokens (<c>.refitter</c> already sets
/// <c>useCancellationTokens</c>) and pass the token through the calls below.
/// </remarks>
internal sealed class ShareApiClient(IApiv1 api) : IShareApiClient
{
    public Task<Result<UserDetails>> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            () => api.Users(userId),
            ShareApiMappings.ToUserDetails,
            cancellationToken);

    public Task<Result<CreatedShare>> CreateShareAsync(
        CreateShareRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SendAsync(
            () => api.SharesPost(request.ToBody()),
            ShareApiMappings.ToCreatedShare,
            cancellationToken);
    }

    public Task<Result> FinalizeShareAsync(
        Guid shareId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => api.Finalize(shareId), cancellationToken);

    public Task<Result<ShareDetails>> GetShareAsync(
        Guid shareId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            () => api.SharesGet(shareId),
            ShareApiMappings.ToShareDetails,
            cancellationToken);

    private static async Task<Result<TValue>> SendAsync<TResponse, TValue>(
        Func<Task<TResponse>> send,
        Func<TResponse, Result<TValue>> onResponse,
        CancellationToken cancellationToken)
    {
        try
        {
            TResponse response = await send().WaitAsync(cancellationToken);

            return onResponse(response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The token is still live, so this was the HttpClient timeout firing rather
            // than the caller cancelling.
            return Result.Failure<TValue>(ShareApiErrors.Timeout());
        }
        catch (Exception exception) when (exception is ApiExceptionBase or HttpRequestException)
        {
            return Result.Failure<TValue>(ShareApiErrorMapper.FromException(exception));
        }
    }

    /// <summary>
    /// The same conversation for an endpoint that returns its payload bare rather than wrapped
    /// in the API's <c>Result</c> envelope: there is nothing to unwrap, so reaching the response
    /// at all is the success case and <paramref name="map"/> only translates it.
    /// </summary>
    /// <remarks>
    /// Overload resolution picks this over the envelope overload only when
    /// <paramref name="map"/> returns something other than a <see cref="Result{TValue}"/>;
    /// a mapper that already returns one binds to the envelope overload, which has the more
    /// specific parameter type.
    /// </remarks>
    private static Task<Result<TValue>> SendAsync<TResponse, TValue>(
        Func<Task<TResponse>> send,
        Func<TResponse, TValue> map,
        CancellationToken cancellationToken) =>
        SendAsync(
            send,
            response => Result.Success(map(response)),
            cancellationToken);

    private static async Task<Result> SendAsync(Func<Task> send, CancellationToken cancellationToken)
    {
        try
        {
            await send().WaitAsync(cancellationToken);

            return Result.Success();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(ShareApiErrors.Timeout());
        }
        catch (Exception exception) when (exception is ApiExceptionBase or HttpRequestException)
        {
            return Result.Failure(ShareApiErrorMapper.FromException(exception));
        }
    }
}
