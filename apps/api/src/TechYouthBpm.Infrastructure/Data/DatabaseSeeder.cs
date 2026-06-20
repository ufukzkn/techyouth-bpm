using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Users.AddRange(
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Username = "admin",
                DisplayName = "Admin User",
                Password = "admin123",
                Role = Role.Admin
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Username = "user",
                DisplayName = "Process Starter",
                Password = "user123",
                Role = Role.User
            },
            new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Username = "approver",
                DisplayName = "Process Approver",
                Password = "approver123",
                Role = Role.Approver
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
