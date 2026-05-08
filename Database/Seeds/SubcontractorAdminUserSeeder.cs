using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.Entities;
using Portlink.Api.Helpers;

namespace Portlink.Api.Database.Seeds;

public static class SubcontractorAdminUserSeeder
{
    private const string Email = "Sadmin@gmail.com";
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
            Role = "subcontractor",
            IsVerified = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        db.SubcontractorProfiles.Add(new SubcontractorProfile
        {
            UserId = user.Id,
            FullName = "Portlink Subcontractor Admin",
            CompanyName = "Portlink Subcontractor",
            Country = "Turkey",
            City = "Istanbul",
            IsVerified = true,
            ExpertiseTags = new List<string> { "General" },
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
    }
}
