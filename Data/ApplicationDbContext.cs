
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmConnect.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<FarmerProfile> FarmerProfiles => Set<FarmerProfile>();
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();
    public DbSet<Produce> ProduceItems => Set<Produce>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<PriceHistoryView> PriceHistoryView => Set<PriceHistoryView>();
    public DbSet<SellingExpense> SellingExpenses => Set<SellingExpense>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c) => c.Properties<decimal>().HavePrecision(18, 2);

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Produce>().ToTable("Produce");
        b.Entity<PriceHistory>(e =>
        {
            e.ToTable("PriceHistory");
            e.Property(x => x.PriceDate).HasColumnType("date");
            e.HasIndex(x => new { x.ProduceId, x.PriceDate });
            e.HasIndex(x => x.MarketName);
            e.HasOne(x => x.Produce).WithMany().HasForeignKey(x => x.ProduceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PriceHistoryView>().HasNoKey().ToView("vw_PriceHistory");
        b.Entity<Listing>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_Listings_Stock", "[QuantityAvailable] >= 0"));
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.ProduceId });
            e.HasIndex(x => x.Location);
            e.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Produce).WithMany().HasForeignKey(x => x.ProduceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Order>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrderItem>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SellingExpense>(e =>
        {
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
