namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MtConnectSnapshot(
    string MachineIp,
    string? Execution,
    string? Program,
    string? ActiveAlarms,
    int? M30Counter1,
    int? M30Counter2,
    int? LoopsRemaining,
    IReadOnlyList<string> EventLogEntries,
    DateTimeOffset ReceivedAt);
