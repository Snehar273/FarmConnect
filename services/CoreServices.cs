using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using FarmConnect.Data;
using FarmConnect.Models;

using Microsoft.EntityFrameworkCore;
namespace FarmConnect.Services;

public class OrderService(ApplicationDbContext db) : IOrderService
{
    IQueryable<Order> Q() => db.Orders.Include(o => o.Items).Include(o => o.Buyer).Include(o => o.Farmer).Include(o => o.Logistics).ThenInclude(b => b.Partner);

    public Task<List<Order>> GetBuyerOrdersAsync(string id) => Q().Where(o => o.BuyerId == id).OrderByDescending(o => o.CreatedAt).ToListAsync();
    public Task<List<Order>> GetFarmerOrdersAsync(string id) => Q().Where(o => o.FarmerId == id).OrderByDescending(o => o.CreatedAt).ToListAsync();
    public Task<List<Order>> GetAllOrdersAsync() => Q().OrderByDescending(o => o.CreatedAt).ToListAsync();

    public async Task<OrderResult> PlaceOrderAsync(string buyerId, int listingId, decimal quantity)
    {
        OrderResult Fail(string m) => new(false, m, null);
        var l = await db.Listings.AsNoTracking().Include(x => x.Produce).FirstOrDefaultAsync(x => x.Id == listingId);
        if (l == null) return Fail("Listing not found.");
        if (l.Status != ListingStatus.Active) return Fail("This listing is no longer available.");
        if (quantity <= 0) return Fail("Enter a quantity greater than zero.");
        if (quantity < l.MinOrderQty) return Fail($"Minimum order is {l.MinOrderQty:0.##} {l.Unit}.");
        if (quantity > l.QuantityAvailable) return Fail($"Only {l.QuantityAvailable:0.##} {l.Unit} available.");

        var price = l.PricePerUnit;
        await using var tx = await db.Database.BeginTransactionAsync();
        // atomic, concurrency-safe stock reservation
        var rows = await db.Listings
            .Where(x => x.Id == listingId && x.Status == ListingStatus.Active && x.QuantityAvailable >= quantity && x.MinOrderQty <= quantity && x.PricePerUnit == price)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityAvailable, x => x.QuantityAvailable - quantity));
        if (rows == 0) { await tx.RollbackAsync(); return Fail("Stock or price just changed. Please review and try again."); }
        await db.Listings.Where(x => x.Id == listingId && x.QuantityAvailable == 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ListingStatus.SoldOut));

        var order = new Order
        {
            OrderNo = "FC-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
            BuyerId = buyerId, FarmerId = l.FarmerId, TotalAmount = quantity * price,
            Items = { new OrderItem { ListingId = l.Id, ProduceName = l.Produce.Name + (string.IsNullOrEmpty(l.Variety) ? "" : " · " + l.Variety), Unit = l.Unit, Quantity = quantity, AgreedUnitPrice = price } }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // TRANSACTIONAL OUTBOX: the event is stored in the SAME transaction as the order
        db.OutboxEvents.Add(new OutboxEvent
        {
            EventId = Guid.NewGuid(), EventType = "OrderPlaced",
            Payload = JsonSerializer.Serialize(new OrderPlacedEvent(order.Id, order.OrderNo, l.Id, quantity, l.Unit))
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(true, null, order);
    }

    public async Task<StatusResult> UpdateStatusAsync(int id, string userId, bool farmer, OrderStatus status)
    {
        var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && (farmer ? x.FarmerId == userId : x.BuyerId == userId));
        if (o == null) return new(false, true, null);
        var ok = farmer
            ? (o.Status == OrderStatus.Placed && status is OrderStatus.Accepted or OrderStatus.Rejected) || (o.Status == OrderStatus.Accepted && status is OrderStatus.Completed or OrderStatus.Cancelled)
            : o.Status == OrderStatus.Placed && status == OrderStatus.Cancelled;
        if (!ok) return new(false, false, "That status change is not allowed.");

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var it in o.Items)
        {
            var lid = it.ListingId; var qty = it.Quantity;
            if (status is OrderStatus.Rejected or OrderStatus.Cancelled)
            {
                await db.Listings.Where(x => x.Id == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityAvailable, x => x.QuantityAvailable + qty));
                await db.Listings.Where(x => x.Id == lid && x.Status == ListingStatus.SoldOut && x.QuantityAvailable > 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ListingStatus.Active));
            }
            else if (status == OrderStatus.Completed)
                await db.Listings.Where(x => x.Id == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantitySold, x => x.QuantitySold + qty));
        }
        if (status is OrderStatus.Rejected or OrderStatus.Cancelled)   // cancellation rule: booking is cancelled too
        {
            var oid = o.Id; var note = $"Booking cancelled: order was {status.ToString().ToLower()}.";
            await db.LogisticsBookings.Where(b => b.OrderId == oid && b.BookingStatus != BookingStatus.Delivered && b.BookingStatus != BookingStatus.Cancelled)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.BookingStatus, BookingStatus.Cancelled).SetProperty(b => b.UpdatedAt, DateTime.Now).SetProperty(b => b.Note, note));
        }
        o.Status = status;
        if (status == OrderStatus.Completed) o.CompletedAt = DateTime.Now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(true, false, $"Order {o.OrderNo} marked {status}.");
    }
}

