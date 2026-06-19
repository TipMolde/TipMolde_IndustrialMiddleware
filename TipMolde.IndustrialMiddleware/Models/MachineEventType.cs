namespace TipMolde.IndustrialMiddleware.Models;

public enum MachineEventType
{
    WorkStarted = 0,
    WorkPaused = 1,
    WorkFinished = 2,
    AlarmTriggered = 3,
    ContextIncomplete = 4
}
