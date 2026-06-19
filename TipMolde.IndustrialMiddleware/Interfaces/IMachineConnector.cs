using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineConnector
{
    string Protocol { get; }

    Task<IReadOnlyList<RawMachineData>> ReadAsync(CancellationToken cancellationToken);
}
