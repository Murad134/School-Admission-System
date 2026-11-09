using Microsoft.Extensions.Configuration;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Models;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Tests;

public class HoldLifecycleTests
{
    private readonly IConfiguration _configuration;

    public HoldLifecycleTests()
    {
        var configValues = new Dictionary<string, string?>
        {
            { "HOLD_DURATION_SECONDS", "300" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task CreateHold_WithValidStudentId_ReturnsHeldStatus()
    {
        var dbName = $"LifecycleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);

        Assert.NotNull(hold);
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.Equal(cls.Id, hold.ClassId);
        Assert.Equal(studentId, hold.StudentId);
        Assert.False(string.IsNullOrEmpty(hold.ExpiresAt));
    }

    [Fact]
    public async Task ConfirmHold_WhenHeldAndValidOwner_TransitionsToConfirmed()
    {
        var dbName = $"LifecycleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);
        var confirmRes = await holdService.ConfirmHoldAsync(hold.HoldId, studentId);

        Assert.Equal(HoldStatus.Confirmed, confirmRes.Status);
        Assert.Equal(hold.HoldId, confirmRes.HoldId);

        var detail = await holdService.GetHoldByIdAsync(hold.HoldId);
        Assert.Equal(HoldStatus.Confirmed, detail.Status);
    }

    [Fact]
    public async Task ConfirmHold_WhenAlreadyConfirmed_IsIdempotent()
    {
        var dbName = $"LifecycleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);

        // First confirm
        var confirm1 = await holdService.ConfirmHoldAsync(hold.HoldId, studentId);
        Assert.Equal(HoldStatus.Confirmed, confirm1.Status);

        // Second duplicate confirm should return 200 with CONFIRMED without error
        var confirm2 = await holdService.ConfirmHoldAsync(hold.HoldId, studentId);
        Assert.Equal(HoldStatus.Confirmed, confirm2.Status);

        // Check seat counts: confirmedSeats should remain 1
        var classDetail = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(1, classDetail.ConfirmedSeats);
        Assert.Equal(0, classDetail.HeldSeats);
        Assert.Equal(49, classDetail.AvailableSeats);
    }

    [Fact]
    public async Task ConfirmHold_WhenWrongStudentAttempts_ThrowsForbiddenException()
    {
        var dbName = $"LifecycleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var ownerStudentId = "20152691234567890";
        var otherStudentId = "20152699999999999";

        var hold = await holdService.CreateHoldAsync(cls.Id, ownerStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            holdService.ConfirmHoldAsync(hold.HoldId, otherStudentId));
    }

    [Fact]
    public async Task CancelHold_WhenHeldAndOwner_TransitionsToCancelledAndReleasesSeat()
    {
        var dbName = $"LifecycleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);

        // Available seats should now be 49
        var classBefore = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(49, classBefore.AvailableSeats);
        Assert.Equal(1, classBefore.HeldSeats);

        // Cancel hold
        await holdService.CancelHoldAsync(hold.HoldId, studentId);

        // Check hold status
        var detail = await holdService.GetHoldByIdAsync(hold.HoldId);
        Assert.Equal(HoldStatus.Cancelled, detail.Status);

        // Seat should be immediately returned
        var classAfter = await classService.GetClassByIdAsync(cls.Id);
        Assert.Equal(50, classAfter.AvailableSeats);
        Assert.Equal(0, classAfter.HeldSeats);
    }
}

