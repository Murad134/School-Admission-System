using Microsoft.AspNetCore.Mvc;
using SchoolAdmissionSystem.Api.Common;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly IHoldService _holdService;

    public HoldsController(IHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpGet("{holdId}")]
    public async Task<IActionResult> GetHoldById([FromRoute] string holdId)
    {
        var result = await _holdService.GetHoldByIdAsync(holdId);
        return Ok(result);
    }

    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> ConfirmHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || !StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var result = await _holdService.ConfirmHoldAsync(holdId, studentId);
        return Ok(result);
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> CancelHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || !StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        await _holdService.CancelHoldAsync(holdId, studentId);
        return NoContent();
    }
}

