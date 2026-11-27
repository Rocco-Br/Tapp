using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tapp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        void StockLink<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<T, StockAmount> o) where T : class
        {
            o.WithOwner().HasForeignKey("OwnerId");
            o.Property<int>("Id");
            o.HasKey("Id");
            o.HasOne<StockItem>().WithMany().HasForeignKey(s => s.StockItemId).OnDelete(DeleteBehavior.Restrict);
        }

        b.Entity<Event>().HasIndex(e => e.IsRunning).IsUnique().HasFilter("IsRunning = 1");

        b.Entity<Order>().Property(o => o.Status).HasConversion<string>();
        b.Entity<Order>().HasOne<Event>().WithMany().HasForeignKey(o => o.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne<AppUser>().WithMany().HasForeignKey(o => o.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne<AppUser>().WithMany().HasForeignKey(o => o.PlacedById).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasMany(o => o.Lines).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);

        b.Entity<StockLog>().HasOne<StockItem>().WithMany().HasForeignKey(l => l.StockItemId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Product>().HasMany(p => p.OptionGroups).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);
        b.Entity<OptionGroup>().HasMany(g => g.Options).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);

        b.Entity<Product>().OwnsMany(p => p.Stock, StockLink);
        b.Entity<Option>().OwnsMany(o => o.Stock, StockLink);
    }
}
