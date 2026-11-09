using System.Diagnostics;
using System.Text.Json;
using FarmConnect.Data;
using FarmConnect.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FarmConnect.Services;

// ---------- Logistics ----------
public class LogisticsService(ApplicationDbContext db) : ILogisticsService
{
    static decimal? ToKg(decimal q, string unit) => (unit ?? "").ToLower() switch { "kg" => q, "quintal" => q * 100, "ton" => q * 1000, _ => null };
    static bool Covers(string areas, string place) =>
        !string.IsNullOrWhiteSpace(place) && (areas ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(a => place.Contains(a, StringComparison.OrdinalIgnoreCase));
    static bool Eligible(LogisticsPartner p, string pickup, string delivery, decimal kg) =>
        p.Status == "Active" && p.IsAvailable && p.CapacityKg >= kg && Covers(p.PickupAreas, pickup) && Covers(p.DeliveryAreas, delivery);

    public async Task<LogisticsBooking> BookForOrderAsync(int orderId, CancellationToken ct = default)
    {
        var existing = await db.LogisticsBookings.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (existing != null) return existing;                          // idempotent: same event twice => still ONE booking
        var o = await db.Orders.Include(x => x.Items).ThenInclude(i => i.Listing).FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o == null || o.Status == OrderStatus.Rejected || o.Status == OrderStatus.Cancelled) return null;

        var it = o.Items.First();
        var pickup = it.Listing.Location;
        var delivery = (await db.BuyerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == o.BuyerId, ct))?.Address;
        var kg = ToKg(it.Quantity, it.Unit);
        var booking = new LogisticsBooking { OrderId = orderId, PickupLocation = pickup, DeliveryLocation = delivery, QuantityKg = kg };
        LogisticsPartner partner = null;

        if (kg == null) booking.Note = $"Unit '{it.Unit}' cannot be converted to kg - admin must assign a partner.";
        else if (string.IsNullOrWhiteSpace(delivery)) booking.Note = "Buyer has no delivery location on profile - admin must assign.";
        else
        {
            var all = await db.LogisticsPartners.AsNoTracking().ToListAsync(ct);
            partner = all.Where(p => Eligible(p, pickup, delivery, kg.Value)).OrderBy(p => p.RatePerKg).FirstOrDefault();
            if (partner == null) booking.Note = $"No active partner covers {pickup} to {delivery} for {kg.Value:0.##} kg.";
        }
        if (partner != null)
        {
            booking.LogisticsPartnerId = partner.LogisticsPartnerId; booking.BookingStatus = BookingStatus.Assigned;
            booking.EstimatedCost = Math.Round(kg.Value * partner.RatePerKg, 2); booking.Note = "Auto-assigned (estimate only).";
        }
        else booking.BookingStatus = BookingStatus.PendingAssignment;

        db.LogisticsBookings.Add(booking);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)   // unique OrderId violated by a concurrent duplicate
        {
            db.Entry(booking).State = EntityState.Detached;
            return await db.LogisticsBookings.FirstAsync(x => x.OrderId == orderId, ct);
        }
        return booking;
    }

    public async Task<(bool Ok, string Message)> AssignPartnerAsync(int bookingId, int partnerId)
    {
        var bk = await db.LogisticsBookings.FirstOrDefaultAsync(x => x.BookingId == bookingId);
        var p = await db.LogisticsPartners.FindAsync(partnerId);
        if (bk == null || p == null) return (false, "Booking or partner not found.");
        if (bk.BookingStatus == BookingStatus.Cancelled || bk.BookingStatus == BookingStatus.Delivered) return (false, "This booking is closed.");
        if (p.Status != "Active" || !p.IsAvailable) return (false, "Partner is not available.");
        if (bk.QuantityKg.HasValue && (p.CapacityKg < bk.QuantityKg.Value || !Covers(p.PickupAreas, bk.PickupLocation) || !Covers(p.DeliveryAreas, bk.DeliveryLocation)))
            return (false, "Partner does not cover this route or lacks capacity.");
        bk.LogisticsPartnerId = p.LogisticsPartnerId; bk.BookingStatus = BookingStatus.Assigned; bk.UpdatedAt = DateTime.Now;
        bk.EstimatedCost = bk.QuantityKg.HasValue ? Math.Round(bk.QuantityKg.Value * p.RatePerKg, 2) : null;
        bk.Note = "Assigned by admin (estimate only).";
        await db.SaveChangesAsync();
        return (true, $"Assigned to {p.PartnerName}.");
    }

    public Task<List<LogisticsBooking>> GetBookingsAsync() =>
        db.LogisticsBookings.Include(x => x.Partner).Include(x => x.Order).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
    public Task<List<LogisticsPartner>> GetPartnersAsync() => db.LogisticsPartners.OrderBy(p => p.PartnerName).ToListAsync();
    public async Task<bool> TogglePartnerAsync(int id)
    {
        var p = await db.LogisticsPartners.FindAsync(id);
        if (p == null) return false;
        p.IsAvailable = !p.IsAvailable;
        await db.SaveChangesAsync();
        return true;
    }
        public async Task<int> RematchPendingAsync()
    {
        var pending = await db.LogisticsBookings.Where(b => b.BookingStatus == BookingStatus.PendingAssignment).ToListAsync();
        var partners = await db.LogisticsPartners.AsNoTracking().ToListAsync();
        int n = 0;
        foreach (var b in pending)
        {
            if (!b.QuantityKg.HasValue || string.IsNullOrWhiteSpace(b.DeliveryLocation)) continue;
            var p = partners.Where(x => Eligible(x, b.PickupLocation, b.DeliveryLocation, b.QuantityKg.Value)).OrderBy(x => x.RatePerKg).FirstOrDefault();
            if (p == null) continue;
            b.LogisticsPartnerId = p.LogisticsPartnerId; b.BookingStatus = BookingStatus.Assigned;
            b.EstimatedCost = Math.Round(b.QuantityKg.Value * p.RatePerKg, 2);
            b.Note = "Auto-assigned on re-match (estimate only)."; b.UpdatedAt = DateTime.Now; n++;
        }
        if (n > 0) await db.SaveChangesAsync();
        return n;
    }
}

