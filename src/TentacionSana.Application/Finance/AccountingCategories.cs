namespace TentacionSana.Application.Finance;

public enum AccountingCategoryKind { DirectCost, OperatingExpense, Investment }

public static class AccountingCategories
{
    public static readonly IReadOnlyList<string> Income =
        ["Cobro manual", "Anticipo", "Aporte de socio", "Devolución", "Otro ingreso"];

    public static readonly IReadOnlyList<string> DirectCosts =
        ["Ingredientes", "Envases", "Etiquetas"];

    public static readonly IReadOnlyList<string> OperatingExpenses =
        ["Delivery", "Transporte", "Pasajes", "Publicidad", "Servicios", "Utensilios menores", "Otros"];

    public static readonly IReadOnlyList<string> Investments =
        ["Equipos", "Equipos de cocina"];

    public static IReadOnlyList<string> Expenses { get; } =
        [.. DirectCosts, .. OperatingExpenses, .. Investments];

    public static AccountingCategoryKind ClassifyExpense(string category) =>
        DirectCosts.Contains(category) ? AccountingCategoryKind.DirectCost :
        Investments.Contains(category) ? AccountingCategoryKind.Investment :
        AccountingCategoryKind.OperatingExpense;

    public static bool IsOtherIncome(string category) =>
        category is "Cobro manual" or "Devolución" or "Otro ingreso";
}
