namespace TentacionSana.Application.Security;

public static class AppPermissions
{
    public const string ClaimType = "permission";
    public const string Administration = "administration.manage";
    public const string Sales = "sales.manage";
    public const string Production = "production.manage";
    public const string Deliveries = "deliveries.manage";
    public const string Finance = "finance.manage";

    public static readonly IReadOnlyList<PermissionOption> All =
    [
        new(Administration, "Administración de usuarios", "Crear usuarios y roles, asignar permisos y habilitar cuentas."),
        new(Sales, "Ventas y pedidos", "Clientes, solicitudes, pedidos, productos y coordinación de entregas."),
        new(Production, "Producción e inventario", "Compras de insumos, recetas, faltantes, lotes, inventario terminado y reservas."),
        new(Deliveries, "Reparto", "Ruta personal, confirmación de entregas y evidencias."),
        new(Finance, "Finanzas", "Cuentas por cobrar, pagos y saldos.")
    ];
}

public sealed record PermissionOption(string Key,string Name,string Description);
public interface IUserAdministrationService
{
    Task<UserAdministrationSnapshot> GetAsync(CancellationToken cancellationToken=default);
    Task<SecurityOperationResult> CreateUserAsync(CreateUserCommand command,Guid actorId,CancellationToken cancellationToken=default);
    Task<SecurityOperationResult> UpdateUserAsync(UpdateUserCommand command,Guid actorId,CancellationToken cancellationToken=default);
    Task<SecurityOperationResult> CreateRoleAsync(CreateRoleCommand command,Guid actorId,CancellationToken cancellationToken=default);
    Task<SecurityOperationResult> UpdateRolePermissionsAsync(UpdateRolePermissionsCommand command,Guid actorId,CancellationToken cancellationToken=default);
}
public sealed record CreateUserCommand(string DisplayName,string UserName,string TemporaryPassword,IReadOnlyList<Guid> RoleIds);
public sealed record UpdateUserCommand(Guid UserId,string DisplayName,bool IsEnabled,IReadOnlyList<Guid> RoleIds);
public sealed record CreateRoleCommand(string Name,IReadOnlyList<string> Permissions);
public sealed record UpdateRolePermissionsCommand(Guid RoleId,IReadOnlyList<string> Permissions);
public sealed record SecurityOperationResult(bool Succeeded,IReadOnlyList<string> Errors);
public sealed record UserAdministrationSnapshot(IReadOnlyList<ManagedUser> Users,IReadOnlyList<ManagedRole> Roles,IReadOnlyList<PermissionOption> Permissions);
public sealed record ManagedUser(Guid Id,string DisplayName,string UserName,bool IsEnabled,bool MustChangePassword,IReadOnlyList<Guid> RoleIds,IReadOnlyList<string> RoleNames);
public sealed record ManagedRole(Guid Id,string Name,bool IsSystem,IReadOnlyList<string> Permissions,int UserCount);
