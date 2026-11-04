using FarmConnect.Data;
using FarmConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FarmConnect.Controllers
{
    [Authorize]
    public class PriceController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index(
            int? produceId,
            string market = "",
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            // 1. Load available produce
            var produce = await db.ProduceItems
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            // 2. Select the first produce by default
            produceId ??= produce.FirstOrDefault()?.ProduceId;

            // 3. Get price history from SQL view
            var query = db.PriceHistoryView
                .AsNoTracking()
                .AsQueryable();

            if (produceId.HasValue)
            {
                query = query.Where(x => x.ProduceId == produceId.Value);
            }
            else
            {
                query = query.Where(x => false);
            }

            // 4. Apply market filter
            if (!string.IsNullOrWhiteSpace(market))
            {
                query = query.Where(x => x.MarketName == market);
            }

            // 5. Apply date filters
            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(x => x.PriceDate >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(x => x.PriceDate < end);
            }

            // 6. Retrieve records
            var rows = await query
                .OrderBy(x => x.PriceDate)
                .ToListAsync();

            // 7. Available markets for the selected produce
            ViewBag.Markets = await db.PriceHistoryView
                .AsNoTracking()
                .Where(x => x.ProduceId == produceId)
                .Select(x => x.MarketName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            // 8. Produce dropdown
            ViewBag.Produce = new SelectList(
                produce,
                "ProduceId",
                "Name",
                produceId
            );

            // 9. Available units
            var units = rows
                .Select(x => x.Unit)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            ViewBag.Units = units;

            // 10. Calculate statistics
            ViewBag.Min = rows.Count > 0
                ? rows.Min(x => x.MinPrice)
                : 0m;

            ViewBag.Max = rows.Count > 0
                ? rows.Max(x => x.MaxPrice)
                : 0m;

            ViewBag.Avg = rows.Count > 0
                ? rows.Average(x => x.ModalPrice)
                : 0m;

            // 11. Generate unique calendar-date labels
            var dates = rows
                .Select(x => x.PriceDate.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var labels = dates
                .Select(x => x.ToString("dd MMM yyyy"))
                .ToList();

            // 12. Create one chart dataset per market
            var sets = rows
                .GroupBy(x => x.MarketName)
                .Select(group => new
                {
                    label = group.Key,

                    data = dates.Select(date =>
                    {
                        var dailyPrices = group
                            .Where(x => x.PriceDate.Date == date)
                            .Select(x => (decimal?)x.ModalPrice)
                            .ToList();

                        return dailyPrices.Count > 0
                            ? dailyPrices.Average()
                            : (decimal?)null;
                    }).ToList()
                })
                .ToList();

            // 13. Serialize chart data
            ViewBag.Chart = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    labels,
                    sets
                },
                new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web)
            );

            // 14. Return view
            return View(rows);
        }
    }
}