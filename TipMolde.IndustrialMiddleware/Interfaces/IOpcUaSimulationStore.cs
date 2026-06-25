using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IOpcUaSimulationStore
{
    OpcUaSimulationState GetState();

    OpcUaSimulationState Apply(OpcUaSimulationCommand command);

    OpcUaSimulationState ApplyPreset(string preset);

    OpcUaSimulationState Reset();
}
