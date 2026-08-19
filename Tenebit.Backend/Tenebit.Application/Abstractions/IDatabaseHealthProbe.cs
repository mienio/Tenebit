namespace Tenebit.Application.Abstractions;

public interface IDatabaseHealthProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
