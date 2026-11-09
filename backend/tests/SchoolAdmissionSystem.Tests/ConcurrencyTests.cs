using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using SchoolAdmissionSystem.Api.DTOs;
using SchoolAdmissionSystem.Api.Exceptions;
using SchoolAdmissionSystem.Api.Services;

namespace SchoolAdmissionSystem.Tests;

public class ConcurrencyTests
{
    private readonly IConfiguration _configuration;

    public ConcurrencyTests()
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
    public async Task CreateHold_UnderHighConcurrency_NeverExceedsTotalSeats()
    {
        // Arrange
        var dbName = $"ConcurrencyDb_{Guid.NewGuid():N}";
        using (var setupContext = TestDbContextFactory.Create(dbName))
        {
            setupContext.Database.EnsureCreated();

            var classService = new ClassService(setupContext);
            var createdClass = await classService.CreateClassAsync(new CreateClassRequest
            {
                Name = "Class 6 - Morning Shift",
                TotalSeats = 5
            });

            var classId = createdClass.Id;
            const int concurrentRequests = 50;
            const int totalSeats = 5;

            var successfulHolds = new ConcurrentBag<HoldResponse>();
            var conflictExceptions = new ConcurrentBag<ConflictException>();
            var unexpectedExceptions = new ConcurrentBag<Exception>();

            // Act: Fire 50 simultaneous hold requests from 50 unique 17-digit student IDs
            var tasks = Enumerable.Range(1, concurrentRequests).Select(async i =>
            {
                using var workerContext = TestDbContextFactory.Create(dbName);
                var holdService = new HoldService(workerContext, _configuration);
                var studentId = $"2015269{i:D10}"; // 17 digits

                try
                {
                    var hold = await holdService.CreateHoldAsync(classId, studentId);
                    successfulHolds.Add(hold);
                }
                catch (ConflictException cex)
                {
                    conflictExceptions.Add(cex);
                }
                catch (Exception ex)
                {
                    unexpectedExceptions.Add(ex);
                }
            });

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(unexpectedExceptions);
            Assert.Equal(totalSeats, successfulHolds.Count);
            Assert.Equal(concurrentRequests - totalSeats, conflictExceptions.Count);

            // Verify with ClassService that seat inventory invariant is strictly satisfied
            using var verifyContext = TestDbContextFactory.Create(dbName);
            var verifyClassService = new ClassService(verifyContext);
            var classDetail = await verifyClassService.GetClassByIdAsync(classId);

            Assert.Equal(totalSeats, classDetail.TotalSeats);
            Assert.Equal(totalSeats, classDetail.HeldSeats);
            Assert.Equal(0, classDetail.ConfirmedSeats);
            Assert.Equal(0, classDetail.AvailableSeats);
            Assert.True(classDetail.HeldSeats + classDetail.ConfirmedSeats <= classDetail.TotalSeats);
        }
    }
}
