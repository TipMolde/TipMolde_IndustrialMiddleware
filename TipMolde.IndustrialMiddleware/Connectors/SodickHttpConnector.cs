using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Connectors;

public sealed class SodickHttpConnector : IMachineConnector
{
    private readonly HttpClient _httpClient;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly IMachineCatalogStore _machineCatalogStore;
    private readonly ILogger<SodickHttpConnector> _logger;

    public SodickHttpConnector(
        HttpClient httpClient,
        IOptions<IndustrialMiddlewareOptions> options,
        IMachineCatalogStore machineCatalogStore,
        ILogger<SodickHttpConnector> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _machineCatalogStore = machineCatalogStore;
        _logger = logger;
    }

    public string Protocol => "SODICK";

    public async Task<IReadOnlyList<RawMachineData>> ReadAsync(CancellationToken cancellationToken)
    {
        var targets = _machineCatalogStore.GetTargets()
            .Where(target => IsSodickProtocol(target.Protocol))
            .ToArray();

        if (targets.Length == 0)
        {
            if (!_options.SodickFallbackProbeEnabled)
            {
                _logger.LogDebug(
                    "Sem maquinas Sodick no catalogo. O fallback Sodick esta desativado para nao consultar {MachineIp} durante testes de outros protocolos.",
                    _options.SodickFallbackMachineIp);

                return Array.Empty<RawMachineData>();
            }

            targets = new[]
            {
                new MachineCatalogTarget(
                    0,
                    _options.SodickFallbackMachineCode,
                    _options.SodickFallbackMachineIp,
                    BuildBaseUrl(_options.SodickFallbackMachineIp),
                    Protocol,
                    BuildBaseUrl(_options.SodickFallbackMachineIp),
                    null,
                    false)
            };
        }

        var results = new List<RawMachineData>();
        foreach (var target in targets)
        {
            var baseUrl = ResolveBaseUrl(target);
            var candidatePaths = _options.SodickProbePaths.Length > 0
                ? _options.SodickProbePaths
                : new[] { "/" };

            foreach (var path in candidatePaths)
            {
                var requestUri = Combine(baseUrl, path);
                try
                {
                    using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug(
                            "Sodick probe sem sucesso em {Url}. Status: {StatusCode}.",
                            requestUri,
                            response.StatusCode);
                        continue;
                    }

                    var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        _logger.LogDebug("Sodick probe devolveu body vazio em {Url}.", requestUri);
                        continue;
                    }

                    _logger.LogInformation(
                        "Sodick probe bem-sucedido em {Url} para {MachineIp}. Content-Type={ContentType}; Bytes={Bytes}.",
                        requestUri,
                        target.MachineIp,
                        response.Content.Headers.ContentType?.ToString() ?? "desconhecido",
                        payload.Length);

                    LogResponsePreview(requestUri, payload);

                    if (_options.SodickIgnoreActiveXHostPage && IsActiveXHostPage(payload))
                    {
                        _logger.LogInformation(
                            "A resposta de {Url} e apenas a pagina host do ActiveX MClient. Ignorada como telemetria da maquina.",
                            requestUri);
                        continue;
                    }

                    results.Add(new RawMachineData(
                        target.MachineIp,
                        Protocol,
                        payload,
                        DateTimeOffset.UtcNow,
                        target.MachineCode,
                        requestUri));

                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Falha a consultar Sodick em {Url} para a maquina {MachineIp} ({MachineCode}).",
                        requestUri,
                        target.MachineIp,
                        target.MachineCode);
                }
            }
        }

        return results;
    }

    private static bool IsSodickProtocol(string? protocol)
        => string.Equals(protocol, "SODICK", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "SODICK-HTTP", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "SODICK_HTTP", StringComparison.OrdinalIgnoreCase);

    private string ResolveBaseUrl(MachineCatalogTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.EndpointUrl))
        {
            return target.EndpointUrl;
        }

        if (!string.IsNullOrWhiteSpace(target.PollUrl) && !target.PollUrl.EndsWith("/current", StringComparison.OrdinalIgnoreCase))
        {
            return target.PollUrl;
        }

        return BuildBaseUrl(target.MachineIp);
    }

    private static string BuildBaseUrl(string machineIp)
        => $"http://{machineIp}";

    private static string Combine(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');

        return trimmedPath.Length == 0
            ? trimmedBase
            : $"{trimmedBase}/{trimmedPath}";
    }

    private void LogResponsePreview(string requestUri, string payload)
    {
        if (!_options.SodickLogResponsePreview)
        {
            return;
        }

        var preview = Regex.Replace(payload, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        var maxChars = Math.Max(100, _options.SodickResponsePreviewMaxChars);
        if (preview.Length > maxChars)
        {
            preview = preview[..maxChars] + "...";
        }

        _logger.LogInformation(
            "Sodick response preview de {Url}:{NewLine}{Preview}",
            requestUri,
            Environment.NewLine,
            preview);
    }

    private static bool IsActiveXHostPage(string payload)
        => payload.Contains("MClient.cab", StringComparison.OrdinalIgnoreCase)
           || payload.Contains("CLSID:8CEA101D-3EE5-11D2-A19F-004033328ED0", StringComparison.OrdinalIgnoreCase)
           || payload.Contains("MClient1.SURL", StringComparison.OrdinalIgnoreCase);
}
