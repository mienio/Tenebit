using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public static class StarterPersonRelationTypes
{
    private static readonly (string Pl, string En, int SortOrder)[] Definitions =
    [
        ("Pracownik", "Employee", 10),
        ("Kontraktor", "Contractor", 20),
        ("Dostawca", "Vendor", 30)
    ];

    public static IReadOnlyList<PersonRelationType> Create(Guid organizationId, string language) =>
        Definitions.Select(item => new PersonRelationType(organizationId, language == "pl" ? item.Pl : item.En, item.SortOrder)).ToList();
}
