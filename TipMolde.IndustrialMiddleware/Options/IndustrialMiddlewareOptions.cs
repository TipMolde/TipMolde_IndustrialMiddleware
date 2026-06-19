namespace TipMolde.IndustrialMiddleware.Options;

public sealed class IndustrialMiddlewareOptions
{
    public const string SectionName = "IndustrialMiddleware";

    public string BackendBaseUrl { get; set; } = "https://localhost:5001";

    public string BackendEventsPath { get; set; } = "/api/industrial/events";

    public string BackendMachinesPath { get; set; } = "/api/Maquina";

    public string? BackendBearerToken { get; set; }

    public int BackendRequestTimeoutSeconds { get; set; } = 15;

    public int PollIntervalSeconds { get; set; } = 5;

    public int MachineCatalogRefreshMinutes { get; set; } = 60;

    public int MachineCatalogPageSize { get; set; } = 250;

    public bool IgnoreMachinesInMaintenance { get; set; } = true;

    public int DefaultMachineTimeoutSeconds { get; set; } = 30;

    public bool EmitToBackend { get; set; } = false;

    public string MtConnectCurrentPath { get; set; } = "/current";

    public int MtConnectPort { get; set; } = 8082;

    public string MtConnectFallbackMachineIp { get; set; } = "192.168.1.50";

    public string MtConnectFallbackMachineCode { get; set; } = "MTCONNECT-01";

    public string OpcUaFallbackEndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    public string OpcUaFallbackMachineIp { get; set; } = "127.0.0.1";

    public string OpcUaFallbackMachineCode { get; set; } = "OPCUA-01";
}
