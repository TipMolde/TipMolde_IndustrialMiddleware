using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineStateStore
{
    Task<MachineStateSnapshot?> GetAsync(string machineIp, CancellationToken cancellationToken);

    Task UpsertAsync(MachineStateSnapshot snapshot, CancellationToken cancellationToken);
}
