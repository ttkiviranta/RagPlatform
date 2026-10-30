using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthStatusResponse> GetHealth()
    {
        var healthStatus = _healthService.GetStatus();
        return Ok(healthStatus);
    }
}
