using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineCatalogClient
{
    Task<IReadOnlyList<MachineCatalogTarget>> GetMachineTargetsAsync(CancellationToken cancellationToken);
}