// ---------- Event handler + Outbox processor ----------
public class OrderPlacedHandler(ILogisticsService logistics) : IEventHandler
{
    public string EventType => "OrderPlaced";
    public async Task HandleAsync(OutboxEvent e, CancellationToken ct)
    {
        var ev = JsonSerializer.Deserialize<OrderPlacedEvent>(e.Payload);
        await logistics.BookForOrderAsync(ev.OrderId, ct);
    }
}

public class OutboxProcessor(IServiceScopeFactory sf, ILogger<OutboxProcessor> log) : BackgroundService
{
        protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try   // crash recovery: events left "Processing" go back to Pending
        {
            using var s = sf.CreateScope();
            var d = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await d.OutboxEvents.Where(x => x.Status == OutboxStatus.Processing)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, OutboxStatus.Pending), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Outbox recovery skipped"); }

        while (!ct.IsCancellationRequested)
        {
            try { if (await RunOnce(ct) == 0) await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { log.LogError(ex, "Outbox loop failed"); await Task.Delay(5000, ct); }
        }
    }

    async Task<int> RunOnce(CancellationToken ct)
    {
        using var scope = sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>().ToDictionary(h => h.EventType);
        var batch = await db.OutboxEvents.Where(x => x.Status == OutboxStatus.Pending).OrderBy(x => x.CreatedAt).Take(20).ToListAsync(ct);
        int done = 0;
        foreach (var ev in batch)
        {
            var id = ev.EventId;
            var claimed = await db.OutboxEvents.Where(x => x.EventId == id && x.Status == OutboxStatus.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, OutboxStatus.Processing), ct);
            if (claimed == 0) continue;   // claimed elsewhere
            try
            {
                if (!handlers.TryGetValue(ev.EventType, out var handler)) throw new InvalidOperationException("No handler for " + ev.EventType);
                await handler.HandleAsync(ev, ct);
                await db.OutboxEvents.Where(x => x.EventId == id).ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.Status, OutboxStatus.Processed).SetProperty(x => x.ProcessedAt, DateTime.Now).SetProperty(x => x.ErrorMessage, (string)null), ct);
                done++;
            }
            catch (Exception ex)
            {
                var retry = ev.RetryCount + 1; var st = retry >= 5 ? OutboxStatus.Failed : OutboxStatus.Pending;
                var msg = ex.Message.Length > 450 ? ex.Message[..450] : ex.Message;
                log.LogWarning("Outbox event {Id} failed (try {R}): {M}", id, retry, msg);
                await db.OutboxEvents.Where(x => x.EventId == id).ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.Status, st).SetProperty(x => x.RetryCount, retry).SetProperty(x => x.ErrorMessage, msg), ct);
            }
        }
        return done;
    }
}

