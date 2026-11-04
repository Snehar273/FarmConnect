namespace FarmConnect.Data;

public static class DbSeeder
{
    const string ViewSql = @"CREATE OR ALTER VIEW dbo.vw_PriceHistory AS
SELECT ph.PriceHistoryId, ph.ProduceId, p.Name AS ProduceName, ph.MarketName, ph.PriceDate,
       ph.MinPrice, ph.MaxPrice, ph.ModalPrice, ph.Unit, ph.Source
FROM dbo.PriceHistory ph INNER JOIN dbo.Produce p ON ph.ProduceId = p.ProduceId;";

    public static async Task InitAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(ViewSql);

        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { "Farmer", "Buyer", "Admin" })
            if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole(r));

        async Task<ApplicationUser> Make(string email, string name, string pass, string role, string phone, string loc = null)
        {
            var u = await um.FindByEmailAsync(email);
            if (u != null) return u;
            u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, PhoneNumber = phone };
            var res = await um.CreateAsync(u, pass);
            if (!res.Succeeded) throw new Exception("Seed failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await um.AddToRoleAsync(u, role);
            if (role == "Farmer") db.FarmerProfiles.Add(new FarmerProfile { UserId = u.Id, Location = loc });
            if (role == "Buyer") db.BuyerProfiles.Add(new BuyerProfile { UserId = u.Id });
            await db.SaveChangesAsync();
            return u;
        }

        await Make(cfg["Seed:AdminEmail"] ?? "admin@farmconnect.com", "System Admin", cfg["Seed:AdminPassword"] ?? "Admin@12345", "Admin", "9000000000");

        if (!await db.ProduceItems.AnyAsync())
        {
            db.ProduceItems.AddRange(
                new Produce { Name = "Tomato", Emoji = "🍅" }, new Produce { Name = "Onion", Emoji = "🧅" },
                new Produce { Name = "Potato", Emoji = "🥔" }, new Produce { Name = "Carrot", Emoji = "🥕" },
                new Produce { Name = "Brinjal", Emoji = "🍆" }, new Produce { Name = "Green Chilli", Emoji = "🌶️" },
                new Produce { Name = "Cabbage", Emoji = "🥬" }, new Produce { Name = "Banana", Emoji = "🍌" },
                new Produce { Name = "Maize", Emoji = "🌽" }, new Produce { Name = "Broccoli", Emoji = "🥦" });
            await db.SaveChangesAsync();
        }

        if (!await db.PriceHistories.AnyAsync())
        {
            var markets = new[] { "Erode", "Coimbatore", "Salem" };
            var rows = new List<PriceHistory>();
            foreach (var p in await db.ProduceItems.ToListAsync())
                for (int m = 0; m < markets.Length; m++)
                    for (int d = 29; d >= 0; d--)
                    {
                        int modal = 12 + p.ProduceId * 3 + ((d * 7 + m * 5 + p.ProduceId * 3) % 9) - 4 + m;
                        rows.Add(new PriceHistory { ProduceId = p.ProduceId, MarketName = markets[m], PriceDate = DateTime.Today.AddDays(-d), MinPrice = modal - 3, MaxPrice = modal + 4, ModalPrice = modal, Unit = "kg", Source = "Sample demo data (not live market prices)" });
                    }
            db.PriceHistories.AddRange(rows);
            await db.SaveChangesAsync();
        }

        var f = await Make("farmer@demo.com", "Murugan Selvam", "Demo@12345", "Farmer", "9876543210", "Erode");
        await Make("buyer@demo.com", "Priya Traders", "Demo@12345", "Buyer", "9123456780");
        if (!await db.Listings.AnyAsync())
        {
            var p = await db.ProduceItems.ToDictionaryAsync(x => x.Name);
            Listing L(string n, string v, string g, decimal q, decimal min, decimal price, string d, string loc) => new()
            { FarmerId = f.Id, ProduceId = p[n].ProduceId, Variety = v, Grade = g, QuantityAvailable = q, MinOrderQty = min, PricePerUnit = price, HarvestDate = DateTime.Today, Description = d, Location = loc };
            db.Listings.AddRange(
                L("Tomato", "Hybrid", "A", 500, 50, 25, "Freshly harvested tomatoes", "Erode"),
                L("Onion", "Red", "A", 800, 100, 22, "Firm, well-cured red onions", "Erode"),
                L("Broccoli", "Green", "B", 200, 20, 60, "Crisp broccoli heads", "Coimbatore"),
                L("Green Chilli", "Local", "A", 150, 10, 48, "Hot and fresh green chillies", "Salem"));
            await db.SaveChangesAsync();
        }
    }
}
