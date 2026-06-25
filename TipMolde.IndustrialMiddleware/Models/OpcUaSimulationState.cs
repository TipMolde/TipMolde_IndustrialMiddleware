namespace TipMolde.IndustrialMiddleware.Models;

public sealed record OpcUaSimulationState(
    string MachineIp,
    string MachineCode,
    string EndpointUrl,
    string State,
    bool Active,
    string Program,
    int Counter,
    string? Alarm,
    string? OperatorCode,
    string? WorkOrderCode,
    string? OperationCode,
    string? PartCode,
    string? MoldCode,
    string Source,
    DateTimeOffset UpdatedAt)
{
    public string ToPayload()
    {
        var segments = new List<string>
        {
            $"STATE={State}",
            $"ACTIVE={Active.ToString().ToLowerInvariant()}",
            $"PROGRAM={Program}",
            $"COUNTER={Counter}"
        };

        if (!string.IsNullOrWhiteSpace(Alarm))
        {
            segments.Insert(4, $"ALARM={Alarm}");
        }

        AddIfPresent(segments, "OP", OperatorCode);
        AddIfPresent(segments, "OF", WorkOrderCode);
        AddIfPresent(segments, "OPERACAO", OperationCode);
        AddIfPresent(segments, "PECA", PartCode);
        AddIfPresent(segments, "MOLDE", MoldCode);
        segments.Add($"SOURCE={Source}");

        return string.Join(";", segments);
    }

    private static void AddIfPresent(List<string> segments, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            segments.Add($"{key}={value}");
        }
    }
}
