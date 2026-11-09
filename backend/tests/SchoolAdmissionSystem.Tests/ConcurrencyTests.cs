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
    public async Task CreateHold_UnderHighBurstConcurrency_NeverExceedsTotalSeats()
    {
        // Arrange
        var dbName = $"ConcurrencyDb_Burst_{Guid.NewGuid():N}";
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

            // Invariant verification: held + confirmed <= totalSeats
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

    [Fact]
    public async Task CreateHold_SimultaneousRaceForFinalAvailableSeat_AllowsOnlyOneSuccess()
    {
        // Arrange: Class with 10 seats, 9 already held, only 1 seat remaining
        var dbName = $"ConcurrencyDb_FinalSeat_{Guid.NewGuid():N}";
        using (var setupContext = TestDbContextFactory.Create(dbName))
        {
            setupContext.Database.EnsureCreated();

            var classService = new ClassService(setupContext);
            var createdClass = await classService.CreateClassAsync(new CreateClassRequest
            {
                Name = "Class 10 - Science",
                TotalSeats = 10
            });

            var classId = createdClass.Id;
            var holdService = new HoldService(setupContext, _configuration);

            // Pre-hold 9 seats
            for (int i = 1; i <= 9; i++)
            {
                var preHoldStudent = $"2015269000000000{i}";
                await holdService.CreateHoldAsync(classId, preHoldStudent);
            }

            // Verify exactly 1 seat remains before the race
            var preCheck = await classService.GetClassByIdAsync(classId);
            Assert.Equal(1, preCheck.AvailableSeats);
            Assert.Equal(9, preCheck.HeldSeats);

            const int raceContenders = 25;
            var successfulHolds = new ConcurrentBag<HoldResponse>();
            var conflictExceptions = new ConcurrentBag<ConflictException>();
            var unexpectedExceptions = new ConcurrentBag<Exception>();

            // Act: 25 contenders simultaneously race for the 1 final seat
            var tasks = Enumerable.Range(10, raceContenders).Select(async i =>
            {
                using var workerContext = TestDbContextFactory.Create(dbName);
                var workerHoldService = new HoldService(workerContext, _configuration);
                var studentId = $"2015269{i:D10}";

                try
                {
                    var hold = await workerHoldService.CreateHoldAsync(classId, studentId);
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

            // Assert: Exactly 1 contender gets the seat, the other 24 receive 409 Conflict
            Assert.Empty(unexpectedExceptions);
            Assert.Single(successfulHolds);
            Assert.Equal(raceContenders - 1, conflictExceptions.Count);

            // Verify final state: exactly 10 occupied, 0 available, no overselling
            using var verifyContext = TestDbContextFactory.Create(dbName);
            var verifyClassService = new ClassService(verifyContext);
            var finalDetail = await verifyClassService.GetClassByIdAsync(classId);

            Assert.Equal(10, finalDetail.TotalSeats);
            Assert.Equal(10, finalDetail.HeldSeats);
            Assert.Equal(0, finalDetail.AvailableSeats);
            Assert.True(finalDetail.HeldSeats + finalDetail.ConfirmedSeats <= finalDetail.TotalSeats);
        }
    }
}
