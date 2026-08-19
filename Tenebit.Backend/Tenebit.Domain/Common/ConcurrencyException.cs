namespace Tenebit.Domain.Common;

/// <summary>Konflikt współbieżności (optimistic concurrency) wykryty przy zapisie do bazy - zgłaszany przez
/// infrastrukturę (np. <c>TenebitDbContext</c>) zamiast wyjątku EF Core, żeby warstwa Application nie musiała
/// znać technologii dostępu do danych.</summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }
}
