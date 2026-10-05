using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Security;
using TentacionSana.Infrastructure;

namespace TentacionSana.IntegrationTests.Security;

public sealed class IdentityConfigurationTests
{
    [Fact]
    public void IdentityUsesRequiredLockoutSettingsAndInternalPolicy()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Database=configuration_test;Username=test;Password=test"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var identityOptions = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var authorizationOptions = provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>()
            .Value;

        Assert.Equal(5, identityOptions.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identityOptions.Lockout.DefaultLockoutTimeSpan);
        Assert.NotNull(authorizationOptions.GetPolicy(AppPolicies.InternalAccess));
    }
}
