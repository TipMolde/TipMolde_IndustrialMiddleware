using System.Collections.Concurrent;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class InMemoryMachineStateStore : IMachineStateStore
{
    private readonly ConcurrentDictionary<string, MachineStateSnapshot> _states = new(StringComparer.OrdinalIgnoreCase);

    public Task<MachineStateSnapshot?> GetAsync(string machineIp, CancellationToken cancellationToken)
    {
        _states.TryGetValue(machineIp, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task UpsertAsync(MachineStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        _states[snapshot.MachineIp] = snapshot;
        return Task.CompletedTask;
    }
}
