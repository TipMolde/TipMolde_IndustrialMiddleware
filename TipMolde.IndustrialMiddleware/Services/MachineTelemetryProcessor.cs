using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class MachineTelemetryProcessor : IMachineTelemetryProcessor
{
    private readonly IMtConnectSnapshotParser _mtConnectSnapshotParser;
    private readonly IMachineNormalizer _normalizer;
    private readonly IBackendClient _backendClient;
    private readonly IMachineStateStore _stateStore;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<MachineTelemetryProcessor> _logger;
    private readonly ConcurrentDictionary<string, MtConnectSnapshot> _lastMtConnectSnapshots = new(StringComparer.OrdinalIgnoreCase);

    public MachineTelemetryProcessor(
        IMtConnectSnapshotParser mtConnectSnapshotParser,
        IMachineNormalizer normalizer,
        IBackendClient backendClient,
        IMachineStateStore stateStore,
        IOptions<IndustrialMiddlewareOptions> options,
        ILogger<MachineTelemetryProcessor> logger)
    {
        _mtConnectSnapshotParser = mtConnectSnapshotParser;
        _normalizer = normalizer;
        _backendClient = backendClient;
        _stateStore = stateStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MachineTelemetryProcessingResult> ProcessAsync(RawMachineData rawData, CancellationToken cancellationToken)
    {
        var previous = await _stateStore.GetAsync(rawData.MachineIp, cancellationToken);

        MtConnectSnapshot? mtConnectSnapshot = null;
        string? changeSignature;
        var processingRawData = rawData;

        if (string.Equals(rawData.Protocol, "MTConnect", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                mtConnectSnapshot = _mtConnectSnapshotParser.Parse(rawData.MachineIp, rawData.Payload, rawData.ReceivedAt);
                changeSignature = BuildMtConnectSignature(mtConnectSnapshot);
                processingRawData = rawData with
                {
                    Payload = BuildSemanticPayload(mtConnectSnapshot)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Falha a interpretar o payload MTConnect de {MachineIp}. O payload bruto sera ignorado nesta iteracao.",
                    rawData.MachineIp);

                return new MachineTelemetryProcessingResult(
                    rawData.MachineIp,
                    rawData.Protocol,
                    false,
                    0,
                    false,
                    rawData.ReceivedAt);
            }
        }
        else
        {
            changeSignature = ComputeHash(rawData.Payload);
        }

        var hasChanged = previous is null
            || !string.Equals(previous.ChangeSignature, changeSignature, StringComparison.Ordinal);

        if (!hasChanged)
        {
            _logger.LogDebug(
                "Sem alteracoes para {MachineIp} ({Protocol}).",
                rawData.MachineIp,
                rawData.Protocol);

            return new MachineTelemetryProcessingResult(
                rawData.MachineIp,
                rawData.Protocol,
                false,
                0,
                false,
                rawData.ReceivedAt);
        }

        _logger.LogInformation(
            "Alteracao detetada em {MachineIp} ({Protocol})",
            rawData.MachineIp,
            rawData.Protocol);

        if (mtConnectSnapshot is not null)
        {
            _lastMtConnectSnapshots.TryGetValue(rawData.MachineIp, out var previousSnapshot);
            LogMtConnectSnapshot(previousSnapshot, mtConnectSnapshot);
            _lastMtConnectSnapshots[rawData.MachineIp] = mtConnectSnapshot;
        }
        else
        {
            _logger.LogInformation("Payload recebido de {MachineIp} ({Protocol})", rawData.MachineIp, rawData.Protocol);
        }

        var normalized = _normalizer.Normalize(processingRawData);
        LogNormalizedTelemetryPreview(normalized);

        var payload = new[]
        {
            BuildTelemetryDto(normalized, rawData.SourceName)
        };

        LogBackendTelemetryPreview(payload);

        var sentToBackend = false;
        if (_options.EmitToBackend)
        {
            await _backendClient.SendTelemetryAsync(payload, cancellationToken);
            sentToBackend = true;
        }
        else
        {
            _logger.LogInformation(
                "Modo de teste ativo: nao serao enviados eventos para o backend nesta execucao.");
        }

        await _stateStore.UpsertAsync(
            new MachineStateSnapshot(
                normalized.MachineIp,
                normalized.State,
                normalized.Program,
                normalized.OccurredAt,
                changeSignature,
                rawData.Payload),
            cancellationToken);

        return new MachineTelemetryProcessingResult(
            rawData.MachineIp,
            rawData.Protocol,
            true,
            payload.Length,
            sentToBackend,
            rawData.ReceivedAt);
    }

    private static MachineTelemetryDto BuildTelemetryDto(NormalizedMachineData normalized, string? sourceName)
        => new(
            normalized.MachineIp,
            normalized.Protocol,
            normalized.OccurredAt,
            normalized.State,
            normalized.Program,
            normalized.PieceCounter,
            normalized.AlarmCodes,
            normalized.Context?.OperatorCode,
            normalized.Context?.PartCode,
            normalized.Context?.MoldCode,
            sourceName,
            normalized.RawPayload);

    private void LogNormalizedTelemetryPreview(NormalizedMachineData normalized)
    {
        var previewJson = JsonSerializer.Serialize(new
        {
            normalized.MachineIp,
            normalized.Protocol,
            normalized.State,
            normalized.Program,
            normalized.PieceCounter,
            normalized.AlarmCodes,
            normalized.Context,
            normalized.IntegrationLevel,
            normalized.Confidence,
            normalized.ContextCompleteness,
            normalized.OccurredAt,
            normalized.RawPayload
        }, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogInformation(
            "Leitura normalizada que entrou no pipeline:{NewLine}{Json}",
            Environment.NewLine,
            previewJson);
    }

    private static string ComputeHash(string payload)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private static string BuildMtConnectSignature(MtConnectSnapshot snapshot)
    {
        var events = snapshot.EventLogEntries.Count == 0
            ? string.Empty
            : string.Join("|", snapshot.EventLogEntries);

        return string.Join(
            "||",
            snapshot.Execution ?? string.Empty,
            snapshot.Program ?? string.Empty,
            snapshot.ActiveAlarms ?? string.Empty,
            snapshot.M30Counter1?.ToString() ?? string.Empty,
            snapshot.M30Counter2?.ToString() ?? string.Empty,
            snapshot.LoopsRemaining?.ToString() ?? string.Empty,
            events);
    }

    private static string BuildSemanticPayload(MtConnectSnapshot snapshot)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(snapshot.Execution))
        {
            segments.Add($"STATE={snapshot.Execution}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Program))
        {
            segments.Add($"PROGRAM={snapshot.Program}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveAlarms)
            && !string.Equals(snapshot.ActiveAlarms, "NO ACTIVE ALARMS", StringComparison.OrdinalIgnoreCase))
        {
            segments.Add($"ALARM={snapshot.ActiveAlarms}");
        }

        if (snapshot.M30Counter1 is not null)
        {
            segments.Add($"COUNTER={snapshot.M30Counter1}");
        }

        return string.Join(";", segments);
    }

    private void LogBackendTelemetryPreview(MachineTelemetryDto[] payload)
    {
        var previewJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogInformation(
            "Telemetria JSON preparada para o backend ({Count} registo(s)):{NewLine}{Json}",
            payload.Length,
            Environment.NewLine,
            previewJson);
    }

    private void LogMtConnectSnapshot(MtConnectSnapshot? previousSnapshot, MtConnectSnapshot currentSnapshot)
    {
        var alarms = string.IsNullOrWhiteSpace(currentSnapshot.ActiveAlarms) ? "sem alarmes" : currentSnapshot.ActiveAlarms;
        var events = currentSnapshot.EventLogEntries.Count == 0
            ? "sem eventos recentes"
            : string.Join(" | ", currentSnapshot.EventLogEntries);

        _logger.LogInformation(
            "MTConnect -> IP={MachineIp} | Exec={PreviousExecution} -> {CurrentExecution} | Programa={PreviousProgram} -> {CurrentProgram} | Alarmes={PreviousAlarms} -> {CurrentAlarms} | M30={M30Counter1}/{M30Counter2} | Loops={LoopsRemaining} | Eventos={Events}",
            currentSnapshot.MachineIp,
            previousSnapshot?.Execution ?? "sem estado anterior",
            currentSnapshot.Execution ?? "desconhecido",
            previousSnapshot?.Program ?? "sem estado anterior",
            currentSnapshot.Program ?? "desconhecido",
            previousSnapshot?.ActiveAlarms ?? "sem estado anterior",
            alarms,
            currentSnapshot.M30Counter1?.ToString() ?? "-",
            currentSnapshot.M30Counter2?.ToString() ?? "-",
            currentSnapshot.LoopsRemaining?.ToString() ?? "-",
            events);
    }
}
