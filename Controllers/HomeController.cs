namespace FarmConnect.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Farmers = await db.FarmerProfiles.CountAsync();
        ViewBag.Listings = await db.Listings.CountAsync(l => l.Status == ListingStatus.Active);
        ViewBag.Orders = await db.Orders.CountAsync();
        var items = await db.Listings.Include(l => l.Produce).Include(l => l.Farmer)
            .Where(l => l.Status == ListingStatus.Active && l.QuantityAvailable > 0)
            .OrderByDescending(l => l.CreatedAt).Take(4).ToListAsync();
        return View(items);
    }
    public IActionResult Error() => View();
}
