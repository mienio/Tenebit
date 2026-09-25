namespace Tenebit.Domain.Assignments;

/// <summary>
/// Ogólny stan sprzętu przy zwrocie, wybierany z trzech opcji zamiast opisywany wyłącznie wolnym tekstem -
/// żeby dało się po nim filtrować i raportować. Szczegóły (np. "brak ładowarki") zostają w ReturnCondition.
/// Nie steruje dalszym losem aktywa - od tego jest ReturnResolution.
/// </summary>
public enum ReturnState
{
    Working = 0,
    Damaged = 1,
    Incomplete = 2
}
