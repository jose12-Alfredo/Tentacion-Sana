using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TentacionSana.Application.Security;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Persistence;
using TentacionSana.Application.Catalog;
using TentacionSana.Infrastructure.Catalog;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Application.Requests;
using TentacionSana.Infrastructure.Requests;
using TentacionSana.Application.Customers;
using TentacionSana.Infrastructure.Customers;
using TentacionSana.Application.Orders;
using TentacionSana.Infrastructure.Orders;
using TentacionSana.Application.Inventory;
using TentacionSana.Infrastructure.Inventory;
using TentacionSana.Application.Deliveries;
using TentacionSana.Infrastructure.Deliveries;
using TentacionSana.Application.Settlements;
using TentacionSana.Infrastructure.Settlements;
using TentacionSana.Application.Dashboard;
using TentacionSana.Infrastructure.Dashboard;
using TentacionSana.Application.Receivables;
using TentacionSana.Infrastructure.Receivables;
using TentacionSana.Application.Finance;
using TentacionSana.Infrastructure.Finance;

namespace TentacionSana.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection mediante secretos locales o variables de entorno.");
        }

        var connectionOptions = new NpgsqlConnectionStringBuilder(connectionString);
        connectionOptions.Timeout = Math.Max(connectionOptions.Timeout, 30);
        connectionOptions.KeepAlive = 30;

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionOptions.ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "TentacionSana.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/acceso-denegado";
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.InternalAccess, policy =>
            {
                RequireInternalAccount(policy);
            })
            .AddPolicy(AppPolicies.Administration, policy =>
            {
                RequireInternalAccount(policy);
                policy.RequireAssertion(c => c.User.IsInRole(AppRoles.Administrator) || c.User.HasClaim(AppPermissions.ClaimType, AppPermissions.Administration));
            })
            .AddPolicy(AppPolicies.ManageSales, policy =>
            {
                RequireInternalAccount(policy);
                policy.RequireAssertion(c => c.User.IsInRole(AppRoles.Administrator) || c.User.IsInRole(AppRoles.Sales) || c.User.HasClaim(AppPermissions.ClaimType, AppPermissions.Sales));
            })
            .AddPolicy(AppPolicies.ManageProduction, policy =>
            {
                RequireInternalAccount(policy);
                policy.RequireAssertion(c => c.User.IsInRole(AppRoles.Administrator) || c.User.IsInRole(AppRoles.Production) || c.User.HasClaim(AppPermissions.ClaimType, AppPermissions.Production));
            })
            .AddPolicy(AppPolicies.ManageDeliveries, policy =>
            {
                RequireInternalAccount(policy);
                policy.RequireAssertion(c => c.User.IsInRole(AppRoles.Administrator) || c.User.IsInRole(AppRoles.Delivery) || c.User.HasClaim(AppPermissions.ClaimType, AppPermissions.Deliveries));
            })
            .AddPolicy(AppPolicies.ManageFinance, policy =>
            {
                RequireInternalAccount(policy);
                policy.RequireAssertion(c => c.User.IsInRole(AppRoles.Administrator) || c.User.IsInRole(AppRoles.Finance) || c.User.HasClaim(AppPermissions.ClaimType, AppPermissions.Finance));
            });

        services.AddScoped<InitialAdministratorSeeder>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<IPublicCatalogService>(provider => provider.GetRequiredService<CatalogService>());
        services.AddScoped<IProductManagementService>(provider => provider.GetRequiredService<CatalogService>());
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddScoped<IProductImageManagementService, CloudinaryProductImageService>();
        services.AddScoped<IDeliveryEvidenceService, CloudinaryDeliveryEvidenceService>();
        services.AddScoped<IDeliveryPointImageService, CloudinaryDeliveryPointImageService>();
        services.AddScoped<IProductRequestService, ProductRequestService>();
        services.AddScoped<IOperationsQueryService, OperationsQueryService>();
        services.AddScoped<ICustomerManagementService, CustomerManagementService>();
        services.AddScoped<IOrderManagementService, OrderManagementService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddMemoryCache();
        services.AddHttpClient<LocationCoordinateResolver>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TentacionSana/1.0 (delivery-map)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IReceivablesService, ReceivablesService>();
        services.AddScoped<ICashLedgerService, CashLedgerService>();
        services.AddScoped<IAccountingAccountService, AccountingAccountService>();
        services.AddScoped<IIncomeStatementService, IncomeStatementService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    private static void RequireInternalAccount(AuthorizationPolicyBuilder policy)
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("account_enabled", "true");
        policy.RequireClaim("must_change_password", "false");
    }
}
