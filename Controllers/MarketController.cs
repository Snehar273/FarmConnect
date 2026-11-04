using FarmConnect.Data;
using FarmConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmConnect.Controllers
{
    [AllowAnonymous]
    public class MarketController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> um) : Controller
    {
        public async Task<IActionResult> Index(
            string q = "",
            string grade = "",
            string location = "",
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string sort = "",
            int? pulse = null)
        {
            // 1. Get active listings
            IQueryable<Listing> qry = db.Listings
                .Include(l => l.Produce)
                .Include(l => l.Farmer)
                .Where(l =>
                    l.Status == ListingStatus.Active &&
                    l.QuantityAvailable > 0);

            // 2. Restrict farmers to their own listings
            if (User.IsInRole("Farmer"))
            {
                var uid = um.GetUserId(User);
                qry = qry.Where(l => l.FarmerId == uid);
            }

            // 3. Search filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                qry = qry.Where(l =>
                    l.Produce.Name.Contains(q) ||
                    l.Variety.Contains(q));
            }

            // 4. Grade filter
            if (!string.IsNullOrWhiteSpace(grade))
            {
                qry = qry.Where(l => l.Grade == grade);
            }

            // 5. Location filter
            if (!string.IsNullOrWhiteSpace(location))
            {
                qry = qry.Where(l => l.Location.Contains(location));
            }

            // 6. Minimum price filter
            if (minPrice.HasValue)
            {
                qry = qry.Where(l =>
                    l.PricePerUnit >= minPrice.Value);
            }

            // 7. Maximum price filter
            if (maxPrice.HasValue)
            {
                qry = qry.Where(l =>
                    l.PricePerUnit <= maxPrice.Value);
            }

            // 8. Sorting
            qry = sort switch
            {
                "low" => qry.OrderBy(l => l.PricePerUnit),
                "high" => qry.OrderByDescending(l => l.PricePerUnit),
                _ => qry.OrderByDescending(l => l.CreatedAt)
            };

            // 9. Execute listing query
            var items = await qry.ToListAsync();

            // 10. Get produce collection
            var produce = await db.ProduceItems
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Always send a valid collection to the view
            ViewBag.ProduceList = produce;

            // 11. Select produce for Market Pulse
            int? pid =
                pulse
                ?? items.Select(l => (int?)l.ProduceId).FirstOrDefault()
                ?? produce.Select(p => (int?)p.ProduceId).FirstOrDefault();

            // 12. Get price history
            var rows = await db.PriceHistoryView
                .AsNoTracking()
                .Where(x => x.ProduceId == pid)
                .OrderByDescending(x => x.PriceDate)
                .Take(120)
                .ToListAsync();

            // 13. Prepare price series
            var unit = rows.FirstOrDefault()?.Unit;

            var series = rows
                .Where(r => r.Unit == unit)
                .GroupBy(r => r.PriceDate)
                .OrderBy(g => g.Key)
                .TakeLast(14)
                .Select(g => new
                {
                    d = g.Key,
                    v = Math.Round(g.Average(r => r.ModalPrice), 2)
                })
                .ToList();

            // 14. Market Pulse values
            ViewBag.PulseId = pid;

            ViewBag.PulseName =
                produce.FirstOrDefault(p => p.ProduceId == pid)?.Name;

            ViewBag.PulseUnit = unit;

            ViewBag.PulseSource =
                rows.FirstOrDefault()?.Source;

            ViewBag.PulseSample =
                (rows.FirstOrDefault()?.Source ?? "")
                .Contains("ample", StringComparison.OrdinalIgnoreCase);

            ViewBag.PulseLast =
                series.Count > 0
                    ? series[^1].d.ToString("dd MMM yyyy")
                    : null;

            ViewBag.PulseLatest =
                series.Count > 0
                    ? series[^1].v
                    : 0m;

            ViewBag.PulseDelta =
                series.Count > 1
                    ? series[^1].v - series[^2].v
                    : 0m;

            // 15. Chart JSON
            ViewBag.PulseJson =
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    labels = series.Select(s => s.d.ToString("dd MMM")),
                    data = series.Select(s => s.v)
                });

            // 16. Return view
            return View(items);
        }

        // Listing details
        public async Task<IActionResult> Details(int id)
        {
            var item = await db.Listings
                .Include(l => l.Produce)
                .Include(l => l.Farmer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            var uid = um.GetUserId(User);

            if (User.IsInRole("Farmer") && item.FarmerId != uid)
            {
                return NotFound();
            }

            if (item.Status == ListingStatus.Inactive &&
                !User.IsInRole("Admin") &&
                item.FarmerId != uid)
            {
                return NotFound();
            }

            return View(item);
        }
    }
}