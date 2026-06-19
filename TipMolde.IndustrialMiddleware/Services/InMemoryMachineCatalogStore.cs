using System.Collections.Immutable;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class InMemoryMachineCatalogStore : IMachineCatalogStore
{
    private ImmutableArray<MachineCatalogTarget> _targets = ImmutableArray<MachineCatalogTarget>.Empty;

    public IReadOnlyList<MachineCatalogTarget> GetTargets() => _targets;

    public void Replace(IEnumerable<MachineCatalogTarget> targets)
    {
        _targets = targets.ToImmutableArray();
    }
}
