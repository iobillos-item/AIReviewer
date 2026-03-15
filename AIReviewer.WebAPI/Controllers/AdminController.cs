using AIReviewer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIReviewer.WebAPI.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ISopIngestionService _ingestionService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ISopIngestionService ingestionService, ILogger<AdminController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [HttpPost("ingest-sop")]
    public async Task<IActionResult> IngestSop()
    {
        _logger.LogInformation("SOP ingestion triggered via API");
        await _ingestionService.IngestAsync();
        return Ok(new { message = "SOP ingestion completed" });
    }
}
