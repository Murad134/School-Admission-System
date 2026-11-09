using Microsoft.AspNetCore.Mvc;
using SchoolAdmissionSystem.Api.Common;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly IClassService _classService;
    private readonly IHoldService _holdService;

    public ClassesController(IClassService classService, IHoldService holdService)
    {
        _classService = classService;
        _holdService = holdService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            throw new ValidationException(errors);
        }

        var result = await _classService.CreateClassAsync(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllClasses()
    {
        var result = await _classService.GetAllClassesAsync();
        return Ok(result);
    }

    [HttpGet("{classId}")]
    public async Task<IActionResult> GetClassById([FromRoute] string classId)
    {
        var result = await _classService.GetClassByIdAsync(classId);
        return Ok(result);
    }

    [HttpPost("{classId}/holds")]
    public async Task<IActionResult> CreateHold(
        [FromRoute] string classId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || !StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var result = await _holdService.CreateHoldAsync(classId, studentId);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

