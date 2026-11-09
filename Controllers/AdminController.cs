using System.Text.Json;
using FarmConnect.Services;

namespace FarmConnect.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> um) : Controller
{
    static object P(IEnumerable<(string, int)> x)
    {
        var l = x.ToList();
        return new { labels = l.Select(i => i.Item1).ToList(), data = l.Select(i => i.Item2).ToList() };
    }

    async Task<object> Snap()
    {
        long h = PriceCacheService.Hits, s = PriceCacheService.SqlReads;
        var recent = await db.Orders.AsNoTracking().Include(o => o.Buyer).Include(o => o.Farmer).OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync();
        return new
        {
            users = await um.Users.CountAsync(),
            farmers = await db.FarmerProfiles.CountAsync(),
            buyers = await db.BuyerProfiles.CountAsync(),
            active = await db.Listings.CountAsync(l => l.Status == ListingStatus.Active),
            orders = await db.Orders.CountAsync(),
            placed = await db.Orders.CountAsync(o => o.Status == OrderStatus.Placed),
            revenue = await db.Orders.Where(o => o.Status == OrderStatus.Completed).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m,
            bookings = await db.LogisticsBookings.CountAsync(),
            pendingLogi = await db.LogisticsBookings.CountAsync(b => b.BookingStatus == BookingStatus.PendingAssignment),
            outboxOpen = await db.OutboxEvents.CountAsync(e => e.Status == OutboxStatus.Pending || e.Status == OutboxStatus.Processing),
            outboxFailed = await db.OutboxEvents.CountAsync(e => e.Status == OutboxStatus.Failed),
            cacheRatio = h + s == 0 ? 0 : (int)(h * 100 / (h + s)),
            hits = h, sql = s,
            recent = recent.Select(o => new { orderNo = o.OrderNo, buyer = o.Buyer.FullName, farmer = o.Farmer.FullName, amount = o.TotalAmount, status = o.Status.ToString(), at = o.CreatedAt.ToString("dd MMM HH:mm") }).ToList()
        };
    }

    [HttpGet] public async Task<IActionResult> Stats() => Json(await Snap());

    public async Task<IActionResult> Index()
    {
        var rows = new List<UserRow>();
        foreach (var u in await um.Users.OrderBy(x => x.CreatedAt).ToListAsync())
            rows.Add(new UserRow { Id = u.Id, Name = u.FullName, Email = u.Email, Phone = u.PhoneNumber, Role = (await um.GetRolesAsync(u)).FirstOrDefault(), Locked = u.LockoutEnd > DateTimeOffset.UtcNow });

        var from = DateTime.Today.AddDays(-13);
        var days = Enumerable.Range(0, 14).Select(i => from.AddDays(i)).ToList();
        var recent = await db.Orders.AsNoTracking().Where(o => o.CreatedAt >= from || o.CompletedAt >= from)
            .Select(o => new { o.CreatedAt, o.CompletedAt, o.Status, o.TotalAmount }).ToListAsync();
        var placed = days.Select(d => recent.Count(o => o.CreatedAt.Date == d)).ToList();
        var revenue = days.Select(d => recent.Where(o => o.Status == OrderStatus.Completed && o.CompletedAt.HasValue && o.CompletedAt.Value.Date == d).Sum(o => o.TotalAmount)).ToList();
        var st = await db.Orders.GroupBy(o => o.Status).Select(g => new { k = g.Key, n = g.Count() }).ToListAsync();
        var ls = await db.Listings.GroupBy(l => l.Status).Select(g => new { k = g.Key, n = g.Count() }).ToListAsync();
        var top = await db.OrderItems.GroupBy(i => i.ProduceName).Select(g => new { k = g.Key, n = g.Count() }).OrderByDescending(x => x.n).Take(5).ToListAsync();
        var lg = await db.LogisticsBookings.GroupBy(b => b.BookingStatus).Select(g => new { k = g.Key, n = g.Count() }).ToListAsync();

        ViewBag.Dash = JsonSerializer.Serialize(new
        {
            trend = new { labels = days.Select(d => d.ToString("dd MMM")).ToList(), placed, revenue },
            orderStatus = P(st.Select(x => (x.k.ToString(), x.n))),
            listings = P(ls.Select(x => (x.k.ToString(), x.n))),
            top = P(top.Select(x => (x.k, x.n))),
            logistics = P(lg.Select(x => (x.k.ToString(), x.n))),
            roles = P(rows.GroupBy(r => r.Role ?? "None").Select(g => (g.Key, g.Count())))
        });
        ViewBag.Snap = JsonSerializer.Serialize(await Snap(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return View(new AdminVm
        {
            Users = rows,
            Listings = await db.Listings.Include(l => l.Produce).Include(l => l.Farmer).OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync(),
            Orders = await db.Orders.Include(o => o.Items).Include(o => o.Buyer).Include(o => o.Farmer).OrderByDescending(o => o.CreatedAt).Take(50).ToListAsync(),
            CompletedRevenue = await db.Orders.Where(o => o.Status == OrderStatus.Completed).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var u = await um.FindByIdAsync(id);
        if (u == null || await um.IsInRoleAsync(u, "Admin")) { TempData["Error"] = "Cannot change this account."; return RedirectToAction(nameof(Index)); }
        var locked = u.LockoutEnd > DateTimeOffset.UtcNow;
        await um.SetLockoutEnabledAsync(u, true);
        await um.SetLockoutEndDateAsync(u, locked ? null : DateTimeOffset.MaxValue);
        await um.UpdateSecurityStampAsync(u);   // signs the user out of existing sessions within ~1 minute
        TempData["Ok"] = locked ? "User unlocked." : "User locked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleListing(int id)
    {
        var l = await db.Listings.FindAsync(id);
        if (l == null) return NotFound();
        if (l.Status == ListingStatus.Active) l.Status = ListingStatus.Inactive;
        else if (l.QuantityAvailable > 0) l.Status = ListingStatus.Active;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}