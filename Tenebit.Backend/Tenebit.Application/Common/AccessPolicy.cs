using Tenebit.Application.Abstractions;

namespace Tenebit.Application.Common;

public static class AccessPolicy
{
    public static Result EnsureAnyRole(ICurrentUser currentUser, params string[] roles)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure(Error.Unauthorized());
        }

        if (roles.Length == 0 || currentUser.HasAnyRole(roles))
        {
            return Result.Success();
        }

        SecurityTelemetry.AuthorizationDenied();
        return Result.Failure(Error.Forbidden());
    }
}
