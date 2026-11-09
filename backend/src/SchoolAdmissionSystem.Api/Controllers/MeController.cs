using Microsoft.AspNetCore.Mvc;
using SchoolAdmissionSystem.Api.Common;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Api.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly IHoldService _holdService;

    public MeController(IHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpGet("holds")]
    public async Task<IActionResult> GetMyHolds([FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || !StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var result = await _holdService.GetStudentHoldsAsync(studentId);
        return Ok(result);
    }
}

