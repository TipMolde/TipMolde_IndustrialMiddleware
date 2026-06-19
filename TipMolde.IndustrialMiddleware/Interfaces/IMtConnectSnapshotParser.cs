using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMtConnectSnapshotParser
{
    MtConnectSnapshot Parse(string machineIp, string xmlPayload, DateTimeOffset receivedAt);
}
