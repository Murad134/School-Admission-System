using Microsoft.Extensions.Configuration;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Models;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Tests;

public class ExpirationAndCancellationTests
{
    private readonly IConfiguration _configuration;

    public ExpirationAndCancellationTests()
    {
        var configValues = new Dictionary<string, string?>
        {
            { "HOLD_DURATION_SECONDS", "1" } // 1 second duration
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task GetHoldById_WhenPastExpirationTime_DynamicallyReturnsExpired()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 20 });

        // Insert a hold that already expired 10 minutes ago
        var expiredHold = new SeatHold
        {
            Id = "h_test_exp1",
            ClassId = cls.Id,
            StudentId = "20152691234567890",
            Status = HoldStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
        };

        context.Holds.Add(expiredHold);
        await context.SaveChangesAsync();

        var holdService = new HoldService(context, _configuration);
        var detail = await holdService.GetHoldByIdAsync("h_test_exp1");

        Assert.Equal(HoldStatus.Expired, detail.Status);
    }

    [Fact]
    public async Task ConfirmHold_WhenExpired_ThrowsConflictException()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 20 });

        var studentId = "20152691234567890";
        var expiredHold = new SeatHold
        {
            Id = "h_test_exp2",
            ClassId = cls.Id,
            StudentId = studentId,
            Status = HoldStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };

        context.Holds.Add(expiredHold);
        await context.SaveChangesAsync();

        var holdService = new HoldService(context, _configuration);

        await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.ConfirmHoldAsync("h_test_exp2", studentId));
    }

    [Fact]
    public async Task ExpiredHold_AutomaticallyReleasesSeat_WithoutManualIntervention()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 10 });

        // Add 1 expired hold
        context.Holds.Add(new SeatHold
        {
            Id = "h_expired",
            ClassId = cls.Id,
            StudentId = "20152691111111111",
            Status = HoldStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow.AddMinutes(-12),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-12)
        });

        // Add 1 active hold
        context.Holds.Add(new SeatHold
        {
            Id = "h_active",
            ClassId = cls.Id,
            StudentId = "20152692222222222",
            Status = HoldStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        // Available seats should be 9 (10 - 1 active = 9), expired hold is ignored!
        var classDetail = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(1, classDetail.HeldSeats);
        Assert.Equal(9, classDetail.AvailableSeats);
    }
}
