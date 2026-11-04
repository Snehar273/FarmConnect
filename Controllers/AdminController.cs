namespace FarmConnect.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index()
    {
        var rows = new List<UserRow>();
        foreach (var u in await um.Users.OrderBy(x => x.CreatedAt).ToListAsync())
            rows.Add(new UserRow { Id = u.Id, Name = u.FullName, Email = u.Email, Phone = u.PhoneNumber, Role = (await um.GetRolesAsync(u)).FirstOrDefault(), Locked = u.LockoutEnd > DateTimeOffset.UtcNow });
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
