using Microsoft.Extensions.Configuration;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
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
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 20 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        // First hold succeeds
        var hold1 = await holdService.CreateHoldAsync(cls.Id, studentId);
        Assert.NotNull(hold1);

        // Second hold by the same student in the same class while hold1 is active must throw ConflictException
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CreateHoldAsync(cls.Id, studentId));

        Assert.Contains("already has an active hold", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateHold_WhenStudentHasConfirmedAdmissionInSameClass_ThrowsConflictException()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 20 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold = await holdService.CreateHoldAsync(cls.Id, studentId);
        await holdService.ConfirmHoldAsync(hold.HoldId, studentId);

        // Attempting to hold another seat after confirmation must be rejected
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            holdService.CreateHoldAsync(cls.Id, studentId));

        Assert.Contains("already has an active hold or confirmed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateHold_WhenPreviousHoldWasCancelled_AllowsNewHold()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 20 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";

        var hold1 = await holdService.CreateHoldAsync(cls.Id, studentId);
        await holdService.CancelHoldAsync(hold1.HoldId, studentId);

        // After cancellation, the student is permitted to apply again
        var hold2 = await holdService.CreateHoldAsync(cls.Id, studentId);
        Assert.NotNull(hold2);
        Assert.NotEqual(hold1.HoldId, hold2.HoldId);
    }
}
