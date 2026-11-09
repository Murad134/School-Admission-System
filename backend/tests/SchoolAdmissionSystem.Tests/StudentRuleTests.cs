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

        // Because hold1 was cancelled, the student can apply again
        var hold2 = await holdService.CreateHoldAsync(cls.Id, studentId);
        Assert.NotNull(hold2);
        Assert.Equal(HoldStatus.Held, hold2.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456")] // 16 digits
    [InlineData("123456789012345678")] // 18 digits
    [InlineData("2015269123456789A")] // letters
    public async Task CreateHold_WithInvalidStudentId_ThrowsValidationException(string invalidId)
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 9 - Day", TotalSeats = 40 });

        var holdService = new HoldService(context, _configuration);

        await Assert.ThrowsAsync<ValidationException>(() =>
            holdService.CreateHoldAsync(cls.Id, invalidId));
    }

    [Fact]
    public async Task GetStudentHolds_ReturnsCurrentStudentHoldHistoryWithClassNames()
    {
        var dbName = $"StudentRuleDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);

        var classService = new ClassService(context);
        var cls1 = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 6 - Morning", TotalSeats = 50 });
        var cls2 = await classService.CreateClassAsync(new CreateClassRequest { Name = "Class 7 - Day", TotalSeats = 50 });

        var holdService = new HoldService(context, _configuration);
        var studentId = "20152691234567890";
        var otherStudentId = "20152699999999999";

        // Student applies to cls1 and cls2
        await holdService.CreateHoldAsync(cls1.Id, studentId);
        await holdService.CreateHoldAsync(cls2.Id, studentId);

        // Another student applies to cls1
        await holdService.CreateHoldAsync(cls1.Id, otherStudentId);

        var myHolds = await holdService.GetStudentHoldsAsync(studentId);

        Assert.Equal(2, myHolds.Count);
        Assert.Contains(myHolds, h => h.ClassId == cls1.Id && h.ClassName == "Class 6 - Morning");
        Assert.Contains(myHolds, h => h.ClassId == cls2.Id && h.ClassName == "Class 7 - Day");
    }
}
