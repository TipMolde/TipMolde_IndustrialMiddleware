using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class ProtocolDetectionService : IProtocolDetectionService
{
    private const string NoProtocolMessage =
        "Nao foi detetado nenhum protocolo de comunicacao neste IP. Nao coloque este IP na maquina e contacte um tecnico.";

    private readonly HttpClient _httpClient;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<ProtocolDetectionService> _logger;

    public ProtocolDetectionService(
        HttpClient httpClient,
        IOptions<IndustrialMiddlewareOptions> options,
        ILogger<ProtocolDetectionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProtocolDetectionResult> DetectAsync(ProtocolDetectionRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.MachineIp))
        {
            return Failure(string.Empty, "O IP da maquina e obrigatorio para testar a comunicacao.");
        }

        var machineIp = request.MachineIp.Trim();
        if (!IPAddress.TryParse(machineIp, out _))
        {
            return Failure(machineIp, "O IP informado nao tem um formato valido.");
        }

        _logger.LogInformation("A testar protocolos industriais no IP {MachineIp}.", machineIp);

        var mtConnect = await TryDetectMtConnectAsync(machineIp);
        if (mtConnect.Detected)
        {
            return mtConnect;
        }

        var opcUa = await TryDetectOpcUaAsync(machineIp);
        if (opcUa.Detected)
        {
            return opcUa;
        }

        var sodick = await TryDetectSodickAsync(machineIp);
        if (sodick.Detected)
        {
            return sodick;
        }

        return Failure(machineIp, NoProtocolMessage);
    }

    private async Task<ProtocolDetectionResult> TryDetectMtConnectAsync(string machineIp)
    {
        var endpointUrl = $"http://{machineIp}:{_options.MtConnectPort}{_options.MtConnectCurrentPath}";

        try
        {
            using var response = await _httpClient.GetAsync(endpointUrl);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(machineIp, null);
            }

            var payload = await response.Content.ReadAsStringAsync();
            if (payload.Contains("MTConnect", StringComparison.OrdinalIgnoreCase))
            {
                return Success(machineIp, "MTConnect", endpointUrl, "Protocolo MTConnect detetado com sucesso.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "MTConnect nao detetado em {MachineIp}.", machineIp);
        }

        return Failure(machineIp, null);
    }

    private async Task<ProtocolDetectionResult> TryDetectOpcUaAsync(string machineIp)
    {
        if (_options.OpcUaSimulationEnabled
            && string.Equals(machineIp, _options.OpcUaSimulationMachineIp, StringComparison.OrdinalIgnoreCase))
        {
            return Success(
                machineIp,
                "OPC-UA",
                $"opc.tcp://{machineIp}:4840",
                "Protocolo OPC-UA detetado atraves do simulador configurado para este IP.");
        }

        var endpointUrl = $"opc.tcp://{machineIp}:4840";

        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(machineIp, 4840);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.ProtocolDetectionTimeoutSeconds)));

            var completed = await Task.WhenAny(connectTask, timeoutTask);
            if (completed == connectTask && tcpClient.Connected)
            {
                return Success(machineIp, "OPC-UA", endpointUrl, "Endpoint OPC-UA acessivel na porta 4840.");
            }
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException)
        {
            _logger.LogDebug(ex, "OPC-UA nao detetado em {MachineIp}.", machineIp);
        }

        return Failure(machineIp, null);
    }

    private async Task<ProtocolDetectionResult> TryDetectSodickAsync(string machineIp)
    {
        var candidatePaths = _options.SodickProbePaths.Length > 0
            ? _options.SodickProbePaths
            : ["/"];

        foreach (var path in candidatePaths)
        {
            var endpointUrl = Combine($"http://{machineIp}", path);

            try
            {
                using var response = await _httpClient.GetAsync(endpointUrl);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var payload = await response.Content.ReadAsStringAsync();
                if (IsSodickPayload(payload))
                {
                    return Success(machineIp, "SODICK", endpointUrl, "Interface Sodick HTTP detetada com sucesso.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogDebug(ex, "Sodick HTTP nao detetado em {MachineIp} via {EndpointUrl}.", machineIp, endpointUrl);
            }
        }

        return Failure(machineIp, null);
    }

    private static bool IsSodickPayload(string payload)
    {
        return payload.Contains("MClient.cab", StringComparison.OrdinalIgnoreCase)
            || payload.Contains("MClient1.SURL", StringComparison.OrdinalIgnoreCase)
            || payload.Contains("Sodick", StringComparison.OrdinalIgnoreCase);
    }

    private static string Combine(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');

        return trimmedPath.Length == 0
            ? trimmedBase
            : $"{trimmedBase}/{trimmedPath}";
    }

    private static ProtocolDetectionResult Success(string machineIp, string protocol, string endpointUrl, string message)
    {
        return new ProtocolDetectionResult(machineIp, true, protocol, endpointUrl, message);
    }

    private static ProtocolDetectionResult Failure(string machineIp, string? message)
    {
        return new ProtocolDetectionResult(machineIp, false, null, null, message);
    }
}
