namespace TipMolde.IndustrialMiddleware.Options;

public sealed class IndustrialMiddlewareOptions
{
    public const string SectionName = "IndustrialMiddleware";

    public string BackendBaseUrl { get; set; } = "http://localhost:8080";

    public string BackendEventsPath { get; set; } = "/api/industrial/events";

    public string BackendMachinesPath { get; set; } = "/api/industrial/machines";

    public string? BackendBearerToken { get; set; }

    public int BackendRequestTimeoutSeconds { get; set; } = 15;

    public int PollIntervalSeconds { get; set; } = 5;

    public int MachineCatalogRefreshMinutes { get; set; } = 60;

    public int MachineCatalogPageSize { get; set; } = 250;

    public bool IgnoreMachinesInMaintenance { get; set; } = true;

    public int DefaultMachineTimeoutSeconds { get; set; } = 30;

    public int ProtocolDetectionTimeoutSeconds { get; set; } = 5;

    public bool EmitToBackend { get; set; } = true;

    public string MtConnectCurrentPath { get; set; } = "/current";

    public int MtConnectPort { get; set; } = 8082;

    public string[] SodickProbePaths { get; set; } =
    [
        "/"
    ];

    public bool SodickLogResponsePreview { get; set; } = true;

    public int SodickResponsePreviewMaxChars { get; set; } = 2000;

    public bool SodickIgnoreActiveXHostPage { get; set; } = true;

    public bool OpcUaSimulationEnabled { get; set; } = false;

    public string OpcUaSimulationMachineIp { get; set; } = "127.0.0.1";

    public string OpcUaSimulationMachineCode { get; set; } = "OPCUA-SIM-01";
}
