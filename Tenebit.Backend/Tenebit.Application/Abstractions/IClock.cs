namespace Tenebit.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
