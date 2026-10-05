using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Web.Security;

public static class SecurityEndpointExtensions
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login");

        endpoints.MapPost("/account/logout", LogoutAsync)
            .RequireAuthorization();

        endpoints.MapPost("/account/change-password", ChangePasswordAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        [FromForm] LoginForm form,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext)
    {
        var normalizedUserName = userManager.NormalizeName(form.UserName.Trim());
        var user = await userManager.FindByNameAsync(form.UserName.Trim());

        if (user is null || !user.IsEnabled)
        {
            await RecordSecurityEventAsync(
                dbContext,
                null,
                "LoginFailed",
                normalizedUserName,
                false,
                httpContext);
            return Results.Redirect("/login?error=credenciales");
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            form.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        var eventType = result.IsLockedOut ? "LoginLockedOut" : result.Succeeded ? "LoginSucceeded" : "LoginFailed";
        await RecordSecurityEventAsync(
            dbContext,
            user.Id,
            eventType,
            user.NormalizedUserName,
            result.Succeeded,
            httpContext);

        if (!result.Succeeded)
        {
            return Results.Redirect(result.IsLockedOut ? "/login?error=bloqueada" : "/login?error=credenciales");
        }

        return Results.Redirect(user.MustChangePassword ? "/cambiar-contrasena" : "/app");
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        await signInManager.SignOutAsync();
        await RecordSecurityEventAsync(
            dbContext,
            user?.Id,
            "Logout",
            user?.NormalizedUserName,
            true,
            httpContext);
        return Results.Redirect("/login");
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromForm] ChangePasswordForm form,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null || !user.IsEnabled)
        {
            return Results.Redirect("/login");
        }

        if (!string.Equals(form.NewPassword, form.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.Redirect("/cambiar-contrasena?error=confirmacion");
        }

        var result = await userManager.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded)
        {
            return Results.Redirect("/cambiar-contrasena?error=invalida");
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        await RecordSecurityEventAsync(
            dbContext,
            user.Id,
            "PasswordChanged",
            user.NormalizedUserName,
            true,
            httpContext);

        return Results.Redirect("/app");
    }

    private static async Task RecordSecurityEventAsync(
        ApplicationDbContext dbContext,
        Guid? userId,
        string eventType,
        string? userName,
        bool succeeded,
        HttpContext httpContext)
    {
        dbContext.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            UserName = userName,
            Succeeded = succeeded,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(httpContext.RequestAborted);
    }

    public sealed record LoginForm(string UserName, string Password);

    public sealed record ChangePasswordForm(
        string CurrentPassword,
        string NewPassword,
        string ConfirmPassword);
}
