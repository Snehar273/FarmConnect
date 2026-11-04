namespace FarmConnect.Controllers;

[Authorize]
public class DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> um) : Controller
{
    public IActionResult Index() =>
        User.IsInRole("Admin") ? RedirectToAction("Index", "Admin") :
        User.IsInRole("Farmer") ? RedirectToAction(nameof(Farmer)) : RedirectToAction(nameof(Buyer));

    [Authorize(Roles = "Farmer")]
    public async Task<IActionResult> Farmer()
    {
        var uid = um.GetUserId(User);
        ViewBag.Active = await db.Listings.CountAsync(l => l.FarmerId == uid && l.Status == ListingStatus.Active);
        ViewBag.Pending = await db.Orders.CountAsync(o => o.FarmerId == uid && o.Status == OrderStatus.Placed);
        ViewBag.Completed = await db.Orders.CountAsync(o => o.FarmerId == uid && o.Status == OrderStatus.Completed);
        ViewBag.Sales = await db.OrderItems.Where(i => i.Order.FarmerId == uid && i.Order.Status == OrderStatus.Completed)
            .SumAsync(i => (decimal?)(i.Quantity * i.AgreedUnitPrice)) ?? 0m;
        return View();
    }

    [Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Buyer()
    {
        var uid = um.GetUserId(User);
        ViewBag.Open = await db.Orders.CountAsync(o => o.BuyerId == uid && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Accepted));
        ViewBag.Done = await db.Orders.CountAsync(o => o.BuyerId == uid && o.Status == OrderStatus.Completed);
        ViewBag.Spent = await db.Orders.Where(o => o.BuyerId == uid && o.Status == OrderStatus.Completed).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        return View();
    }
}