public class FarmerService(ApplicationDbContext db, IOrderService orders) : IFarmerService
{
    public Task<ApplicationUser> GetFarmerByIdAsync(string id) => db.Users.FirstOrDefaultAsync(u => u.Id == id);
    public Task<FarmerProfile> GetFarmerProfileAsync(string id) => db.FarmerProfiles.FirstOrDefaultAsync(p => p.UserId == id);

    public async Task<FarmerProfile> UpdateFarmerProfileAsync(string id, string farmName, string location)
    {
        var p = await GetFarmerProfileAsync(id);
        if (p == null) return null;
        p.FarmName = farmName; p.Location = location;
        await db.SaveChangesAsync();
        return p;
    }

    public Task<List<Listing>> GetFarmerListingsAsync(string id) =>
        db.Listings.Include(l => l.Produce).Where(l => l.FarmerId == id).OrderByDescending(l => l.CreatedAt).ToListAsync();

    public Task<List<Order>> GetFarmerOrdersAsync(string id) => orders.GetFarmerOrdersAsync(id);

    public async Task<FarmerDashboard> GetDashboardAsync(string id)
    {
        var sales = await db.OrderItems.Where(i => i.Order.FarmerId == id && i.Order.Status == OrderStatus.Completed).SumAsync(i => (decimal?)(i.Quantity * i.AgreedUnitPrice)) ?? 0m;
        return new(
            await db.Listings.CountAsync(l => l.FarmerId == id && l.Status == ListingStatus.Active),
            await db.Orders.CountAsync(o => o.FarmerId == id && o.Status == OrderStatus.Placed),
            await db.Orders.CountAsync(o => o.FarmerId == id && o.Status == OrderStatus.Completed), sales);
    }

    public async Task<EarningsVm> GetFarmerEarningsAsync(string uid, string period, DateTime? fromDate, DateTime? toDate)
    {
        var today = DateTime.Today; DateTime s, e;
        switch (period)
        {
            case "daily": s = e = today; break;
            case "weekly": s = today.AddDays(-6); e = today; break;
            case "custom": s = (fromDate ?? today.AddDays(-30)).Date; e = (toDate ?? today).Date; break;
            default: period = "monthly"; s = new DateTime(today.Year, today.Month, 1); e = today; break;
        }
        var end = e.AddDays(1);
        var list = await db.Orders.Include(o => o.Items).Include(o => o.Buyer)
            .Where(o => o.FarmerId == uid && o.Status == OrderStatus.Completed && o.CompletedAt >= s && o.CompletedAt < end)
            .OrderByDescending(o => o.CompletedAt).ToListAsync();
        var exp = await db.SellingExpenses.Where(x => x.FarmerId == uid && x.ExpenseDate >= s && x.ExpenseDate < end).OrderByDescending(x => x.ExpenseDate).ToListAsync();
        var vm = new EarningsVm
        {
            Period = period, From = s, To = e, Orders = list, Expenses = exp,
            Gross = list.Sum(o => o.Items.Sum(i => i.Quantity * i.AgreedUnitPrice)),
            Qty = list.Sum(o => o.Items.Sum(i => i.Quantity)), Costs = exp.Sum(x => x.Amount)
        };
        vm.Net = vm.Gross - vm.Costs;
        return vm;
    }

