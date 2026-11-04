namespace FarmConnect.Controllers;

[Authorize]
public class OrdersController(ApplicationDbContext db, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index()
    {
        var uid = um.GetUserId(User);
        IQueryable<Order> q = db.Orders.Include(o => o.Items).Include(o => o.Buyer).Include(o => o.Farmer);
        if (User.IsInRole("Farmer")) q = q.Where(o => o.FarmerId == uid);
        else if (User.IsInRole("Buyer")) q = q.Where(o => o.BuyerId == uid); // buyers only see their own orders
        return View(await q.OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Place(int listingId, decimal quantity)
    {
        IActionResult Fail(string msg) { TempData["Error"] = msg; return RedirectToAction("Details", "Market", new { id = listingId }); }

        var l = await db.Listings.AsNoTracking().Include(x => x.Produce).FirstOrDefaultAsync(x => x.Id == listingId);
        if (l == null) return NotFound();
        if (l.Status != ListingStatus.Active) return Fail("This listing is no longer available.");
        if (quantity <= 0) return Fail("Enter a quantity greater than zero.");
        if (quantity < l.MinOrderQty) return Fail($"Minimum order is {l.MinOrderQty:0.##} {l.Unit}.");
        if (quantity > l.QuantityAvailable) return Fail($"Only {l.QuantityAvailable:0.##} {l.Unit} available.");

        var price = l.PricePerUnit;
        await using var tx = await db.Database.BeginTransactionAsync();
        // Atomic, concurrency-safe stock reservation: succeeds only if conditions still hold at write time.
        var rows = await db.Listings
            .Where(x => x.Id == listingId && x.Status == ListingStatus.Active && x.QuantityAvailable >= quantity && x.MinOrderQty <= quantity && x.PricePerUnit == price)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityAvailable, x => x.QuantityAvailable - quantity));
        if (rows == 0) { await tx.RollbackAsync(); return Fail("Stock or price just changed. Please review and try again."); }
        await db.Listings.Where(x => x.Id == listingId && x.QuantityAvailable == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ListingStatus.SoldOut));

        var order = new Order
        {
            OrderNo = "FC-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
            BuyerId = um.GetUserId(User), FarmerId = l.FarmerId, TotalAmount = quantity * price,
            Items = { new OrderItem { ListingId = l.Id, ProduceName = l.Produce.Name + (string.IsNullOrEmpty(l.Variety) ? "" : " · " + l.Variety), Unit = l.Unit, Quantity = quantity, AgreedUnitPrice = price } }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Ok"] = $"Order {order.OrderNo} placed successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Farmer,Buyer")]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var uid = um.GetUserId(User);
        var farmer = User.IsInRole("Farmer");
        var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && (farmer ? x.FarmerId == uid : x.BuyerId == uid));
        if (o == null) return NotFound();
        var ok = farmer
            ? (o.Status == OrderStatus.Placed && status is OrderStatus.Accepted or OrderStatus.Rejected) || (o.Status == OrderStatus.Accepted && status is OrderStatus.Completed or OrderStatus.Cancelled)
            : o.Status == OrderStatus.Placed && status == OrderStatus.Cancelled;
        if (!ok) { TempData["Error"] = "That status change is not allowed."; return RedirectToAction(nameof(Index)); }

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var it in o.Items)
        {
            var lid = it.ListingId; var qty = it.Quantity;
            if (status is OrderStatus.Rejected or OrderStatus.Cancelled)   // release reserved stock
            {
                await db.Listings.Where(x => x.Id == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityAvailable, x => x.QuantityAvailable + qty));
                await db.Listings.Where(x => x.Id == lid && x.Status == ListingStatus.SoldOut && x.QuantityAvailable > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ListingStatus.Active));
            }
            else if (status == OrderStatus.Completed)
                await db.Listings.Where(x => x.Id == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantitySold, x => x.QuantitySold + qty));
        }
        o.Status = status;
        if (status == OrderStatus.Completed) o.CompletedAt = DateTime.Now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Ok"] = $"Order {o.OrderNo} marked {status}.";
        return RedirectToAction(nameof(Index));
    }
}
