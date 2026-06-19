using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class MachineEventDetector : IEventDetector
{
    public IReadOnlyList<MachineEvent> Detect(NormalizedMachineData current, MachineStateSnapshot? previous)
    {
        var events = new List<MachineEvent>();

        var normalizedState = current.State.ToUpperInvariant();
        var previousState = previous?.State?.ToUpperInvariant();

        if (IsRunning(normalizedState) && !IsRunning(previousState))
        {
            events.Add(BuildEvent(current, MachineEventType.WorkStarted));
        }

        if (IsStopped(normalizedState) && IsRunning(previousState))
        {
            events.Add(BuildEvent(current, MachineEventType.WorkFinished));
        }

        if (current.AlarmCodes.Count > 0)
        {
            events.Add(BuildEvent(current, MachineEventType.AlarmTriggered));
        }

        if (current.Context is null || current.ContextCompleteness != "FULL")
        {
            events.Add(BuildEvent(current, MachineEventType.ContextIncomplete));
        }

        return events;
    }

    private static MachineEvent BuildEvent(NormalizedMachineData current, MachineEventType eventType)
        => new(
            current.MachineIp,
            eventType,
            current.OccurredAt,
            current.Context?.WorkOrderCode,
            current.Context?.OperationCode,
            current.Context?.PartCode,
            current.Context?.MoldCode,
            current.Program,
            current.Confidence,
            current.ContextCompleteness,
            $"{current.MachineIp}:{eventType}:{current.OccurredAt:O}");

    private static bool IsRunning(string? state)
        => state is "RUNNING" or "ACTIVE" or "EXECUTING" or "EM_CURSO";

    private static bool IsStopped(string? state)
        => state is "STOPPED" or "IDLE" or "PAUSED" or "CONCLUIDO";
}
