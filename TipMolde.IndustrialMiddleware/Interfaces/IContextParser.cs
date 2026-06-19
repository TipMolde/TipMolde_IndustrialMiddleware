using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IContextParser
{
    MachineContext? Parse(string? rawMessage);
}
