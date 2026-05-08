using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.Entities;
using Portlink.Api.Helpers;
using Portlink.Api.Modules.Auth.Entities;

namespace Portlink.Api.Database.Seeds;

public static class AgencyAdminUserSeeder
{
    private const string Email = "Aadmin@gmail.com";
    private const string Password = "123";

    public static async Task SeedAsync(AppDbContext db)
    {
        var normalizedEmail = Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            return;

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = PasswordHelper.Hash(Password),
            Role = "agent",
            IsVerified = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        db.AgentProfiles.Add(new AgentProfile
        {
            UserId = user.Id,
            FullName = "Portlink Agency Admin",
            CompanyName = "Portlink Agency",
            Country = "Turkey",
            City = "Istanbul",
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
    }
}
