namespace Tenebit.Domain.Common;

/// <summary>Próba zapisu wiersza należącego do innej organizacji niż tenant bieżącego żądania. Zgłaszany przez
/// <c>TenebitDbContext</c> tuż przed wysłaniem zmian do bazy - globalny filtr zapytań chroni wyłącznie odczyt,
/// więc bez tego wyjątku encja ostemplowana cudzym OrganizationId trafiłaby do bazy niezauważona.</summary>
public sealed class CrossTenantWriteException : Exception
{
    public CrossTenantWriteException(string message) : base(message)
    {
    }
}
