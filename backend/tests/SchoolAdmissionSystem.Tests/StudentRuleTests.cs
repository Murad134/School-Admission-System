using Microsoft.Extensions.Configuration;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Models;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Tests;

public class StudentRuleTests
{
    private readonly IConfiguration _configuration;

    public StudentRuleTests()
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
    public async Task CreateHold_WhenStudentAlreadyHasActiveHoldInSameClass_ThrowsConflictException()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 40 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        // First hold succeeds
        await holdService.CreateHoldAsync(cls.Id, studentId);

        // Second hold for the same student in the same class must fail with ConflictException
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CreateHoldAsync(cls.Id, studentId));

        Assert.Contains("already has an active hold", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateHold_WhenStudentAlreadyHasConfirmedAdmissionInSameClass_ThrowsConflictException()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 40 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);
        await holdService.ConfirmHoldAsync(hold.HoldId, studentId);

        // Attempting to apply again in the same class after confirmation must fail
        await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CreateHoldAsync(cls.Id, studentId));
    }

    [Fact]
    public async Task CreateHold_WhenPreviousHoldWasCancelled_AllowsNewHold()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 40 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold1 = await holdService.CreateHoldAsync(cls.Id, studentId);
        await holdService.CancelHoldAsync(hold1.HoldId, studentId);

        // Because hold1 was cancelled, the student can now apply again
        var hold2 = await holdService.CreateHoldAsync(cls.Id, studentId);
        Assert.NotNull(hold2);
        Assert.Equal(HoldStatus.Held, hold2.Status);
    }

    [Fact]
    public async Task CreateHold_WithInvalidStudentId_ThrowsValidationException()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 40 });

        var holdService = new HoldService(context, _configuration);

        // Invalid: 16 digits
        await Assert.ThrowsAsync<ValidationException>(() =>
            holdService.CreateHoldAsync(cls.Id, "1234567890123456"));

        // Invalid: letters
        await Assert.ThrowsAsync<ValidationException>(() =>
            holdService.CreateHoldAsync(cls.Id, "2015269123456789A"));
    }
}

