namespace TipMolde.IndustrialMiddleware.Models;

public sealed record OpcUaSimulationCommand(
    string? State = null,
    bool? Active = null,
    string? Program = null,
    int? Counter = null,
    string? Alarm = null,
    string? OperatorCode = null,
    string? PartCode = null,
    string? MoldCode = null,
    bool IncrementCounter = false,
    bool ClearAlarm = false,
    bool ClearContext = false);
