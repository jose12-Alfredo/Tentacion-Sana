namespace TentacionSana.Application.Security;

public static class AppRoles
{
    public const string Administrator = "Administrador";
    public const string Sales = "Ventas";
    public const string Production = "Producción";
    public const string Delivery = "Repartidor";
    public const string Finance = "Finanzas";

    public static readonly IReadOnlyCollection<string> All =
    [
        Administrator,
        Sales,
        Production,
        Delivery,
        Finance
    ];
}
