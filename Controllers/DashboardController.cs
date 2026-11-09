using System.Text.Json;
namespace FarmConnect.Controllers;

[Authorize]
public class DashboardController(IFarmerService farmers, IOrderService orders, IProduceService produce, UserManager<ApplicationUser> um) : Controller
{
    public IActionResult Index() =>
        User.IsInRole("Admin") ? RedirectToAction("Index", "Admin") :
        User.IsInRole("Farmer") ? RedirectToAction(nameof(Farmer)) : RedirectToAction(nameof(Buyer));

    static List<DateTime> Days(int n) => Enumerable.Range(0, n).Select(i => DateTime.Today.AddDays(i - (n - 1))).ToList();

    [Authorize(Roles = "Farmer")]
    public async Task<IActionResult> Farmer()
    {
        var uid = um.GetUserId(User);
        var d = await farmers.GetDashboardAsync(uid);
        var os = await farmers.GetFarmerOrdersAsync(uid);
        var ls = await farmers.GetFarmerListingsAsync(uid);
        var days = Days(7);
        var sales = days.Select(x => os.Where(o => o.Status == OrderStatus.Completed && o.CompletedAt.HasValue && o.CompletedAt.Value.Date == x).Sum(o => o.TotalAmount)).ToList();
        var placed = days.Select(x => os.Count(o => o.CreatedAt.Date == x)).ToList();
        var st = os.GroupBy(o => o.Status).Select(g => new { k = g.Key.ToString(), n = g.Count() }).ToList();
        ViewBag.Active = d.Active; ViewBag.Pending = d.Pending; ViewBag.Completed = d.Completed; ViewBag.Sales = d.Sales;
        ViewBag.Total = os.Count; ViewBag.Rate = os.Count == 0 ? 0 : (int)Math.Round(100.0 * d.Completed / os.Count);
        ViewBag.Week = sales.Sum();
        ViewBag.Dash = JsonSerializer.Serialize(new { labels = days.Select(x => x.ToString("dd MMM")), sales, placed, status = new { labels = st.Select(x => x.k), data = st.Select(x => x.n) } });
        ViewBag.ToReview = os.Where(o => o.Status == OrderStatus.Placed).Take(5).ToList();
        ViewBag.Recent = os.Take(6).ToList();
        ViewBag.Stock = ls.Take(6).ToList();
        return View();
    }

    [Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Buyer()
    {
        var os = await orders.GetBuyerOrdersAsync(um.GetUserId(User));
        var days = Days(14);
        bool live(Order o) => o.Status != OrderStatus.Rejected && o.Status != OrderStatus.Cancelled;
        var spend = days.Select(x => os.Where(o => live(o) && o.CreatedAt.Date == x).Sum(o => o.TotalAmount)).ToList();
        var st = os.GroupBy(o => o.Status).Select(g => new { k = g.Key.ToString(), n = g.Count() }).ToList();
        ViewBag.Open = os.Count(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Accepted);
        ViewBag.Done = os.Count(o => o.Status == OrderStatus.Completed);
        ViewBag.Total = os.Count;
        ViewBag.Spent = os.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount);
        ViewBag.Dash = JsonSerializer.Serialize(new { labels = days.Select(x => x.ToString("dd MMM")), spend, status = new { labels = st.Select(x => x.k), data = st.Select(x => x.n) } });
        ViewBag.Recent = os.Take(5).ToList();
        ViewBag.Picks = (await produce.SearchListingsAsync(null, null, null, null, null, null)).Take(4).ToList();
        return View();
    }
}