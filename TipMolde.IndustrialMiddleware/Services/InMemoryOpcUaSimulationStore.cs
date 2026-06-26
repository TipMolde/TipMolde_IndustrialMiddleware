using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class InMemoryOpcUaSimulationStore : IOpcUaSimulationStore
{
    private readonly object _gate = new();
    private readonly IndustrialMiddlewareOptions _options;
    private OpcUaSimulationState _state;

    public InMemoryOpcUaSimulationStore(IOptions<IndustrialMiddlewareOptions> options)
    {
        _options = options.Value;
        _state = BuildDefaultState(_options);
    }

    public OpcUaSimulationState GetState()
    {
        lock (_gate)
        {
            return _state;
        }
    }

    public OpcUaSimulationState Apply(OpcUaSimulationCommand command)
    {
        lock (_gate)
        {
            var counter = command.Counter ?? _state.Counter;
            if (command.IncrementCounter)
            {
                counter++;
            }

            _state = _state with
            {
                State = Normalize(command.State) ?? _state.State,
                Active = command.Active ?? _state.Active,
                Program = Normalize(command.Program) ?? _state.Program,
                Counter = counter,
                Alarm = command.ClearAlarm ? null : Normalize(command.Alarm) ?? _state.Alarm,
                OperatorCode = command.ClearContext ? null : Normalize(command.OperatorCode) ?? _state.OperatorCode,
                PartCode = command.ClearContext ? null : Normalize(command.PartCode) ?? _state.PartCode,
                MoldCode = command.ClearContext ? null : Normalize(command.MoldCode) ?? _state.MoldCode,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            return _state;
        }
    }

    public OpcUaSimulationState ApplyPreset(string preset)
    {
        var normalizedPreset = preset.Trim().ToUpperInvariant();

        return normalizedPreset switch
        {
            "IDLE" => Apply(new OpcUaSimulationCommand(State: "IDLE", Active: false, ClearAlarm: true)),
            "START" or "RUNNING" => Apply(new OpcUaSimulationCommand(State: "RUNNING", Active: true, IncrementCounter: true, ClearAlarm: true)),
            "PAUSE" or "PAUSED" => Apply(new OpcUaSimulationCommand(State: "PAUSED", Active: false)),
            "STOP" or "STOPPED" => Apply(new OpcUaSimulationCommand(State: "STOPPED", Active: false)),
            "FINISH" or "FINISHED" => Apply(new OpcUaSimulationCommand(State: "STOPPED", Active: false, IncrementCounter: true, ClearAlarm: true)),
            "ALARM" => Apply(new OpcUaSimulationCommand(State: "ALARM", Active: false, Alarm: "E101")),
            "CLEAR-ALARM" or "CLEARALARM" => Apply(new OpcUaSimulationCommand(ClearAlarm: true)),
            "NO-CONTEXT" or "NOCONTEXT" or "SEM-CONTEXTO" => Apply(new OpcUaSimulationCommand(ClearContext: true)),
            "DEFAULT-CONTEXT" or "CONTEXTO" => Apply(new OpcUaSimulationCommand(
                OperatorCode: "1001",
                PartCode: "PECA-A",
                MoldCode: "MOLDE-A")),
            "COUNTER" or "INCREMENT" => Apply(new OpcUaSimulationCommand(IncrementCounter: true)),
            "RESET" => Reset(),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Preset OPC-UA desconhecido.")
        };
    }

    public OpcUaSimulationState Reset()
    {
        lock (_gate)
        {
            _state = BuildDefaultState(_options);
            return _state;
        }
    }

    private static OpcUaSimulationState BuildDefaultState(IndustrialMiddlewareOptions options)
        => new(
            options.OpcUaSimulationMachineIp,
            options.OpcUaSimulationMachineCode,
            $"opc.tcp://{options.OpcUaSimulationMachineIp}:4840",
            "IDLE",
            false,
            "OPC_SIM_A",
            0,
            null,
            "1001",
            "PECA-A",
            "MOLDE-A",
            "OPCUA_SIM",
            DateTimeOffset.UtcNow);

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }
}
