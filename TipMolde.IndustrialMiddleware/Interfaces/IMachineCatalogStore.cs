using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineCatalogStore
{
    IReadOnlyList<MachineCatalogTarget> GetTargets();

    void Replace(IEnumerable<MachineCatalogTarget> targets);
}
