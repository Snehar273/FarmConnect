namespace FarmConnect.Controllers;

[Authorize(Roles = "Admin")]
public class LogisticsController(ILogisticsService logistics, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Partners = await logistics.GetPartnersAsync();
        ViewBag.Outbox = await db.OutboxEvents.AsNoTracking().OrderByDescending(e => e.CreatedAt).Take(12).ToListAsync();
        var counts = await db.OutboxEvents.GroupBy(e => e.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        ViewBag.OutCounts = counts.ToDictionary(x => x.Key.ToString(), x => x.N);
        return View(await logistics.GetBookingsAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int bookingId, int partnerId)
    {
        var r = await logistics.AssignPartnerAsync(bookingId, partnerId);
        TempData[r.Ok ? "Ok" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rematch()
    {
        var n = await logistics.RematchPendingAsync();
        TempData[n > 0 ? "Ok" : "Error"] = n > 0 ? n + " pending booking(s) matched to a partner." : "No pending booking could be matched yet.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePartner(int id) { await logistics.TogglePartnerAsync(id); return RedirectToAction(nameof(Index)); }
}