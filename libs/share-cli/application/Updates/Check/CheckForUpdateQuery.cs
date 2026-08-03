using Share.Application.Abstractions.Messaging;
using Share.Domain.Updates;

namespace Share.Application.Updates.Check;

/// <summary>
/// Reports what moving to a release would do, without changing anything.
/// </summary>
/// <param name="RequestedVersion">
/// The release to report on. <see langword="null"/> means the newest stable one, which is
/// what <c>share update --check</c> asks for.
/// </param>
public sealed record CheckForUpdateQuery(SemanticVersion? RequestedVersion)
    : IQuery<UpdateCheckResponse>;
