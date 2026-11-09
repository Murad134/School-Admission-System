using Microsoft.EntityFrameworkCore;
using SchoolAdmissionSystem.Api.Data;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Models;

namespace SchoolAdmissionSystem.Api.Services;

public class ClassService : IClassService
{
    private readonly ApplicationDbContext _dbContext;

    public ClassService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClassResponse> CreateClassAsync(CreateClassRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Class name is required.");
        }

        if (request.TotalSeats <= 0)
        {
            throw new ValidationException("totalSeats must be greater than 0.");
        }

        var classId = $"c_{Guid.NewGuid():N}"[..10];
        var now = DateTime.UtcNow;

        var schoolClass = new SchoolClass
        {
            Id = classId,
            Name = request.Name.Trim(),
            TotalSeats = request.TotalSeats,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Classes.Add(schoolClass);
        await _dbContext.SaveChangesAsync();

        return new ClassResponse
        {
            Id = schoolClass.Id,
            Name = schoolClass.Name,
            TotalSeats = schoolClass.TotalSeats
        };
    }

    public async Task<List<ClassDetailResponse>> GetAllClassesAsync()
    {
        var now = DateTime.UtcNow;
        var classes = await _dbContext.Classes
            .Include(c => c.Holds)
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return classes.Select(c => MapToDetail(c, now)).ToList();
    }

    public async Task<ClassDetailResponse> GetClassByIdAsync(string classId)
    {
        var now = DateTime.UtcNow;
        var schoolClass = await _dbContext.Classes
            .Include(c => c.Holds)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (schoolClass == null)
        {
            throw new NotFoundException($"Class with id '{classId}' not found.");
        }

        return MapToDetail(schoolClass, now);
    }

    private static ClassDetailResponse MapToDetail(SchoolClass schoolClass, DateTime now)
    {
        var heldSeats = schoolClass.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);
        var confirmedSeats = schoolClass.Holds.Count(h => h.Status == HoldStatus.Confirmed);
        var availableSeats = Math.Max(0, schoolClass.TotalSeats - (heldSeats + confirmedSeats));

        return new ClassDetailResponse
        {
            Id = schoolClass.Id,
            Name = schoolClass.Name,
            TotalSeats = schoolClass.TotalSeats,
            HeldSeats = heldSeats,
            ConfirmedSeats = confirmedSeats,
            AvailableSeats = availableSeats
        };
    }
}
