using Microsoft.AspNetCore.Mvc;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Controllers;

[ApiController]
[Route("api/protocol-detection")]
public sealed class ProtocolDetectionController : ControllerBase
{
    private readonly IProtocolDetectionService _protocolDetectionService;

    public ProtocolDetectionController(IProtocolDetectionService protocolDetectionService)
    {
        _protocolDetectionService = protocolDetectionService;
    }

    [HttpPost]
    public async Task<IActionResult> Detect([FromBody] ProtocolDetectionRequest request)
    {
        var result = await _protocolDetectionService.DetectAsync(request);
        return result.Detected ? Ok(result) : UnprocessableEntity(result);
    }
}
