using Microsoft.EntityFrameworkCore;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.IntegrationTests.Persistence;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void ModelUsesExpectedSchemaAndFoundationTables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;

        using var context = new ApplicationDbContext(options);
        var relationalModel = context.Model.GetRelationalModel();

        Assert.NotNull(relationalModel.FindTable("AspNetUsers", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("AuditEntries", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("IdempotencyRecords", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SecurityEvents", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Products", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("ProductPrices", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("ProductPublications", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("ProductImages", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("ProductRequests", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("RequestStatusHistory", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Customers", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("CustomerContacts", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("DeliveryPoints", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("PaymentResponsibleParties", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Orders", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Deliveries", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("DeliveryEvidence", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Payments", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("PaymentAllocations", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Receivables", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("AccountStatementReports", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("AccountStatementReportOrders", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SettlementObligations", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("Supplies", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SupplyBalances", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SupplyPurchases", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("RecipeIngredients", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SupplyMovements", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("SupplyStockCounts", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("CashMovements", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("CashPayables", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("CashCounts", "tentacion_sana"));
        Assert.NotNull(relationalModel.FindTable("AccountingAccounts", "tentacion_sana"));
    }

    [Fact]
    public void IdempotencyKeyHasUniqueDatabaseConstraint()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;

        using var context = new ApplicationDbContext(options);
        var entity = context.Model.FindEntityType("TentacionSana.Infrastructure.Idempotency.IdempotencyRecord");

        var uniqueIndex = Assert.Single(entity!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["Operation", "Key"]));
        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void RequestConversionHasUniqueSourceRequestConstraint()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType("TentacionSana.Domain.Orders.Order")!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["SourceRequestId"]));
    }

    [Fact]
    public void DeliveryPointKeepsIndependentContactAndPaymentResponsibleRelationships()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType("TentacionSana.Domain.Customers.DeliveryPoint")!;
        var foreignKeys = entity.GetForeignKeys().ToList();

        Assert.Contains(foreignKeys, key => key.Properties.Select(x => x.Name).SequenceEqual(["ContactId"]));
        Assert.Contains(foreignKeys, key => key.Properties.Select(x => x.Name).SequenceEqual(["PaymentResponsiblePartyId"]));
        Assert.All(foreignKeys.Where(x => x.Properties[0].Name is "ContactId" or "PaymentResponsiblePartyId"), key => Assert.Equal(DeleteBehavior.SetNull, key.DeleteBehavior));
    }

    [Fact]
    public void CashLedgerPreventsDuplicatingPaymentAndPurchaseMovements()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType("TentacionSana.Domain.Finance.CashMovement")!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["PaymentId"]));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["SupplyPurchaseId"]));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        return new ApplicationDbContext(options);
    }
}
