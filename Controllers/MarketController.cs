namespace FarmConnect.Controllers;

[AllowAnonymous]
public class MarketController(IProduceService produce, IPriceCacheService prices, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index(string q, string grade, string location, decimal? minPrice, decimal? maxPrice, string sort, int? pulse)
    {
        var farmerId = User.IsInRole("Farmer") ? um.GetUserId(User) : null;   // farmers browse their own listings
        var items = await produce.SearchListingsAsync(q, grade, location, minPrice, maxPrice, sort, farmerId);

        // Market Pulse: price rows come through the Redis-backed cache service
        var list = await produce.GetAllProduceAsync();
        int? pid = pulse ?? items.Select(l => (int?)l.ProduceId).FirstOrDefault() ?? list.FirstOrDefault()?.ProduceId;
        var rows = pid.HasValue ? (await prices.GetPricesAsync(pid.Value, null, DateTime.Today.AddDays(-60), null)).Rows : new List<PriceHistoryView>();
        var unit = rows.LastOrDefault()?.Unit;
        var series = rows.Where(r => r.Unit == unit).GroupBy(r => r.PriceDate).OrderBy(g => g.Key).TakeLast(14)
            .Select(g => new { d = g.Key, v = Math.Round(g.Average(r => r.ModalPrice), 2) }).ToList();
        ViewBag.ProduceList = list;
        ViewBag.PulseId = pid;
        ViewBag.PulseName = list.FirstOrDefault(p => p.ProduceId == pid)?.Name;
        ViewBag.PulseUnit = unit;
        ViewBag.PulseSource = rows.LastOrDefault()?.Source;
        ViewBag.PulseSample = (rows.LastOrDefault()?.Source ?? "").Contains("ample", StringComparison.OrdinalIgnoreCase);
        ViewBag.PulseLast = series.Count > 0 ? series[^1].d.ToString("dd MMM yyyy") : null;
        ViewBag.PulseLatest = series.Count > 0 ? series[^1].v : 0m;
        ViewBag.PulseDelta = series.Count > 1 ? series[^1].v - series[^2].v : 0m;
        ViewBag.PulseJson = System.Text.Json.JsonSerializer.Serialize(new { labels = series.Select(s => s.d.ToString("dd MMM")), data = series.Select(s => s.v) });
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await produce.GetListingByIdAsync(id);
        if (item == null) return NotFound();
        var uid = um.GetUserId(User);
        if (User.IsInRole("Farmer") && item.FarmerId != uid) return NotFound();
        if (item.Status == ListingStatus.Inactive && !User.IsInRole("Admin") && item.FarmerId != uid) return NotFound();
        return View(item);
    }
}