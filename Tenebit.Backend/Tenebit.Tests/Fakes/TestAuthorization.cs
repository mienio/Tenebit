using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;

namespace Tenebit.Tests.Fakes;

internal static class TestAuthorization
{
    public static AssetAuthorizationService Asset(
        IAssetRepository assets,
        ICurrentUser currentUser,
        InMemoryPersonRepository? people = null,
        InMemoryTeamRepository? teams = null) =>
        new(assets, new ManagerScopeService(people ?? new InMemoryPersonRepository(), teams ?? new InMemoryTeamRepository()), currentUser);
}
