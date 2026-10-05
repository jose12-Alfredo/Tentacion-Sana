using System.Threading.RateLimiting;
using System.Security.Claims;
using CloudinaryDotNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Infrastructure;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Persistence;
using TentacionSana.Application.Orders;
using TentacionSana.Application.Inventory;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Catalog;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Orders;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Customers;
using TentacionSana.Application.Finance;
using TentacionSana.Application.Security;
using TentacionSana.Web.Components;
using TentacionSana.Web.Security;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--check-cloudinary", StringComparer.OrdinalIgnoreCase))
{
    var cloudName = builder.Configuration["Cloudinary:CloudName"];
    var apiKey = builder.Configuration["Cloudinary:ApiKey"];
    var apiSecret = builder.Configuration["Cloudinary:ApiSecret"];
    if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
    {
        throw new InvalidOperationException("Falta configurar uno o más valores de Cloudinary.");
    }

    var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
    var result = await cloudinary.PingAsync();
    if (result.Error is not null)
    {
        throw new InvalidOperationException($"Cloudinary rechazó la conexión: {result.Error.Message}");
    }

    Console.WriteLine("Cloudinary conectado correctamente.");
    return;
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtectionKeysDirectory = new DirectoryInfo(
    string.IsNullOrWhiteSpace(dataProtectionKeysPath)
        ? Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys")
        : dataProtectionKeysPath);
dataProtectionKeysDirectory.Create();
builder.Services.AddDataProtection()
    .SetApplicationName("TentacionSana")
    .PersistKeysToFileSystem(dataProtectionKeysDirectory);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("public-product-requests", httpContext =>
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("public-product-requests-read");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

if (args.Contains("--check-phase3", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var requestService = scope.ServiceProvider.GetRequiredService<TentacionSana.Application.Requests.IProductRequestService>();
    var customerService = scope.ServiceProvider.GetRequiredService<TentacionSana.Application.Customers.ICustomerManagementService>();
    var userId = await dbContext.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    var productId = await dbContext.ProductPublications.AsNoTracking().Where(x => x.IsPublished)
        .Join(dbContext.Products.Where(x => x.IsActive), publication => publication.ProductId, product => product.Id, (_, product) => product.Id).OrderBy(x => x).FirstAsync();
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var phoneSuffix = Random.Shared.Next(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
    var key = $"phase3-{suffix}";
    var trackedIds = new HashSet<Guid>();
    Guid? convertedCustomerId = null;

    try
    {
        var command = new TentacionSana.Application.Requests.CreateRequestCommand($"Prueba fase 3 {suffix}", $"7000{phoneSuffix}", $"phase3-{suffix}@example.test", "Recorrido automatizado", "https://maps.example/test", productId, 2, true, key);
        var first = await requestService.CreateAsync(command);
        var duplicate = await requestService.CreateAsync(command);
        if (!first.Succeeded || first.RequestId != duplicate.RequestId) throw new InvalidOperationException("Falló la idempotencia del envío público.");
        trackedIds.Add(first.RequestId!.Value);

        var contacted = await requestService.ChangeStatusAsync(first.RequestId.Value, "Contacted", "Verificación integral.", userId);
        if (!contacted.Succeeded) throw new InvalidOperationException(string.Join(" ", contacted.Errors));
        await using var conversionScope1 = app.Services.CreateAsyncScope();
        await using var conversionScope2 = app.Services.CreateAsyncScope();
        var conversionTask1 = conversionScope1.ServiceProvider.GetRequiredService<TentacionSana.Application.Requests.IProductRequestService>().ConvertAsync(first.RequestId.Value, userId);
        var conversionTask2 = conversionScope2.ServiceProvider.GetRequiredService<TentacionSana.Application.Requests.IProductRequestService>().ConvertAsync(first.RequestId.Value, userId);
        var conversions = await Task.WhenAll(conversionTask1, conversionTask2);
        var conversion = conversions[0];
        if (conversions.Any(x => !x.Succeeded) || conversions.Select(x => x.RequestId).Distinct().Count() != 1) throw new InvalidOperationException("Falló la conversión concurrente e idempotente.");
        trackedIds.Add(conversion.RequestId!.Value);

        dbContext.ChangeTracker.Clear();
        var convertedRequest = await dbContext.ProductRequests.AsNoTracking().SingleAsync(x => x.Id == first.RequestId.Value);
        convertedCustomerId = convertedRequest.ConvertedCustomerId;
        if (convertedCustomerId is null) throw new InvalidOperationException("La conversión no vinculó el cliente.");
        trackedIds.Add(convertedCustomerId.Value);

        var company = await customerService.CreateCustomerAsync(new("Company", "Other", $"Empresa prueba {suffix}", $"7111{phoneSuffix}", $"empresa-{suffix}@example.test", null, true, userId));
        if (!company.Succeeded) throw new InvalidOperationException(string.Join(" ", company.Errors));
        trackedIds.Add(company.Id!.Value);
        var contact = await customerService.AddContactAsync(company.Id.Value, new("Receptor prueba", $"7222{phoneSuffix}", null, "Recepción"), userId);
        var payer = await customerService.AddPaymentResponsibleAsync(company.Id.Value, new("CentralCompany", "Caja central", null, "Caja central", $"7333{phoneSuffix}", null), userId);
        if (!contact.Succeeded || !payer.Succeeded) throw new InvalidOperationException("No se pudieron crear contacto o pagador.");
        trackedIds.Add(contact.Id!.Value); trackedIds.Add(payer.Id!.Value);
        var point = await customerService.AddDeliveryPointAsync(company.Id.Value, new("Branch", "Sucursal prueba", "Av. Prueba 123", "Puerta principal", "https://maps.example/branch", null, contact.Id, payer.Id), userId);
        if (!point.Succeeded) throw new InvalidOperationException(string.Join(" ", point.Errors));
        trackedIds.Add(point.Id!.Value);

        var detail = await customerService.GetCustomerAsync(company.Id.Value);
        var historyCount = await dbContext.RequestStatusHistory.CountAsync(x => x.RequestId == first.RequestId.Value);
        var auditCount = await dbContext.AuditEntries.CountAsync(x => trackedIds.Select(id => id.ToString()).Contains(x.EntityId!));
        if (detail is null || detail.Contacts.Count != 1 || detail.PaymentResponsibles.Count != 1 || detail.DeliveryPoints.Count != 1 || historyCount < 3 || auditCount < 5)
            throw new InvalidOperationException("El recorrido no conservó todas las relaciones, historial o auditoría.");

        Console.WriteLine("Fase 3 verificada en Neon: envío idempotente, estados, conversión, empresa, contacto, sucursal, pagador y auditoría.");
    }
    finally
    {
        dbContext.ChangeTracker.Clear();
        var orders = await dbContext.Orders.Where(x => trackedIds.Contains(x.Id)).ToListAsync();
        dbContext.Orders.RemoveRange(orders);
        var requests = await dbContext.ProductRequests.Where(x => trackedIds.Contains(x.Id)).ToListAsync();
        dbContext.ProductRequests.RemoveRange(requests);
        await dbContext.SaveChangesAsync();
        var customers = await dbContext.Customers.Where(x => trackedIds.Contains(x.Id)).ToListAsync();
        dbContext.Customers.RemoveRange(customers);
        var idempotency = await dbContext.IdempotencyRecords.Where(x => x.Operation == "CreateProductRequest" && x.Key == key).ToListAsync();
        dbContext.IdempotencyRecords.RemoveRange(idempotency);
        var audit = await dbContext.AuditEntries.Where(x => x.EntityId != null && trackedIds.Select(id => id.ToString()).Contains(x.EntityId)).ToListAsync();
        dbContext.AuditEntries.RemoveRange(audit);
        await dbContext.SaveChangesAsync();
    }
    return;
}

if (args.Contains("--check-phase5", StringComparer.OrdinalIgnoreCase))
{
    await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var inventory=scope.ServiceProvider.GetRequiredService<IInventoryService>();var user=await db.Users.AsNoTracking().Select(x=>x.Id).FirstAsync();var now=DateTimeOffset.UtcNow;var product=TentacionSana.Domain.Catalog.Product.Create("Producto verificación fase 5",$"check-phase5-{Guid.NewGuid():N}","Unidad",now);var recipe=RecipeVersion.Create(product.Id,1,"Receta verificación fase 5",now.AddMinutes(-1));var cost=ProductCostVersion.Create(product.Id,3.25m,now.AddMinutes(-1));db.Products.Add(product);db.RecipeVersions.Add(recipe);db.ProductCostVersions.Add(cost);await db.SaveChangesAsync();
    try{var produced=await inventory.RegisterProductionAsync(new(product.Id,now,12,2),user);if(!produced.Succeeded)throw new InvalidOperationException(string.Join(" ",produced.Errors));var output=await inventory.RegisterOutputAsync(new(product.Id,3,"Tasting","Verificación FIFO"),user);var stock=(await inventory.GetStockAsync()).Single(x=>x.ProductId==product.Id);if(!output.Succeeded||stock.Physical!=7)throw new InvalidOperationException("Producción o salida FIFO inconsistente.");var count=await inventory.RegisterCountAsync(new(product.Id,6,"Conteo de verificación"),user);if(!count.Succeeded)throw new InvalidOperationException(string.Join(" ",count.Errors));Console.WriteLine("Fase 5 verificada en Neon: lote, costo estimado, movimiento, FIFO, salida, conteo y ajuste.");}
    finally{db.ChangeTracker.Clear();var allocations=await db.InventoryMovementAllocations.Where(x=>db.InventoryMovements.Any(m=>m.Id==x.MovementId&&m.ProductId==product.Id)).ToListAsync();db.InventoryMovementAllocations.RemoveRange(allocations);db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(x=>x.ProductId==product.Id).ToListAsync());db.StockCounts.RemoveRange(await db.StockCounts.Where(x=>x.ProductId==product.Id).ToListAsync());db.ProductionBatches.RemoveRange(await db.ProductionBatches.Where(x=>x.ProductId==product.Id).ToListAsync());db.ProductStockBalances.RemoveRange(await db.ProductStockBalances.Where(x=>x.ProductId==product.Id).ToListAsync());db.RecipeVersions.RemoveRange(await db.RecipeVersions.Where(x=>x.ProductId==product.Id).ToListAsync());db.ProductCostVersions.RemoveRange(await db.ProductCostVersions.Where(x=>x.ProductId==product.Id).ToListAsync());await db.SaveChangesAsync();db.Products.Remove(await db.Products.SingleAsync(x=>x.Id==product.Id));await db.SaveChangesAsync();}return;
}

if (args.Contains("--check-phase6", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var deliveries = scope.ServiceProvider.GetRequiredService<IDeliveryService>();
    var userId = await db.Users.AsNoTracking().Where(x => x.IsEnabled).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    var now = DateTimeOffset.UtcNow;
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var product = Product.Create($"Producto fase 6 {suffix}", $"check-phase6-{suffix}", "Unidad", now);
    var recipe = RecipeVersion.Create(product.Id, 1, "Receta fase 6", now.AddMinutes(-5));
    var cost = ProductCostVersion.Create(product.Id, 2.50m, now.AddMinutes(-5));
    var price = new ProductPrice(Guid.NewGuid(), product.Id, 10m, now.AddMinutes(-5));
    var customer = Customer.Create($"Cliente fase 6 {suffix}", $"+5917000{Random.Shared.Next(1000, 9999)}", null, now);
    var contact = customer.AddContact("Recepción", "+59171111111", null, "Receptor", now);
    var payer = customer.AddPaymentResponsible(PaymentResponsibleKind.Customer, customer.Name, customer.Phone!, null, now);
    var point = customer.AddDeliveryPoint(DeliveryPointKind.Branch, "Punto de prueba", "Dirección fase 6", null, "https://maps.example/check", contact.Id, payer.Id, now);
    var balance = ProductStockBalance.Create(product.Id); balance.Produce(4);
    var batch = ProductionBatch.Create($"F6-{suffix}", product.Id, recipe.Id, now, 4, 0, cost.EstimatedUnitCost, userId);
    var order = Order.CreateDraft(customer.Id, null, userId, now);
    var line = order.AddLine(product.Id, product.Name, 2, 10m, 10m, null, userId, now);
    order.Configure(customer.Id, point.Id, contact.Id, payer.Id, now.AddDays(2), "Recorrido fase 6", userId, now);
    order.Confirm(OrderSnapshot.Create(order.Id, customer.Name, point.Label, point.Address, contact.Name, contact.Phone, payer.Name, now), userId, now);
    var reserved = balance.Reserve(2); order.Reservations.Single().Allocate(reserved);
    order.AdvanceTo(OrderStatus.InPreparation, "Verificación fase 6", userId, now);
    order.AdvanceTo(OrderStatus.Ready, "Verificación fase 6", userId, now);
    db.AddRange(product, recipe, cost, price, customer, balance, batch, order);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    Guid? deliveryId = null;
    var completeKey = $"phase6-delivery-{suffix}";
    try
    {
        var board = await deliveries.GetBoardAsync(DateOnly.FromDateTime(now.ToLocalTime().DateTime));
        if (board.Items.All(x => x.OrderId != order.Id)) throw new InvalidOperationException("El tablero ocultó un pedido pendiente con fecha prometida futura.");
        var scheduled = await deliveries.ScheduleAsync(new(order.Id, null, now.AddMinutes(10), [new(line.Id, 2)]), userId);
        if (!scheduled.Succeeded || scheduled.Id is null) throw new InvalidOperationException(string.Join(" ", scheduled.Errors));
        deliveryId = scheduled.Id;
        db.ChangeTracker.Clear();
        var detail = await deliveries.GetAsync(deliveryId.Value) ?? throw new InvalidOperationException("No se recuperó la entrega.");
        var started = await deliveries.StartAsync(detail.Id, detail.Version, userId);
        if (!started.Succeeded) throw new InvalidOperationException(string.Join(" ", started.Errors));
        db.ChangeTracker.Clear();
        detail = await deliveries.GetAsync(deliveryId.Value) ?? throw new InvalidOperationException();
        var completeCommand = new CompleteDeliveryCommand(detail.Id, detail.Version, completeKey, "Receptor fase 6", detail.Lines.Select(x => new CompleteLine(x.Id, x.PendingQuantity)).ToList(), 0, "Cash", null, false);
        await using var concurrentScope1 = app.Services.CreateAsyncScope();
        await using var concurrentScope2 = app.Services.CreateAsyncScope();
        var completions = await Task.WhenAll(concurrentScope1.ServiceProvider.GetRequiredService<IDeliveryService>().CompleteAsync(completeCommand, userId), concurrentScope2.ServiceProvider.GetRequiredService<IDeliveryService>().CompleteAsync(completeCommand, userId));
        if (completions.Any(x => !x.Succeeded)) throw new InvalidOperationException("La confirmación concurrente no fue idempotente.");
        db.ChangeTracker.Clear();
        var finalReceivable = await db.Receivables.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        var saleCount = await db.Sales.CountAsync(x => x.DeliveryId == deliveryId);
        var paymentCount = await db.Payments.CountAsync(x => x.OrderId == order.Id);
        var settlementCount = await db.SettlementObligations.CountAsync(x => db.Payments.Any(p => p.Id == x.PaymentId && p.OrderId == order.Id));
        var movementCount = await db.InventoryMovements.CountAsync(x => x.ProductId == product.Id && x.Kind == InventoryMovementKind.DeliveredSale);
        var finalBalance = await db.ProductStockBalances.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        if (saleCount != 1 || paymentCount != 0 || settlementCount != 0 || movementCount != 1 || finalReceivable.Balance != 20 || finalReceivable.CreditAmount != 0 || finalBalance.PhysicalQuantity != 2 || finalBalance.ReservedQuantity != 0)
            throw new InvalidOperationException("El recorrido no conservó venta, inventario o cuenta por cobrar.");
        Console.WriteLine("Fase 6 verificada en Neon: pedido futuro visible, programación, ruta, confirmación concurrente idempotente, FIFO, venta, inventario y cuenta por cobrar.");
    }
    finally
    {
        db.ChangeTracker.Clear();
        var paymentIds = await db.Payments.Where(x => x.OrderId == order.Id).Select(x => x.Id).ToListAsync();
        db.SettlementObligations.RemoveRange(await db.SettlementObligations.Where(x => paymentIds.Contains(x.PaymentId)).ToListAsync());
        db.Payments.RemoveRange(await db.Payments.Where(x => x.OrderId == order.Id).ToListAsync());
        db.Receivables.RemoveRange(await db.Receivables.Where(x => x.OrderId == order.Id).ToListAsync());
        db.Sales.RemoveRange(await db.Sales.Where(x => x.OrderId == order.Id).ToListAsync());
        if (deliveryId is not null) db.Deliveries.RemoveRange(await db.Deliveries.Where(x => x.Id == deliveryId).ToListAsync());
        db.IdempotencyRecords.RemoveRange(await db.IdempotencyRecords.Where(x => x.Operation == "CompleteDelivery" && x.Key == completeKey).ToListAsync());
        await db.SaveChangesAsync();
        db.Orders.RemoveRange(await db.Orders.Where(x => x.Id == order.Id).ToListAsync()); await db.SaveChangesAsync();
        db.InventoryMovementAllocations.RemoveRange(await db.InventoryMovementAllocations.Where(x => db.InventoryMovements.Any(m => m.Id == x.MovementId && m.ProductId == product.Id)).ToListAsync());
        db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(x => x.ProductId == product.Id).ToListAsync());
        db.ProductionBatches.RemoveRange(await db.ProductionBatches.Where(x => x.ProductId == product.Id).ToListAsync());
        db.ProductStockBalances.RemoveRange(await db.ProductStockBalances.Where(x => x.ProductId == product.Id).ToListAsync());
        db.RecipeVersions.RemoveRange(await db.RecipeVersions.Where(x => x.ProductId == product.Id).ToListAsync());
        db.ProductCostVersions.RemoveRange(await db.ProductCostVersions.Where(x => x.ProductId == product.Id).ToListAsync());
        db.ProductPrices.RemoveRange(await db.ProductPrices.Where(x => x.ProductId == product.Id).ToListAsync());
        await db.SaveChangesAsync();
        db.Customers.RemoveRange(await db.Customers.Where(x => x.Id == customer.Id).ToListAsync());
        db.Products.RemoveRange(await db.Products.Where(x => x.Id == product.Id).ToListAsync());
        db.AuditEntries.RemoveRange(await db.AuditEntries.Where(x => x.EntityId == order.Id.ToString() || x.EntityId == deliveryId.ToString()).ToListAsync());
        await db.SaveChangesAsync();
    }
    return;
}

if (args.Contains("--check-phase4", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var orders = scope.ServiceProvider.GetRequiredService<IOrderManagementService>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var customer = await db.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.CreatedAtUtc).FirstAsync();
    var product = await orders.ProductOptionsAsync();
    var user = await db.Users.AsNoTracking().Select(x => x.Id).FirstAsync();
    if (product.Count == 0 || product[0].CurrentPrice <= 0) throw new InvalidOperationException("Se requiere un producto activo con precio vigente.");
    Guid? orderId = null;
    try
    {
        var created = await orders.CreateDraftAsync(customer.Id, user); orderId = created.Id;
        if (!created.Succeeded || orderId is null) throw new InvalidOperationException(string.Join(" ", created.Errors));
        var detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException("No se recuperó el borrador.");
        var configured = await orders.ConfigureDraftAsync(orderId.Value, detail.Version, new(customer.Id, null, null, null, DateTimeOffset.UtcNow.AddDays(1), "Verificación automatizada fase 4"), user);
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        var line = await orders.AddLineAsync(orderId.Value, detail.Version, new(product[0].Id, 2, 0, 0, product[0].CurrentPrice, null, null, null), user);
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        var stale = await orders.ConfigureDraftAsync(orderId.Value, detail.Version - 1, new(customer.Id, null, null, null, detail.PromisedAtUtc, detail.Notes), user);
        var confirmed = await orders.ConfirmAsync(orderId.Value, detail.Version, user);
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        await using var competingScope1 = app.Services.CreateAsyncScope();
        await using var competingScope2 = app.Services.CreateAsyncScope();
        var competing1 = competingScope1.ServiceProvider.GetRequiredService<IOrderManagementService>();
        var competing2 = competingScope2.ServiceProvider.GetRequiredService<IOrderManagementService>();
        var concurrentResults = await Task.WhenAll(
            competing1.ReviseConfigurationAsync(orderId.Value, detail.Version, new(customer.Id, null, null, null, detail.PromisedAtUtc, "Cambio concurrente A"), "Prueba concurrente A", user),
            competing2.ReviseConfigurationAsync(orderId.Value, detail.Version, new(customer.Id, null, null, null, detail.PromisedAtUtc, "Cambio concurrente B"), "Prueba concurrente B", user));
        if (concurrentResults.Count(x => x.Succeeded) != 1) throw new InvalidOperationException("La concurrencia real no produjo un único ganador.");
        db.ChangeTracker.Clear();
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        var revised = await orders.ReviseLineAsync(orderId.Value, detail.Lines[0].Id, detail.Version, new(1, 0, 0, detail.Lines[0].SoldUnitPrice, null, null, null, null, null, "Reducción verificada"), user);
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        if (!revised.Succeeded || detail.ShortageQuantity != 1) throw new InvalidOperationException("La reducción no ajustó la reserva.");
        var cancelled = await orders.CancelAsync(orderId.Value, detail.Version, "Cierre de verificación automatizada", user);
        detail = await orders.GetAsync(orderId.Value) ?? throw new InvalidOperationException();
        if (!configured.Succeeded || !line.Succeeded || stale.Succeeded || !confirmed.Succeeded || !cancelled.Succeeded || detail.Status != "Cancelled" || detail.History.Count < 5 || detail.ShortageQuantity != 0)
            throw new InvalidOperationException("El recorrido de pedidos no conservó concurrencia, historial o liberación de reservas.");
        Console.WriteLine($"Fase 4 verificada en Neon: pedido #{detail.Number}, precio congelado, dos escrituras simultáneas con un ganador, ajuste de reserva, historial y cancelación.");
    }
    finally
    {
        if (orderId is not null)
        {
            db.ChangeTracker.Clear();
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId);
            if (order is not null) db.Orders.Remove(order);
            var audit = await db.AuditEntries.Where(x => x.EntityType == "Order" && x.EntityId == orderId.ToString()).ToListAsync();
            db.AuditEntries.RemoveRange(audit); await db.SaveChangesAsync();
        }
    }
    return;
}

if (args.Contains("--check-admin", StringComparer.OrdinalIgnoreCase))
{
    var userName = builder.Configuration["BootstrapAdmin:UserName"];
    if (string.IsNullOrWhiteSpace(userName))
    {
        throw new InvalidOperationException("Falta BootstrapAdmin:UserName para identificar la cuenta.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
    var user = await userManager.FindByNameAsync(userName)
        ?? throw new InvalidOperationException("No se encontró la cuenta administradora.");
    var roles = await userManager.GetRolesAsync(user);
    Console.WriteLine($"Cuenta habilitada: {user.IsEnabled}");
    Console.WriteLine($"Cambio obligatorio pendiente: {user.MustChangePassword}");
    Console.WriteLine($"Rol Administrador: {roles.Contains(TentacionSana.Application.Security.AppRoles.Administrator)}");
    return;
}

if (args.Contains("--check-catalog", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var products = await dbContext.Products.AsNoTracking()
        .OrderBy(product => product.Name)
        .Select(product => new
        {
            product.Name,
            product.Slug,
            product.IsActive,
            IsPublished = dbContext.ProductPublications
                .Where(publication => publication.ProductId == product.Id)
                .Select(publication => publication.IsPublished)
                .FirstOrDefault(),
            PriceCount = dbContext.ProductPrices.Count(price => price.ProductId == product.Id),
            Images = dbContext.ProductImages
                .Where(image => image.ProductId == product.Id)
                .Select(image => image.SecureUrl)
                .ToList()
        })
        .ToListAsync();

    Console.WriteLine($"Productos registrados: {products.Count}");
    using var httpClient = new HttpClient();
    foreach (var product in products)
    {
        Console.WriteLine($"Producto: {product.Name}");
        Console.WriteLine($"Ruta pública: /productos/{product.Slug}");
        Console.WriteLine($"Activo: {product.IsActive}; publicado: {product.IsPublished}; precios: {product.PriceCount}; imágenes: {product.Images.Count}");
        foreach (var imageUrl in product.Images)
        {
            using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
            Console.WriteLine($"Imagen Cloudinary accesible: {response.IsSuccessStatusCode}");
        }
    }
    return;
}

if (args.Contains("--seed-admin", StringComparer.OrdinalIgnoreCase))
{
    var userName = builder.Configuration["BootstrapAdmin:UserName"];
    var displayName = builder.Configuration["BootstrapAdmin:DisplayName"];
    var temporaryPassword = builder.Configuration["BootstrapAdmin:TemporaryPassword"];

    if (string.IsNullOrWhiteSpace(userName)
        || string.IsNullOrWhiteSpace(displayName)
        || string.IsNullOrWhiteSpace(temporaryPassword))
    {
        throw new InvalidOperationException(
            "Configure BootstrapAdmin__UserName, BootstrapAdmin__DisplayName y BootstrapAdmin__TemporaryPassword antes de usar --seed-admin.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<InitialAdministratorSeeder>();
    await seeder.SeedAsync(userName, displayName, temporaryPassword);
    return;
}

// Configure the HTTP request pipeline.
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapSecurityEndpoints();
app.MapGet("/media/delivery-points/{pointId:guid}", GetDeliveryPointImageAsync)
    .RequireAuthorization(AppPolicies.InternalAccess);
app.MapGet("/media/delivery-evidence/{evidenceId:guid}", GetDeliveryEvidenceAsync)
    .RequireAuthorization(AppPolicies.InternalAccess);
app.MapGet("/media/payment-evidence/{evidenceId:guid}", GetPaymentEvidenceAsync)
    .RequireAuthorization(AppPolicies.InternalAccess);
app.MapGet("/media/cash-evidence/{movementId:guid}", GetCashEvidenceAsync)
    .RequireAuthorization(AppPolicies.InternalAccess);
app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

app.Run();

static async Task<IResult> GetDeliveryPointImageAsync(Guid pointId, IDeliveryPointImageService images, CancellationToken cancellationToken)
{
    var image = await images.GetContentAsync(pointId, cancellationToken);
    return image is null ? Results.NotFound() : Results.File(image.Content, image.ContentType, enableRangeProcessing: true);
}

static async Task<IResult> GetDeliveryEvidenceAsync(Guid evidenceId, ClaimsPrincipal user, IDeliveryEvidenceService evidenceService, CancellationToken cancellationToken)
{
    var userId = CurrentUserId(user);
    if (userId is null) return Results.Unauthorized();
    var evidence = await evidenceService.GetContentAsync(evidenceId, userId.Value, cancellationToken);
    return EvidenceFile(evidence.Succeeded, evidence.Content, evidence.ContentType);
}

static async Task<IResult> GetPaymentEvidenceAsync(Guid evidenceId, ClaimsPrincipal user, IDeliveryService deliveryService, CancellationToken cancellationToken)
{
    var userId = CurrentUserId(user);
    if (userId is null) return Results.Unauthorized();
    var evidence = await deliveryService.GetPaymentEvidenceContentAsync(evidenceId, userId.Value, cancellationToken);
    return EvidenceFile(evidence.Succeeded, evidence.Content, evidence.ContentType);
}

static async Task<IResult> GetCashEvidenceAsync(Guid movementId, ClaimsPrincipal user, ICashLedgerService cashLedger, CancellationToken cancellationToken)
{
    var userId = CurrentUserId(user);
    if (userId is null) return Results.Unauthorized();
    var evidence = await cashLedger.GetEvidenceContentAsync(movementId, userId.Value, cancellationToken);
    return EvidenceFile(evidence.Succeeded, evidence.Content, evidence.ContentType);
}

static Guid? CurrentUserId(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

static IResult EvidenceFile(bool succeeded, byte[]? content, string? contentType) =>
    succeeded && content is not null
        ? Results.File(content, contentType ?? "application/octet-stream", enableRangeProcessing: true)
        : Results.NotFound();
