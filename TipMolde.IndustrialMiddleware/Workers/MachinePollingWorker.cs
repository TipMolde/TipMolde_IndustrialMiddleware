using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Workers;

public sealed class MachinePollingWorker : BackgroundService
{
    private readonly IEnumerable<IMachineConnector> _connectors;
    private readonly IMtConnectSnapshotParser _mtConnectSnapshotParser;
    private readonly IMachineNormalizer _normalizer;
    private readonly IEventDetector _eventDetector;
    private readonly IBackendClient _backendClient;
    private readonly IMachineStateStore _stateStore;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<MachinePollingWorker> _logger;
    private readonly Dictionary<string, MtConnectSnapshot> _lastMtConnectSnapshots = new(StringComparer.OrdinalIgnoreCase);

    public MachinePollingWorker(
        IEnumerable<IMachineConnector> connectors,
        IMtConnectSnapshotParser mtConnectSnapshotParser,
        IMachineNormalizer normalizer,
        IEventDetector eventDetector,
        IBackendClient backendClient,
        IMachineStateStore stateStore,
        Microsoft.Extensions.Options.IOptions<IndustrialMiddlewareOptions> options,
        ILogger<MachinePollingWorker> logger)
    {
        _connectors = connectors;
        _mtConnectSnapshotParser = mtConnectSnapshotParser;
        _normalizer = normalizer;
        _eventDetector = eventDetector;
        _backendClient = backendClient;
        _stateStore = stateStore;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Industrial middleware started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var connector in _connectors)
            {
                var rawItems = await connector.ReadAsync(stoppingToken);

                foreach (var raw in rawItems)
                {
                    var previous = await _stateStore.GetAsync(raw.MachineIp, stoppingToken);

                    MtConnectSnapshot? mtConnectSnapshot = null;
                    string? changeSignature;

                    if (string.Equals(raw.Protocol, "MTConnect", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            mtConnectSnapshot = _mtConnectSnapshotParser.Parse(raw.MachineIp, raw.Payload, raw.ReceivedAt);
                            changeSignature = BuildMtConnectSignature(mtConnectSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Falha a interpretar o payload MTConnect de {MachineIp}. O payload bruto sera ignorado nesta iteracao.",
                                raw.MachineIp);
                            continue;
                        }
                    }
                    else
                    {
                        changeSignature = ComputeHash(raw.Payload);
                    }

                    var hasChanged = previous is null
                        || !string.Equals(previous.ChangeSignature, changeSignature, StringComparison.Ordinal);

                    if (!hasChanged)
                    {
                        _logger.LogDebug(
                            "Sem alteracoes para {MachineIp} ({Protocol}).",
                            raw.MachineIp,
                            raw.Protocol);
                        continue;
                    }

                    _logger.LogInformation(
                        "Alteracao detetada em {MachineIp} ({Protocol})",
                        raw.MachineIp,
                        raw.Protocol);

                    if (mtConnectSnapshot is not null)
                    {
                        _lastMtConnectSnapshots.TryGetValue(raw.MachineIp, out var previousSnapshot);
                        LogMtConnectSnapshot(previousSnapshot, mtConnectSnapshot);
                        _lastMtConnectSnapshots[raw.MachineIp] = mtConnectSnapshot;
                    }
                    else
                    {
                        _logger.LogInformation("Payload recebido de {MachineIp} ({Protocol})", raw.MachineIp, raw.Protocol);
                    }

                    var normalized = _normalizer.Normalize(raw);
                    var detected = _eventDetector.Detect(normalized, previous);
                    var payload = detected.Select(x => new MachineEventDto(
                        x.MachineIp,
                        x.EventType.ToString(),
                        x.OccurredAt,
                        x.WorkOrderCode,
                        x.OperationCode,
                        x.PartCode,
                        x.MoldCode,
                        x.Program,
                        x.Confidence,
                        x.ContextCompleteness,
                        x.CorrelationId)).ToArray();

                    LogBackendPayloadPreview(payload);

                    if (_options.EmitToBackend)
                    {
                        await _backendClient.SendEventsAsync(
                            payload,
                            stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Modo de teste ativo: nao serao enviados eventos para o backend nesta execucao.");
                    }

                    await _stateStore.UpsertAsync(
                        new MachineStateSnapshot(
                            _options.EmitToBackend ? normalized.MachineIp : raw.MachineIp,
                            _options.EmitToBackend ? normalized.State : null,
                            _options.EmitToBackend ? normalized.Program : null,
                            detected.LastOrDefault()?.CorrelationId,
                            _options.EmitToBackend ? normalized.OccurredAt : raw.ReceivedAt,
                            changeSignature,
                            raw.Payload),
                        stoppingToken);
                }
            }
        }
    }

    private static string ComputeHash(string payload)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

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

    private void LogBackendPayloadPreview(MachineEventDto[] payload)
    {
        var previewJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogInformation(
            "Preview do JSON que seria enviado ao backend:{NewLine}{Json}",
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
