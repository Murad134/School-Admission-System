using Microsoft.EntityFrameworkCore;
using SchoolAdmissionSystem.Api.Data;

namespace SchoolAdmissionSystem.Tests;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