    public async Task<(bool Ok, string Message)> AddSellingExpenseAsync(string uid, int orderId, string description, decimal amount)
    {
        var ok = await db.Orders.AnyAsync(o => o.Id == orderId && o.FarmerId == uid && o.Status == OrderStatus.Completed);
        if (!ok || amount <= 0 || string.IsNullOrWhiteSpace(description) || description.Length > 200) return (false, "Invalid expense entry.");
        db.SellingExpenses.Add(new SellingExpense { OrderId = orderId, FarmerId = uid, Description = description.Trim(), Amount = amount });
        await db.SaveChangesAsync();
        return (true, "Selling cost recorded.");
    }
}

public class ProduceService(ApplicationDbContext db, IPriceCacheService prices) : IProduceService
{
    public Task<List<Produce>> GetAllProduceAsync() => db.ProduceItems.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
    public Task<Produce> GetProduceByIdAsync(int id) => db.ProduceItems.AsNoTracking().FirstOrDefaultAsync(p => p.ProduceId == id);
    public Task<Listing> GetListingByIdAsync(int id) => db.Listings.Include(l => l.Produce).Include(l => l.Farmer).FirstOrDefaultAsync(x => x.Id == id);
    public Task<PriceResult> GetPriceHistoryAsync(int produceId, string market, DateTime? f, DateTime? t) => prices.GetPricesAsync(produceId, market, f, t);

    public Task<List<Listing>> SearchListingsAsync(string q, string grade, string location, decimal? minPrice, decimal? maxPrice, string sort, string farmerId = null)
    {
        IQueryable<Listing> qry = db.Listings.Include(l => l.Produce).Include(l => l.Farmer).Where(l => l.Status == ListingStatus.Active && l.QuantityAvailable > 0);
        if (!string.IsNullOrEmpty(farmerId)) qry = qry.Where(l => l.FarmerId == farmerId);
        if (!string.IsNullOrWhiteSpace(q)) qry = qry.Where(l => l.Produce.Name.Contains(q) || l.Variety.Contains(q));
        if (!string.IsNullOrWhiteSpace(grade)) qry = qry.Where(l => l.Grade == grade);
        if (!string.IsNullOrWhiteSpace(location)) qry = qry.Where(l => l.Location.Contains(location));
        if (minPrice.HasValue) qry = qry.Where(l => l.PricePerUnit >= minPrice.Value);
        if (maxPrice.HasValue) qry = qry.Where(l => l.PricePerUnit <= maxPrice.Value);
        qry = sort switch { "low" => qry.OrderBy(l => l.PricePerUnit), "high" => qry.OrderByDescending(l => l.PricePerUnit), _ => qry.OrderByDescending(l => l.CreatedAt) };
        return qry.ToListAsync();
    }

    public async Task<(bool Ok, string Error)> CheckAvailabilityAsync(int id, decimal qty)
    {
        var l = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (l == null || l.Status != ListingStatus.Active) return (false, "Listing is not available.");
        if (qty < l.MinOrderQty) return (false, $"Minimum order is {l.MinOrderQty:0.##} {l.Unit}.");
        if (qty > l.QuantityAvailable) return (false, $"Only {l.QuantityAvailable:0.##} {l.Unit} available.");
        return (true, null);
    }
}