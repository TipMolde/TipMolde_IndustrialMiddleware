using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IBackendClient
{
    Task SendEventsAsync(IEnumerable<MachineEventDto> events, CancellationToken cancellationToken);
}
