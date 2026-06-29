namespace Application.Abstractions.Authentication;

public interface IUserContext
{
    Guid UserId { get; }

    string ApiKey { get; }

    CancellationToken CancellationToken { get; }
}
