using Microsoft.EntityFrameworkCore;
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
            { "HOLD_DURATION_SECONDS", "600" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task GetHoldById_WhenPastExpirationWindow_DynamicallyReturnsExpiredStatus()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 20 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);

        // Manually manipulate ExpiresAt into the past to simulate time passage
        var dbHold = await context.Holds.FirstAsync(h => h.Id == hold.HoldId);
        dbHold.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        // GetHoldByIdAsync should dynamically evaluate status as EXPIRED
        var detail = await holdService.GetHoldByIdAsync(hold.HoldId);
        Assert.Equal(HoldStatus.Expired, detail.Status);
    }

    [Fact]
    public async Task ConfirmHold_WhenExpired_ThrowsConflictException()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 20 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);

        // Simulate expired hold
        var dbHold = await context.Holds.FirstAsync(h => h.Id == hold.HoldId);
        dbHold.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        // Confirming must fail with ConflictException
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.ConfirmHoldAsync(hold.HoldId, studentId));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpiredHold_AutomaticallyReclaimsSeat_WithoutExplicitApiCall()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 1 });

        var holdService = new HoldService(context, _configuration);
        var studentId1 = "20152691111111111";
        var studentId2 = "20152692222222222";

        var hold1 = await holdService.CreateHoldAsync(cls.Id, studentId1);

        // Class is currently full
        var classFull = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(0, classFull.AvailableSeats);

        // Second student cannot hold
        await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CreateHoldAsync(cls.Id, studentId2));

        // Time passes: hold1 expires
        var dbHold = await context.Holds.FirstAsync(h => h.Id == hold1.HoldId);
        dbHold.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        // Querying class immediately shows 1 available seat without needing any cleanup job
        var classReclaimed = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(1, classReclaimed.AvailableSeats);
        Assert.Equal(0, classReclaimed.HeldSeats);

        // Second student can now successfully hold the reclaimed seat
        var hold2 = await holdService.CreateHoldAsync(cls.Id, studentId2);
        Assert.NotNull(hold2);
        Assert.Equal(HoldStatus.Held, hold2.Status);
    }

    [Fact]
    public async Task CancelHold_WhenAlreadyConfirmed_ThrowsConflictException()
    {
        var dbName = $"ExpirationDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 8 - Morning", TotalSeats = 10 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);
        await holdService.ConfirmHoldAsync(hold.HoldId, studentId);

        // Cannot cancel a confirmed admission
        await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CancelHoldAsync(hold.HoldId, studentId));
    }
}
