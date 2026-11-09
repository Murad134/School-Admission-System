using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolAdmissionSystem.Api.Common;
using SchoolAdmissionSystem.Api.Data;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Models;

namespace SchoolAdmissionSystem.Api.Services;

public class HoldService : IHoldService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClassLocks = new();

    public HoldService(ApplicationDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<HoldResponse> CreateHoldAsync(string classId, string studentId)
    {
        if (!StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        // In-process lock per class to serialize threads within an instance
        var classLock = ClassLocks.GetOrAdd(classId, _ => new SemaphoreSlim(1, 1));
        await classLock.WaitAsync();

        try
        {
            var isPostgres = _dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

            var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                // Acquire row lock in PostgreSQL if running on Postgres
                if (isPostgres)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT \"Id\" FROM classes WHERE \"Id\" = {0} FOR UPDATE", classId);
                }

                var schoolClass = await _dbContext.Classes
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (schoolClass == null)
                {
                    throw new NotFoundException($"Class with id '{classId}' not found.");
                }

                var now = DateTime.UtcNow;

                // Rule: One student cannot have more than one active hold or confirmed seat in a class at any time
                var hasActiveHoldOrConfirmed = await _dbContext.Holds.AnyAsync(h =>
                    h.ClassId == classId &&
                    h.StudentId == studentId &&
                    (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)));

                if (hasActiveHoldOrConfirmed)
                {
                    throw new ConflictException("Student already has an active hold or confirmed seat in this class.");
                }

                // Count active held seats (unexpired) and confirmed seats
                var heldSeats = await _dbContext.Holds.CountAsync(h =>
                    h.ClassId == classId &&
                    h.Status == HoldStatus.Held &&
                    h.ExpiresAt > now);

                var confirmedSeats = await _dbContext.Holds.CountAsync(h =>
                    h.ClassId == classId &&
                    h.Status == HoldStatus.Confirmed);

                var availableSeats = schoolClass.TotalSeats - (heldSeats + confirmedSeats);

                if (availableSeats <= 0)
                {
                    throw new ConflictException("No seats available in this class.");
                }

                // Read HOLD_DURATION_SECONDS from environment/config, default to 600
                var holdDurationSeconds = _configuration.GetValue<int>("HOLD_DURATION_SECONDS", 600);
                if (holdDurationSeconds <= 0)
                {
                    holdDurationSeconds = 600;
                }

                var expiresAt = now.AddSeconds(holdDurationSeconds);
                var holdId = $"h_{Guid.NewGuid():N}"[..10];

                var hold = new SeatHold
                {
                    Id = holdId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = HoldStatus.Held,
                    ExpiresAt = expiresAt,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbContext.Holds.Add(hold);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new HoldResponse
                {
                    HoldId = hold.Id,
                    ClassId = hold.ClassId,
                    StudentId = hold.StudentId,
                    Status = HoldStatus.Held,
                    ExpiresAt = hold.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
            });
        }
        finally
        {
            classLock.Release();
        }
    }

    public async Task<HoldDetailResponse> GetHoldByIdAsync(string holdId)
    {
        var hold = await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == holdId);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        var effectiveStatus = hold.Status;
        if (effectiveStatus == HoldStatus.Held && hold.ExpiresAt <= DateTime.UtcNow)
        {
            effectiveStatus = HoldStatus.Expired;
        }

        return new HoldDetailResponse
        {
            HoldId = hold.Id,
            ClassId = hold.ClassId,
            Status = effectiveStatus,
            ExpiresAt = hold.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    public async Task<ConfirmHoldResponse> ConfirmHoldAsync(string holdId, string studentId)
    {
        if (!StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.Id == holdId);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        // Only the student themselves can confirm their own hold
        if (hold.StudentId != studentId)
        {
            throw new ForbiddenException("You are not authorized to confirm this hold.");
        }

        // Idempotency: Multiple confirms on an already confirmed hold return 200 without re-incrementing
        if (hold.Status == HoldStatus.Confirmed)
        {
            return new ConfirmHoldResponse
            {
                HoldId = hold.Id,
                Status = HoldStatus.Confirmed
            };
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            throw new ConflictException("Hold was cancelled. Cannot confirm admission.");
        }

        // Check for expiration
        if (hold.Status == HoldStatus.Expired || (hold.Status == HoldStatus.Held && hold.ExpiresAt <= DateTime.UtcNow))
        {
            if (hold.Status != HoldStatus.Expired)
            {
                hold.Status = HoldStatus.Expired;
                hold.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            throw new ConflictException("Hold has expired. Cannot confirm admission.");
        }

        // Successfully confirm
        hold.Status = HoldStatus.Confirmed;
        hold.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return new ConfirmHoldResponse
        {
            HoldId = hold.Id,
            Status = HoldStatus.Confirmed
        };
    }

    public async Task CancelHoldAsync(string holdId, string studentId)
    {
        if (!StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.Id == holdId);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        if (hold.StudentId != studentId)
        {
            throw new ForbiddenException("You are not authorized to cancel this hold.");
        }

        if (hold.Status == HoldStatus.Confirmed)
        {
            throw new ConflictException("Cannot cancel an already confirmed admission.");
        }

        if (hold.Status == HoldStatus.Held)
        {
            hold.Status = HoldStatus.Cancelled;
            hold.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<StudentHoldResponse>> GetStudentHoldsAsync(string studentId)
    {
        if (!StudentIdValidator.IsValid(studentId))
        {
            throw new ValidationException("Header X-Student-Id must be a 17-digit numeric birth registration number.");
        }

        var now = DateTime.UtcNow;

        var holds = await _dbContext.Holds
            .Include(h => h.Class)
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        return holds.Select(h =>
        {
            var status = h.Status;
            if (status == HoldStatus.Held && h.ExpiresAt <= now)
            {
                status = HoldStatus.Expired;
            }

            return new StudentHoldResponse
            {
                HoldId = h.Id,
                ClassId = h.ClassId,
                ClassName = h.Class?.Name ?? string.Empty,
                Status = status,
                ExpiresAt = h.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }).ToList();
    }
}

