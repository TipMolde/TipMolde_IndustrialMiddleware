using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineNormalizer
{
    NormalizedMachineData Normalize(RawMachineData rawData);
}
