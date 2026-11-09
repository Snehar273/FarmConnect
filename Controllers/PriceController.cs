namespace FarmConnect.Controllers;

[Authorize]
public class PriceController(IProduceService produce, IPriceCacheService prices) : Controller
{
    public async Task<IActionResult> Index(int? produceId, string market, DateTime? fromDate, DateTime? toDate)
    {
        var list = await produce.GetAllProduceAsync();
        produceId ??= list.FirstOrDefault()?.ProduceId;
        var res = produceId.HasValue ? await prices.GetPricesAsync(produceId.Value, market, fromDate, toDate) : new PriceResult(new(), "No produce", 0);
        var rows = res.Rows;

        var labels = rows.Select(r => r.PriceDate.ToString("dd MMM")).Distinct().ToList();
        var sets = rows.GroupBy(r => r.MarketName).Select(g => new
        {
            label = g.Key,
            data = labels.Select(lb => g.Where(r => r.PriceDate.ToString("dd MMM") == lb).Select(r => (decimal?)r.ModalPrice).FirstOrDefault()).ToList()
        }).ToList();

        ViewBag.Produce = new SelectList(list, "ProduceId", "Name", produceId);
        ViewBag.ProduceAll = new SelectList(list, "ProduceId", "Name");
        ViewBag.Markets = await prices.GetMarketsAsync();
        ViewBag.Chart = System.Text.Json.JsonSerializer.Serialize(new { labels, sets });
        ViewBag.Units = rows.Select(r => r.Unit).Distinct().ToList();
        ViewBag.Min = rows.Any() ? rows.Min(r => r.MinPrice) : 0m;
        ViewBag.Max = rows.Any() ? rows.Max(r => r.MaxPrice) : 0m;
        ViewBag.Avg = rows.Any() ? rows.Average(r => r.ModalPrice) : 0m;
        ViewBag.Served = res.Source; ViewBag.Ms = res.Ms; ViewBag.Hits = PriceCacheService.Hits; ViewBag.SqlReads = PriceCacheService.SqlReads;
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Add(int produceId, string marketName, DateTime priceDate, decimal minPrice, decimal maxPrice, decimal modalPrice, string unit, string source)
    {
        var okUnit = unit is "kg" or "quintal";
        if (string.IsNullOrWhiteSpace(marketName) || !okUnit || minPrice <= 0 || !(minPrice <= modalPrice && modalPrice <= maxPrice) || await produce.GetProduceByIdAsync(produceId) == null)
        { TempData["Error"] = "Invalid price record (need min ≤ modal ≤ max, positive values, unit kg/quintal)."; return RedirectToAction(nameof(Index), new { produceId }); }
        await prices.AddPriceAsync(new PriceHistory { ProduceId = produceId, MarketName = marketName.Trim(), PriceDate = priceDate.Date, MinPrice = minPrice, MaxPrice = maxPrice, ModalPrice = modalPrice, Unit = unit, Source = string.IsNullOrWhiteSpace(source) ? "Admin entry" : source.Trim() });
        TempData["Ok"] = "Price saved and cache invalidated.";
        return RedirectToAction(nameof(Index), new { produceId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Flush() { await prices.FlushAsync(); TempData["Ok"] = "Price cache flushed."; return RedirectToAction(nameof(Index)); }
}