using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Security;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Identity;

public sealed class InitialAdministratorSeeder(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext)
{
    public async Task SeedAsync(
        string userName,
        string displayName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "La creación inicial solo está permitida cuando todavía no existe ningún usuario.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            DisplayName = displayName.Trim(),
            IsEnabled = true,
            MustChangePassword = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LockoutEnabled = true
        };

        var creationResult = await userManager.CreateAsync(user, temporaryPassword);
        if (!creationResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                creationResult.Errors.Select(error => error.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Administrator);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                roleResult.Errors.Select(error => error.Description)));
        }

        dbContext.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EventType = "InitialAdministratorCreated",
            UserName = user.NormalizedUserName,
            Succeeded = true,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
