using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IEventDetector
{
    IReadOnlyList<MachineEvent> Detect(NormalizedMachineData current, MachineStateSnapshot? previous);
}
