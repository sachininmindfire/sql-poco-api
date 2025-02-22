using Microsoft.AspNetCore.Mvc;
using SQLPocoAPI.Models;
using SQLPocoAPI.Services;

namespace SQLPocoAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PocoController : ControllerBase
{
    private readonly IPocoGeneratorService _pocoGeneratorService;
    private readonly ILogger<PocoController> _logger;

    public PocoController(
        IPocoGeneratorService pocoGeneratorService,
        ILogger<PocoController> logger)
    {
        _pocoGeneratorService = pocoGeneratorService;
        _logger = logger;
    }

    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConvertSqlToPoco([FromBody] ConversionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SqlScript))
        {
            return BadRequest("SQL script is required");
        }

        try
        {
            var result = await _pocoGeneratorService.GeneratePocoAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting SQL to POCO");
            return StatusCode(500, new ConversionResponse 
            { 
                Success = false, 
                Error = "Internal server error occurred during conversion" 
            });
        }
    }
}