using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Customers;
using TentacionSana.Infrastructure.Persistence;
namespace TentacionSana.Infrastructure.Customers;
public sealed class OperationsQueryService(ApplicationDbContext db):IOperationsQueryService
{
 public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken=default)=>await db.Customers.AsNoTracking().OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new CustomerSummary(x.Id,x.Kind.ToString(),x.Name,x.Phone??"",x.Email,x.Contacts.Count,x.DeliveryPoints.Count,x.PaymentResponsibleParties.Count,x.CreatedAtUtc)).ToListAsync(cancellationToken);
 public async Task<IReadOnlyList<DraftOrderSummary>> GetDraftOrdersAsync(CancellationToken cancellationToken=default)=>await db.Orders.AsNoTracking().Where(x=>x.ArchivedAtUtc==null).OrderByDescending(x=>x.CreatedAtUtc).SelectMany(x=>x.Lines.Select(l=>new DraftOrderSummary(x.Id,db.Customers.Where(c=>c.Id==x.CustomerId).Select(c=>c.Name).First(),l.ProductName,l.Quantity,l.SoldUnitPrice,x.CreatedAtUtc,x.SourceRequestId))).ToListAsync(cancellationToken);
}
