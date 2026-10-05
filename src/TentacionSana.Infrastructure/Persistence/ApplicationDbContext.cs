using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Idempotency;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Catalog;
using TentacionSana.Domain.Requests;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Orders;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Finance;

namespace TentacionSana.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<ProductPublication> ProductPublications => Set<ProductPublication>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductRequest> ProductRequests => Set<ProductRequest>();
    public DbSet<ProductRequestLine> ProductRequestLines => Set<ProductRequestLine>();
    public DbSet<RequestStatusHistory> RequestStatusHistory => Set<RequestStatusHistory>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<DeliveryPoint> DeliveryPoints => Set<DeliveryPoint>();
    public DbSet<PaymentResponsibleParty> PaymentResponsibleParties => Set<PaymentResponsibleParty>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<OrderChangeHistory> OrderChangeHistory => Set<OrderChangeHistory>();
    public DbSet<PromisedDateHistory> PromisedDateHistory => Set<PromisedDateHistory>();
    public DbSet<OrderSnapshot> OrderSnapshots => Set<OrderSnapshot>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<ProductStockBalance> ProductStockBalances=>Set<ProductStockBalance>();
    public DbSet<ProductionBatch> ProductionBatches=>Set<ProductionBatch>();
    public DbSet<InventoryMovement> InventoryMovements=>Set<InventoryMovement>();
    public DbSet<InventoryMovementAllocation> InventoryMovementAllocations=>Set<InventoryMovementAllocation>();
    public DbSet<StockCount> StockCounts=>Set<StockCount>();
    public DbSet<RecipeVersion> RecipeVersions=>Set<RecipeVersion>();
    public DbSet<ProductCostVersion> ProductCostVersions=>Set<ProductCostVersion>();
    public DbSet<Supply> Supplies=>Set<Supply>();
    public DbSet<SupplyBalance> SupplyBalances=>Set<SupplyBalance>();
    public DbSet<SupplyPurchase> SupplyPurchases=>Set<SupplyPurchase>();
    public DbSet<RecipeIngredient> RecipeIngredients=>Set<RecipeIngredient>();
    public DbSet<SupplyMovement> SupplyMovements=>Set<SupplyMovement>();
    public DbSet<SupplyStockCount> SupplyStockCounts=>Set<SupplyStockCount>();
    public DbSet<Delivery> Deliveries=>Set<Delivery>(); public DbSet<DeliveryLine> DeliveryLines=>Set<DeliveryLine>(); public DbSet<DeliveryStatusHistory> DeliveryStatusHistory=>Set<DeliveryStatusHistory>(); public DbSet<DeliveryEvidence> DeliveryEvidence=>Set<DeliveryEvidence>(); public DbSet<DeliveryRoute> DeliveryRoutes=>Set<DeliveryRoute>(); public DbSet<DeliveryRouteStop> DeliveryRouteStops=>Set<DeliveryRouteStop>(); public DbSet<Sale> Sales=>Set<Sale>(); public DbSet<Payment> Payments=>Set<Payment>(); public DbSet<PaymentAllocation> PaymentAllocations=>Set<PaymentAllocation>(); public DbSet<PaymentEvidence> PaymentEvidence=>Set<PaymentEvidence>(); public DbSet<Receivable> Receivables=>Set<Receivable>(); public DbSet<AccountStatementReport> AccountStatementReports=>Set<AccountStatementReport>(); public DbSet<AccountStatementReportOrder> AccountStatementReportOrders=>Set<AccountStatementReportOrder>(); public DbSet<SettlementObligation> SettlementObligations=>Set<SettlementObligation>(); public DbSet<Settlement> Settlements=>Set<Settlement>(); public DbSet<SettlementAllocation> SettlementAllocations=>Set<SettlementAllocation>();
    public DbSet<CashMovement> CashMovements=>Set<CashMovement>(); public DbSet<CashPayable> CashPayables=>Set<CashPayable>(); public DbSet<CashCount> CashCounts=>Set<CashCount>(); public DbSet<AccountingAccount> AccountingAccounts=>Set<AccountingAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("tentacion_sana");

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
            entity.HasIndex(user => user.NormalizedUserName).IsUnique();
        });

        builder.Entity<IdentityRole<Guid>>().HasData(
            CreateRole(SystemRoleIds.Administrator, AppRoles.Administrator),
            CreateRole(SystemRoleIds.Sales, AppRoles.Sales),
            CreateRole(SystemRoleIds.Production, AppRoles.Production),
            CreateRole(SystemRoleIds.Delivery, AppRoles.Delivery),
            CreateRole(SystemRoleIds.Finance, AppRoles.Finance));

        builder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Action).HasMaxLength(120).IsRequired();
            entity.Property(entry => entry.EntityType).HasMaxLength(160).IsRequired();
            entity.Property(entry => entry.EntityId).HasMaxLength(100);
            entity.Property(entry => entry.Reason).HasMaxLength(500);
            entity.HasIndex(entry => entry.OccurredAtUtc);
            entity.HasIndex(entry => new { entry.EntityType, entry.EntityId });
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Operation).HasMaxLength(120).IsRequired();
            entity.Property(record => record.Key).HasMaxLength(160).IsRequired();
            entity.HasIndex(record => new { record.Operation, record.Key }).IsUnique();
            entity.HasIndex(record => record.ExpiresAtUtc);
        });

        builder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("SecurityEvents");
            entity.HasKey(securityEvent => securityEvent.Id);
            entity.Property(securityEvent => securityEvent.EventType).HasMaxLength(80).IsRequired();
            entity.Property(securityEvent => securityEvent.UserName).HasMaxLength(256);
            entity.Property(securityEvent => securityEvent.IpAddress).HasMaxLength(64);
            entity.HasIndex(securityEvent => securityEvent.OccurredAtUtc);
            entity.HasIndex(securityEvent => securityEvent.UserId);
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(160).IsRequired();
            entity.Property(product => product.Slug).HasMaxLength(180).IsRequired();
            entity.Property(product => product.Presentation).HasMaxLength(160).IsRequired();
            entity.HasIndex(product => product.Slug).IsUnique();
            entity.HasOne<ProductCategory>()
                .WithMany()
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.Property(category => category.Slug).HasMaxLength(140).IsRequired();
            entity.HasIndex(category => category.Slug).IsUnique();
        });

        builder.Entity<ProductPrice>(entity =>
        {
            entity.ToTable("ProductPrices");
            entity.HasKey(price => price.Id);
            entity.Property(price => price.Amount).HasPrecision(12, 2);
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(price => price.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(price => new { price.ProductId, price.EffectiveFromUtc }).IsUnique();
        });

        builder.Entity<ProductPublication>(entity =>
        {
            entity.ToTable("ProductPublications");
            entity.HasKey(publication => publication.ProductId);
            entity.Property(publication => publication.PublicDescription).HasMaxLength(1200).IsRequired();
            entity.Property(publication => publication.ApprovedBenefits).HasMaxLength(800);
            entity.Property(publication => publication.LandingTitle).HasMaxLength(160);
            entity.Property(publication => publication.LandingSubtitle).HasMaxLength(300);
            entity.Property(publication => publication.LandingBadgeText).HasMaxLength(120);
            entity.HasOne<Product>()
                .WithOne()
                .HasForeignKey<ProductPublication>(publication => publication.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(publication => new { publication.IsPublished, publication.DisplayOrder });
            entity.HasIndex(publication => new { publication.IsLandingFeatured, publication.LandingOrder });
        });

        builder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages");
            entity.HasKey(image => image.Id);
            entity.Property(image => image.PublicId).HasMaxLength(300).IsRequired();
            entity.Property(image => image.SecureUrl).HasMaxLength(1000).IsRequired();
            entity.Property(image => image.Format).HasMaxLength(20).IsRequired();
            entity.Property(image => image.AltText).HasMaxLength(240).IsRequired();
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(image => image.PublicId).IsUnique();
            entity.HasIndex(image => new { image.ProductId, image.IsPrimary, image.DisplayOrder });
        });

        builder.Entity<ProductRequest>(entity =>
        {
            entity.ToTable("ProductRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.ContactName).HasMaxLength(160).IsRequired();
            entity.Property(request => request.Phone).HasMaxLength(40).IsRequired();
            entity.Property(request => request.Email).HasMaxLength(256);
            entity.Property(request => request.Notes).HasMaxLength(1500);
            entity.Property(request => request.Location).HasMaxLength(1000);
            entity.Property(request => request.Origin).HasMaxLength(40).IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(request => new { request.Status, request.CreatedAtUtc });
            entity.HasMany(request => request.Lines).WithOne().HasForeignKey(line => line.RequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(request => request.StatusHistory).WithOne().HasForeignKey(history => history.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductRequestLine>(entity =>
        {
            entity.ToTable("ProductRequestLines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ProductName).HasMaxLength(160).IsRequired();
            entity.HasOne<Product>().WithMany().HasForeignKey(line => line.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RequestStatusHistory>(entity =>
        {
            entity.ToTable("RequestStatusHistory");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(history => history.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(history => new { history.RequestId, history.ChangedAtUtc });
        });

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20); entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(30); entity.Property(x => x.Name).HasMaxLength(160).IsRequired(); entity.Property(x => x.Phone).HasMaxLength(40); entity.Property(x => x.Email).HasMaxLength(256); entity.Property(x => x.Notes).HasMaxLength(1500);
            entity.HasIndex(x => x.Phone);
            entity.HasMany(x => x.Contacts).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.DeliveryPoints).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.PaymentResponsibleParties).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CustomerContact>(entity => { entity.ToTable("CustomerContacts"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(160).IsRequired(); entity.Property(x => x.Phone).HasMaxLength(40).IsRequired(); entity.Property(x => x.Email).HasMaxLength(256); entity.Property(x => x.Role).HasMaxLength(120); entity.HasOne<DeliveryPoint>().WithMany().HasForeignKey(x => x.DeliveryPointId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<PaymentResponsibleParty>(entity => { entity.ToTable("PaymentResponsibleParties"); entity.HasKey(x => x.Id); entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30); entity.Property(x => x.Label).HasMaxLength(120); entity.Property(x => x.Name).HasMaxLength(160).IsRequired(); entity.Property(x => x.Phone).HasMaxLength(40).IsRequired(); entity.Property(x => x.Email).HasMaxLength(256); entity.HasOne<CustomerContact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.SetNull); entity.HasIndex(x => new { x.CustomerId, x.IsActive }); });
        builder.Entity<DeliveryPoint>(entity => { entity.ToTable("DeliveryPoints"); entity.HasKey(x => x.Id); entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20); entity.Property(x => x.Label).HasMaxLength(120).IsRequired(); entity.Property(x => x.Address).HasMaxLength(500).IsRequired(); entity.Property(x => x.Reference).HasMaxLength(500); entity.Property(x => x.Location).HasMaxLength(1000); entity.Property(x => x.Notes).HasMaxLength(1500); entity.Property(x => x.ImagePublicId).HasMaxLength(300); entity.Property(x => x.ImageFormat).HasMaxLength(20); entity.Property(x => x.ImageFileName).HasMaxLength(255); entity.HasOne<CustomerContact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.SetNull); entity.HasOne<PaymentResponsibleParty>().WithMany().HasForeignKey(x => x.PaymentResponsiblePartyId).OnDelete(DeleteBehavior.SetNull); });
        builder.HasSequence<long>("OrderNumberSequence").StartsAt(1001);
        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasDefaultValueSql("nextval('\"tentacion_sana\".\"OrderNumberSequence\"')").ValueGeneratedOnAdd();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(1500);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.SourceRequestId).IsUnique();
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryPoint>().WithMany().HasForeignKey(x => x.DeliveryPointId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CustomerContact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PaymentResponsibleParty>().WithMany().HasForeignKey(x => x.PaymentResponsiblePartyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.ChangeHistory).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.PromisedDateHistory).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Snapshots).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Reservations).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<OrderLine>(entity => { entity.ToTable("OrderLines"); entity.HasKey(x => x.Id); entity.Property(x => x.ProductName).HasMaxLength(160).IsRequired(); entity.Property(x => x.StandardUnitPrice).HasPrecision(12,2); entity.Property(x => x.SoldUnitPrice).HasPrecision(12,2); entity.Property(x => x.UnitDiscount).HasPrecision(12,2); entity.Property(x => x.DiscountReason).HasMaxLength(500); entity.Property(x => x.ReplacementReason).HasMaxLength(80); entity.Property(x => x.ReplacementNotes).HasMaxLength(500); entity.Property(x => x.TastingReason).HasMaxLength(80); entity.Property(x => x.TastingNotes).HasMaxLength(500); entity.Property(x => x.IsActive).HasDefaultValue(true); entity.Ignore(x => x.LineTotal); entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<OrderStatusHistory>(entity => { entity.ToTable("OrderStatusHistory"); entity.HasKey(x => x.Id); entity.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30); entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30); entity.Property(x => x.Reason).HasMaxLength(500); entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc }); });
        builder.Entity<OrderChangeHistory>(entity => { entity.ToTable("OrderChangeHistory"); entity.HasKey(x => x.Id); entity.Property(x => x.Field).HasMaxLength(80); entity.Property(x => x.PreviousValue).HasMaxLength(1000); entity.Property(x => x.NewValue).HasMaxLength(1000); entity.Property(x => x.Reason).HasMaxLength(500); entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc }); });
        builder.Entity<PromisedDateHistory>(entity => { entity.ToTable("PromisedDateHistory"); entity.HasKey(x => x.Id); entity.Property(x => x.Reason).HasMaxLength(500); entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc }); });
        builder.Entity<OrderSnapshot>(entity => { entity.ToTable("OrderSnapshots"); entity.HasKey(x => x.Id); entity.Property(x => x.CustomerName).HasMaxLength(160); entity.Property(x => x.DeliveryPointLabel).HasMaxLength(120); entity.Property(x => x.DeliveryAddress).HasMaxLength(500); entity.Property(x => x.DeliveryReference).HasMaxLength(500); entity.Property(x => x.DeliveryLocation).HasMaxLength(1000); entity.Property(x => x.LinesJson).HasColumnType("jsonb"); entity.Property(x => x.ContactName).HasMaxLength(160); entity.Property(x => x.ContactPhone).HasMaxLength(40); entity.Property(x => x.PayerName).HasMaxLength(160); });
        builder.Entity<StockReservation>(entity => { entity.ToTable("StockReservations"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ProductId, x.IsActive }); entity.HasOne<OrderLine>().WithMany().HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Cascade); entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<ProductStockBalance>(e=>{e.ToTable("ProductStockBalances");e.HasKey(x=>x.ProductId);e.Property(x=>x.Version).IsConcurrencyToken();e.Ignore(x=>x.AvailableQuantity);});
        builder.Entity<RecipeVersion>(e=>{e.ToTable("RecipeVersions");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.YieldQuantity).HasPrecision(14,4);e.HasIndex(x=>new{x.ProductId,x.Version}).IsUnique();});
        builder.Entity<ProductCostVersion>(e=>{e.ToTable("ProductCostVersions");e.HasKey(x=>x.Id);e.Property(x=>x.EstimatedUnitCost).HasPrecision(12,2);e.HasIndex(x=>new{x.ProductId,x.EffectiveFromUtc}).IsUnique();});
        builder.Entity<Supply>(e=>{e.ToTable("Supplies");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(160).IsRequired();e.Property(x=>x.BaseUnit).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.PurchasePresentation).HasMaxLength(160).IsRequired();e.Property(x=>x.BaseQuantityPerPackage).HasPrecision(14,4);e.HasIndex(x=>x.Name).IsUnique();});
        builder.Entity<SupplyBalance>(e=>{e.ToTable("SupplyBalances");e.HasKey(x=>x.SupplyId);e.Property(x=>x.Quantity).HasPrecision(16,4);e.Property(x=>x.Version).IsConcurrencyToken();e.HasOne<Supply>().WithOne().HasForeignKey<SupplyBalance>(x=>x.SupplyId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<SupplyPurchase>(e=>{e.ToTable("SupplyPurchases");e.HasKey(x=>x.Id);e.Property(x=>x.PurchasePresentation).HasMaxLength(160).IsRequired();e.Property(x=>x.PackageQuantity).HasPrecision(14,4);e.Property(x=>x.UnitPrice).HasPrecision(14,2);e.Property(x=>x.BaseQuantityPerPackage).HasPrecision(14,4);e.Property(x=>x.TotalBaseQuantity).HasPrecision(16,4);e.Property(x=>x.TotalPrice).HasPrecision(14,2);e.HasOne<Supply>().WithMany().HasForeignKey(x=>x.SupplyId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.SupplyId,x.PurchasedAtUtc});});
        builder.Entity<RecipeIngredient>(e=>{e.ToTable("RecipeIngredients");e.HasKey(x=>x.Id);e.Property(x=>x.RequiredQuantity).HasPrecision(14,4);e.HasOne<RecipeVersion>().WithMany().HasForeignKey(x=>x.RecipeVersionId).OnDelete(DeleteBehavior.Cascade);e.HasOne<Supply>().WithMany().HasForeignKey(x=>x.SupplyId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.RecipeVersionId,x.SupplyId}).IsUnique();});
        builder.Entity<SupplyMovement>(e=>{e.ToTable("SupplyMovements");e.HasKey(x=>x.Id);e.Property(x=>x.Kind).HasConversion<string>().HasMaxLength(40);e.Property(x=>x.Quantity).HasPrecision(16,4);e.Property(x=>x.HistoricalBaseUnitCost).HasPrecision(16,6);e.Property(x=>x.Reason).HasMaxLength(500);e.HasOne<Supply>().WithMany().HasForeignKey(x=>x.SupplyId).OnDelete(DeleteBehavior.Restrict);e.HasOne<ProductionBatch>().WithMany().HasForeignKey(x=>x.ProductionBatchId).OnDelete(DeleteBehavior.Restrict);e.HasOne<SupplyPurchase>().WithMany().HasForeignKey(x=>x.PurchaseId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.SupplyId,x.OccurredAtUtc});});
        builder.Entity<SupplyStockCount>(e=>{e.ToTable("SupplyStockCounts");e.HasKey(x=>x.Id);e.Property(x=>x.ExpectedQuantity).HasPrecision(16,4);e.Property(x=>x.CountedQuantity).HasPrecision(16,4);e.Property(x=>x.Difference).HasPrecision(16,4);e.Property(x=>x.Reason).HasMaxLength(500);e.HasOne<Supply>().WithMany().HasForeignKey(x=>x.SupplyId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.SupplyId,x.CountedAtUtc});});
        builder.Entity<ProductionBatch>(e=>{e.ToTable("ProductionBatches");e.HasKey(x=>x.Id);e.Property(x=>x.Number).HasMaxLength(40);e.HasIndex(x=>x.Number).IsUnique();e.Property(x=>x.EstimatedUnitCost).HasPrecision(12,2);e.Property(x=>x.EstimatedTotalCost).HasPrecision(14,2);e.Property(x=>x.VoidReason).HasMaxLength(500);e.Property(x=>x.Version).IsConcurrencyToken();});
        builder.Entity<InventoryMovement>(e=>{e.ToTable("InventoryMovements");e.HasKey(x=>x.Id);e.Property(x=>x.Kind).HasConversion<string>().HasMaxLength(40);e.Property(x=>x.Reason).HasMaxLength(500);e.Property(x=>x.HistoricalUnitCost).HasPrecision(12,2);e.Property(x=>x.HistoricalTotalCost).HasPrecision(14,2);e.HasIndex(x=>new{x.ProductId,x.OccurredAtUtc});});
        builder.Entity<InventoryMovementAllocation>(e=>{e.ToTable("InventoryMovementAllocations");e.HasKey(x=>x.Id);e.Property(x=>x.HistoricalUnitCost).HasPrecision(12,2);e.HasOne<InventoryMovement>().WithMany().HasForeignKey(x=>x.MovementId).OnDelete(DeleteBehavior.Cascade);e.HasOne<ProductionBatch>().WithMany().HasForeignKey(x=>x.ProductionBatchId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<StockCount>(e=>{e.ToTable("StockCounts");e.HasKey(x=>x.Id);e.Property(x=>x.Reason).HasMaxLength(500);e.HasIndex(x=>new{x.ProductId,x.CountedAtUtc});});
        builder.Entity<Delivery>(e=>{e.ToTable("Deliveries");e.HasKey(x=>x.Id);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.ReceiverName).HasMaxLength(160);e.Property(x=>x.FailureReason).HasMaxLength(500);e.Property(x=>x.Version).IsConcurrencyToken();e.HasMany(x=>x.Lines).WithOne().HasForeignKey(x=>x.DeliveryId);e.HasMany(x=>x.History).WithOne().HasForeignKey(x=>x.DeliveryId);});
        builder.Entity<DeliveryLine>(e=>{e.ToTable("DeliveryLines");e.HasKey(x=>x.Id);e.Ignore(x=>x.PendingQuantity);e.HasOne<OrderLine>().WithMany().HasForeignKey(x=>x.OrderLineId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<DeliveryStatusHistory>(e=>{e.ToTable("DeliveryStatusHistory");e.HasKey(x=>x.Id);e.Property(x=>x.PreviousStatus).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.NewStatus).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.Reason).HasMaxLength(500);});
        builder.Entity<DeliveryEvidence>(e=>{e.ToTable("DeliveryEvidence");e.HasKey(x=>x.Id);e.Property(x=>x.PublicId).HasMaxLength(300).IsRequired();e.Property(x=>x.FileName).HasMaxLength(255).IsRequired();e.Property(x=>x.Format).HasMaxLength(20).IsRequired();e.HasIndex(x=>x.PublicId).IsUnique();e.HasIndex(x=>new{x.DeliveryId,x.UploadedAtUtc});e.HasOne<Delivery>().WithMany().HasForeignKey(x=>x.DeliveryId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<DeliveryRoute>(e=>{e.ToTable("DeliveryRoutes");e.HasKey(x=>x.Id);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.Version).IsConcurrencyToken();e.HasIndex(x=>new{x.DriverUserId,x.PromisedDateUtc});e.HasMany(x=>x.Stops).WithOne().HasForeignKey(x=>x.DeliveryRouteId).OnDelete(DeleteBehavior.Cascade);});
        builder.Entity<DeliveryRouteStop>(e=>{e.ToTable("DeliveryRouteStops");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.DeliveryRouteId,x.Position}).IsUnique();e.HasIndex(x=>x.DeliveryId).IsUnique();e.HasOne<Delivery>().WithMany().HasForeignKey(x=>x.DeliveryId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<Sale>(e=>{e.ToTable("Sales");e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(14,2);e.Property(x=>x.HistoricalCost).HasPrecision(14,2);e.HasIndex(x=>x.DeliveryId);});
        builder.Entity<Payment>(e=>{e.ToTable("Payments");e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(14,2);e.Property(x=>x.Method).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.ExternalReference).HasMaxLength(160);e.Property(x=>x.Notes).HasMaxLength(500);e.HasIndex(x=>new{x.ResponsiblePartyId,x.PaymentDateUtc});});
        builder.Entity<PaymentAllocation>(e=>{e.ToTable("PaymentAllocations");e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(14,2);e.HasOne<Payment>().WithMany().HasForeignKey(x=>x.PaymentId).OnDelete(DeleteBehavior.Restrict);e.HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.PaymentId,x.OrderId}).IsUnique();e.HasIndex(x=>new{x.OrderId,x.AppliedAtUtc});});
        builder.Entity<PaymentEvidence>(e=>{e.ToTable("PaymentEvidence");e.HasKey(x=>x.Id);e.Property(x=>x.PublicId).HasMaxLength(300).IsRequired();e.Property(x=>x.FileName).HasMaxLength(255).IsRequired();e.Property(x=>x.Format).HasMaxLength(20).IsRequired();e.HasIndex(x=>x.PaymentId).IsUnique();e.HasIndex(x=>x.PublicId).IsUnique();e.HasOne<Payment>().WithOne().HasForeignKey<PaymentEvidence>(x=>x.PaymentId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<Receivable>(e=>{e.ToTable("Receivables");e.HasKey(x=>x.OrderId);e.Property(x=>x.InvoicedAmount).HasPrecision(14,2);e.Property(x=>x.PaidAmount).HasPrecision(14,2);e.Property(x=>x.Version).IsConcurrencyToken();e.Ignore(x=>x.Balance);});
        builder.Entity<AccountStatementReport>(e=>{e.ToTable("AccountStatementReports");e.HasKey(x=>x.Id);e.Property(x=>x.ReportNumber).HasMaxLength(100).IsRequired();e.Property(x=>x.Type).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.BalanceAtGeneration).HasPrecision(14,2);e.Property(x=>x.PreviousBalance).HasPrecision(14,2);e.Property(x=>x.RegisteredPayment).HasPrecision(14,2);e.HasIndex(x=>x.ReportNumber).IsUnique();e.HasIndex(x=>new{x.ResponsiblePartyId,x.GeneratedAtUtc});e.HasMany(x=>x.Orders).WithOne().HasForeignKey(x=>x.ReportId).OnDelete(DeleteBehavior.Cascade);});
        builder.Entity<AccountStatementReportOrder>(e=>{e.ToTable("AccountStatementReportOrders");e.HasKey(x=>x.Id);e.Property(x=>x.Total).HasPrecision(14,2);e.Property(x=>x.Paid).HasPrecision(14,2);e.Property(x=>x.Balance).HasPrecision(14,2);e.HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.ReportId,x.Position}).IsUnique();});
        builder.Entity<SettlementObligation>(e=>{e.ToTable("SettlementObligations");e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(14,2);e.Property(x=>x.SettledAmount).HasPrecision(14,2);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.Version).IsConcurrencyToken();e.Ignore(x=>x.PendingAmount);e.HasIndex(x=>x.PaymentId).IsUnique();e.HasIndex(x=>new{x.HolderUserId,x.Status});});
        builder.Entity<Settlement>(e=>{e.ToTable("Settlements");e.HasKey(x=>x.Id);e.Property(x=>x.DeclaredAmount).HasPrecision(14,2);e.Property(x=>x.ReceivedAmount).HasPrecision(14,2);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.DifferenceReason).HasMaxLength(500);e.Property(x=>x.Reference).HasMaxLength(160);e.Ignore(x=>x.Difference);e.HasMany(x=>x.Allocations).WithOne().HasForeignKey(x=>x.SettlementId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.HolderUserId,x.ReceivedAtUtc});});
        builder.Entity<SettlementAllocation>(e=>{e.ToTable("SettlementAllocations");e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(14,2);e.HasOne<SettlementObligation>().WithMany().HasForeignKey(x=>x.ObligationId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.SettlementId,x.ObligationId}).IsUnique();});
        builder.Entity<AccountingAccount>(e=>{e.ToTable("AccountingAccounts");e.HasKey(x=>x.Id);e.Property(x=>x.Code).HasMaxLength(20).IsRequired();e.Property(x=>x.Name).HasMaxLength(120).IsRequired();e.Property(x=>x.Kind).HasConversion<string>().HasMaxLength(30);e.HasIndex(x=>x.Code).IsUnique();e.HasIndex(x=>x.Name).IsUnique();e.HasIndex(x=>new{x.Kind,x.IsActive});});
        builder.Entity<CashMovement>(e=>{e.ToTable("CashMovements");e.HasKey(x=>x.Id);e.Property(x=>x.Account).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.Direction).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.Source).HasConversion<string>().HasMaxLength(30);e.Property(x=>x.Detail).HasMaxLength(500).IsRequired();e.Property(x=>x.Category).HasMaxLength(80).IsRequired();e.Property(x=>x.Amount).HasPrecision(14,2);e.Property(x=>x.EvidencePublicId).HasMaxLength(300).IsRequired();e.Property(x=>x.EvidenceFileName).HasMaxLength(255).IsRequired();e.Property(x=>x.EvidenceFormat).HasMaxLength(20).IsRequired();e.HasIndex(x=>x.PaymentId).IsUnique();e.HasIndex(x=>x.SupplyPurchaseId).IsUnique();e.HasIndex(x=>x.TransferId);e.HasIndex(x=>x.AccountingAccountId);e.HasIndex(x=>new{x.Account,x.OccurredAtUtc});e.HasOne<AccountingAccount>().WithMany().HasForeignKey(x=>x.AccountingAccountId).OnDelete(DeleteBehavior.Restrict);});
        builder.Entity<CashPayable>(e=>{e.ToTable("CashPayables");e.HasKey(x=>x.Id);e.Property(x=>x.PersonName).HasMaxLength(160).IsRequired();e.Property(x=>x.Amount).HasPrecision(14,2);e.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20);e.Property(x=>x.EvidencePublicId).HasMaxLength(300).IsRequired();e.Property(x=>x.EvidenceFileName).HasMaxLength(255).IsRequired();e.Property(x=>x.EvidenceFormat).HasMaxLength(20).IsRequired();e.HasIndex(x=>x.SupplyPurchaseId).IsUnique();});
        builder.Entity<CashCount>(e=>{e.ToTable("CashCounts");e.HasKey(x=>x.Id);e.Property(x=>x.ExpectedAmount).HasPrecision(14,2);e.Property(x=>x.CountedAmount).HasPrecision(14,2);e.Property(x=>x.Difference).HasPrecision(14,2);e.Property(x=>x.Observation).HasMaxLength(500);e.Property(x=>x.EvidencePublicId).HasMaxLength(300);e.Property(x=>x.EvidenceFileName).HasMaxLength(255);e.Property(x=>x.EvidenceFormat).HasMaxLength(20);e.HasIndex(x=>x.CountedAtUtc);});
    }

    private static IdentityRole<Guid> CreateRole(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString()
    };
}
