using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IProtocolDetectionService
{
    Task<ProtocolDetectionResult> DetectAsync(ProtocolDetectionRequest request);
}