// ---------- Redis price cache (cache-aside, versioned invalidation, SQL fallback) ----------
public class PriceCacheService(ApplicationDbContext db, IDistributedCache cache, IConfiguration cfg, ILogger<PriceCacheService> log) : IPriceCacheService
{
    static DateTime _skipUntil = DateTime.MinValue;     // circuit breaker: skip Redis for 30s after a failure
    public static long Hits, SqlReads;
    static bool RedisUp => DateTime.UtcNow >= _skipUntil;
    void Down(Exception e) { _skipUntil = DateTime.UtcNow.AddSeconds(30); log.LogWarning("Redis unavailable, using SQL Server for 30s: {M}", e.Message); }
    TimeSpan Ttl(string k, int def) => TimeSpan.FromMinutes(cfg.GetValue<int>(k, def));

    async Task<(T Value, string Source)> GetOrSet<T>(string key, TimeSpan ttl, Func<Task<T>> load)
    {
        var redis = RedisUp;
        if (redis)
        {
            try
            {
                var s = await cache.GetStringAsync(key);
                if (s != null) { Interlocked.Increment(ref Hits); return (JsonSerializer.Deserialize<T>(s), "Redis cache - HIT"); }
            }
            catch (Exception e) { Down(e); redis = false; }
        }
        Interlocked.Increment(ref SqlReads);
        var v = await load();
        if (redis)
        {
            try
            {
                await cache.SetStringAsync(key, JsonSerializer.Serialize(v), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
                return (v, "SQL Server - cache MISS, now stored in Redis");
            }
            catch (Exception e) { Down(e); }
        }
        return (v, "SQL Server - Redis unavailable (fallback)");
    }

    async Task<string> Ver(int pid)
    {
        if (!RedisUp) return "0";
        try { return await cache.GetStringAsync("pv:" + pid) ?? "0"; } catch (Exception e) { Down(e); return "0"; }
    }

    public async Task<PriceResult> GetPricesAsync(int produceId, string market, DateTime? fromDate, DateTime? toDate)
    {
        var sw = Stopwatch.StartNew();
        var ver = await Ver(produceId);
        var key = $"prices:{produceId}:{(string.IsNullOrEmpty(market) ? "all" : market.ToLowerInvariant())}:{fromDate?.ToString("yyyyMMdd") ?? "any"}:{toDate?.ToString("yyyyMMdd") ?? "any"}:v{ver}";
        var openEnded = !toDate.HasValue || toDate.Value.Date >= DateTime.Today;
        var ttl = openEnded ? Ttl("Redis:TodayTtlMinutes", 30) : Ttl("Redis:HistoryTtlMinutes", 60);
        var result = await GetOrSet<List<PriceHistoryView>>(key, ttl, async () =>
        {
            IQueryable<PriceHistoryView> q = db.PriceHistoryView.AsNoTracking().Where(x => x.ProduceId == produceId);
            if (!string.IsNullOrEmpty(market)) q = q.Where(x => x.MarketName == market);
            if (fromDate.HasValue) q = q.Where(x => x.PriceDate >= fromDate.Value.Date);
            if (toDate.HasValue) q = q.Where(x => x.PriceDate <= toDate.Value.Date);
            return await q.OrderBy(x => x.PriceDate).ToListAsync();
        });
        return new PriceResult(result.Value, result.Source, sw.ElapsedMilliseconds);
    }

    public Task<PriceResult> GetDailyPricesAsync(int produceId, string market, DateTime date) => GetPricesAsync(produceId, market, date.Date, date.Date);

    public async Task<List<string>> GetMarketsAsync()
    {
        var r = await GetOrSet<List<string>>("markets", Ttl("Redis:StaticTtlMinutes", 360),
            () => db.PriceHistories.Select(x => x.MarketName).Distinct().OrderBy(x => x).ToListAsync());
        return r.Value;
    }

    public async Task AddPriceAsync(PriceHistory p)
    {
        db.PriceHistories.Add(p);
        await db.SaveChangesAsync();
        await InvalidateProduceAsync(p.ProduceId);
        try { if (RedisUp) await cache.RemoveAsync("markets"); } catch (Exception e) { Down(e); }
    }

    public async Task InvalidateProduceAsync(int produceId)   // bumping the version makes every old key for this produce unreachable
    {
        if (!RedisUp) return;
        try { await cache.SetStringAsync("pv:" + produceId, DateTime.UtcNow.Ticks.ToString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }); }
        catch (Exception e) { Down(e); }
    }

    public async Task FlushAsync()
    {
        foreach (var id in await db.ProduceItems.Select(p => p.ProduceId).ToListAsync()) await InvalidateProduceAsync(id);
        try { if (RedisUp) await cache.RemoveAsync("markets"); } catch (Exception e) { Down(e); }
    }
}