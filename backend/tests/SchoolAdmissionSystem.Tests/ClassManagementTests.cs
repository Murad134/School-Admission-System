using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Tests;

public class ClassManagementTests
{
    [Fact]
    public async Task CreateClass_WithValidData_ReturnsCreatedClass()
    {
        var dbName = $"ClassMgmtDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);
        var service = new ClassService(context);

        var request = new CreateClassRequest
        {
            Name = "Class 6 - Morning Shift",
            TotalSeats = 120
        };

        var result = await service.CreateClassAsync(request);

        Assert.NotNull(result);
        Assert.StartsWith("c_", result.Id);
        Assert.Equal("Class 6 - Morning Shift", result.Name);
        Assert.Equal(120, result.TotalSeats);
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("Class 1", 0)]
    [InlineData("Class 1", -5)]
    public async Task CreateClass_WithInvalidData_ThrowsValidationException(string name, int totalSeats)
    {
        var dbName = $"ClassMgmtDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);
        var service = new ClassService(context);

        var request = new CreateClassRequest
        {
            Name = name,
            TotalSeats = totalSeats
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateClassAsync(request));
    }

    [Fact]
    public async Task GetAllClasses_ReturnsAllClassesWithAccurateSeatCounts()
    {
        var dbName = $"ClassMgmtDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);
        var service = new ClassService(context);

        await service.CreateClassAsync(new CreateClassRequest { Name = "Class 6 - Morning", TotalSeats = 60 });
        await service.CreateClassAsync(new CreateClassRequest { Name = "Class 6 - Day", TotalSeats = 80 });

        var classes = await service.GetAllClassesAsync();

        Assert.Equal(2, classes.Count);
        Assert.Contains(classes, c => c.Name == "Class 6 - Morning" && c.TotalSeats == 60 && c.AvailableSeats == 60);
        Assert.Contains(classes, c => c.Name == "Class 6 - Day" && c.TotalSeats == 80 && c.AvailableSeats == 80);
    }

    [Fact]
    public async Task GetClassById_WhenNonExistent_ThrowsNotFoundException()
    {
        var dbName = $"ClassMgmtDb_{Guid.NewGuid():N}";
        using var context = TestDbContextFactory.Create(dbName);
        var service = new ClassService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetClassByIdAsync("c_non_existent"));
    }
}
