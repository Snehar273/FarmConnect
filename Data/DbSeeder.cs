namespace FarmConnect.Data;

public static class DbSeeder
{
    const string ViewSql = @"CREATE OR ALTER VIEW dbo.vw_PriceHistory AS
SELECT ph.PriceHistoryId, ph.ProduceId, p.Name AS ProduceName, ph.MarketName, ph.PriceDate,
       ph.MinPrice, ph.MaxPrice, ph.ModalPrice, ph.Unit, ph.Source
FROM dbo.PriceHistory ph INNER JOIN dbo.Produce p ON ph.ProduceId = p.ProduceId;";

    // M2 tables for databases created in M1 (EnsureCreated skips an existing database). Safe to run on every start.
    const string M2Sql = @"
IF OBJECT_ID(N'dbo.LogisticsPartners', N'U') IS NULL
CREATE TABLE dbo.LogisticsPartners(
  LogisticsPartnerId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogisticsPartners PRIMARY KEY,
  PartnerName nvarchar(100) NOT NULL, ContactNumber nvarchar(15) NULL,
  PickupAreas nvarchar(300) NULL, DeliveryAreas nvarchar(300) NULL, VehicleType nvarchar(50) NULL,
  CapacityKg decimal(18,2) NOT NULL, RatePerKg decimal(18,2) NOT NULL,
  IsAvailable bit NOT NULL, Status nvarchar(20) NOT NULL);

IF OBJECT_ID(N'dbo.LogisticsBookings', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.LogisticsBookings(
    BookingId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogisticsBookings PRIMARY KEY,
    OrderId int NOT NULL CONSTRAINT FK_LogisticsBookings_Orders FOREIGN KEY REFERENCES dbo.Orders(Id),
    LogisticsPartnerId int NULL CONSTRAINT FK_LogisticsBookings_Partners FOREIGN KEY REFERENCES dbo.LogisticsPartners(LogisticsPartnerId),
    PickupLocation nvarchar(80) NULL, DeliveryLocation nvarchar(200) NULL,
    QuantityKg decimal(18,2) NULL, EstimatedCost decimal(18,2) NULL,
    BookingStatus nvarchar(30) NOT NULL, Note nvarchar(300) NULL,
    CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NOT NULL);
  CREATE UNIQUE INDEX IX_LogisticsBookings_OrderId ON dbo.LogisticsBookings(OrderId);
  CREATE INDEX IX_LogisticsBookings_LogisticsPartnerId ON dbo.LogisticsBookings(LogisticsPartnerId);
END

IF OBJECT_ID(N'dbo.OutboxEvents', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.OutboxEvents(
    EventId uniqueidentifier NOT NULL CONSTRAINT PK_OutboxEvents PRIMARY KEY,
    EventType nvarchar(60) NOT NULL, Payload nvarchar(max) NOT NULL,
    CreatedAt datetime2 NOT NULL, ProcessedAt datetime2 NULL, Status nvarchar(20) NOT NULL,
    RetryCount int NOT NULL, ErrorMessage nvarchar(500) NULL);
  CREATE INDEX IX_OutboxEvents_Status_CreatedAt ON dbo.OutboxEvents(Status, CreatedAt);
END";

    public static async Task InitAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var isDev = sp.GetRequiredService<IHostEnvironment>().IsDevelopment();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(M2Sql);
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

        // admin: the password must come from user-secrets / environment outside Development
        var adminEmail = cfg["Seed:AdminEmail"] ?? "admin@farmconnect.com";
        if (await um.FindByEmailAsync(adminEmail) == null)
        {
            var adminPass = cfg["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPass))
            {
                if (!isDev) throw new InvalidOperationException("Set Seed:AdminPassword (user-secrets or the Seed__AdminPassword environment variable) before the first run outside Development.");
                adminPass = "Admin@12345";
            }
            await Make(adminEmail, "System Admin", adminPass, "Admin", "9000000000");
        }

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

        if (isDev)   // demo accounts with known passwords exist ONLY in Development
        {
            var f = await Make("farmer@demo.com", "Murugan Selvam", "Demo@12345", "Farmer", "9876543210", "Erode");
            var demoBuyer = await Make("buyer@demo.com", "Priya Traders", "Demo@12345", "Buyer", "9123456780");
            var bp = await db.BuyerProfiles.FirstOrDefaultAsync(x => x.UserId == demoBuyer.Id);
            if (bp != null && string.IsNullOrEmpty(bp.Address)) { bp.Address = "Salem"; await db.SaveChangesAsync(); }

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

        // demo transport partners: created once, coverage kept in sync on every start
        void Partner(string name, string phone, string pick, string del, string veh, decimal cap, decimal rate)
        {
            var p = db.LogisticsPartners.FirstOrDefault(x => x.PartnerName == name);
            if (p == null) db.LogisticsPartners.Add(new LogisticsPartner { PartnerName = name, ContactNumber = phone, PickupAreas = pick, DeliveryAreas = del, VehicleType = veh, CapacityKg = cap, RatePerKg = rate });
            else { p.PickupAreas = pick; p.DeliveryAreas = del; }
        }
        Partner("Erode Fresh Transport", "9000000001", "Erode, Coimbatore", "Erode, Salem, Coimbatore", "Mini Truck", 1000, 4.5m);
        Partner("Kongu Agro Logistics", "9000000002", "Erode, Tiruppur, Coimbatore", "Erode, Tiruppur, Coimbatore, Salem, Namakkal", "Lorry", 2000, 3.8m);
        Partner("GreenRoute Delivery", "9000000003", "Salem, Namakkal", "Salem, Namakkal, Erode, Coimbatore", "Pickup Van", 750, 4.2m);
        Partner("Kovai Express Logistics", "9000000004", "Coimbatore, Tiruppur", "Coimbatore, Tiruppur, Erode, Salem", "Mini Van", 1500, 4.0m);
        await db.SaveChangesAsync();
        await sp.GetRequiredService<ILogisticsService>().RematchPendingAsync();
    }
}