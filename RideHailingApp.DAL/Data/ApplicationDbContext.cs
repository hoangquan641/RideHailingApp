using Microsoft.EntityFrameworkCore;
using RideHailingApp.DAL.Entities;

namespace RideHailingApp.DAL.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Ride> Rides { get; set; }
        public DbSet<DriverProfile> DriverProfiles { get; set; } // THÊM MỚI
        public DbSet<UserWallet> UserWallets { get; set; }       // THÊM MỚI

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình Quan hệ 1-1 (Cascade Delete: Xóa User sẽ xóa luôn Profile và Wallet)
            modelBuilder.Entity<User>()
                .HasOne(u => u.DriverProfile).WithOne(d => d.User)
                .HasForeignKey<DriverProfile>(d => d.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Wallet).WithOne(w => w.User)
                .HasForeignKey<UserWallet>(w => w.UserId).OnDelete(DeleteBehavior.Cascade);

            // Chuyển cấu hình Lat/Lng sang bảng DriverProfile
            modelBuilder.Entity<DriverProfile>().Property(d => d.CurrentLat).HasColumnType("decimal(18,6)");
            modelBuilder.Entity<DriverProfile>().Property(d => d.CurrentLng).HasColumnType("decimal(18,6)");

            // Các cấu hình của Ride
            modelBuilder.Entity<Ride>().Property(r => r.PickupLat).HasColumnType("decimal(18,6)");
            modelBuilder.Entity<Ride>().Property(r => r.PickupLng).HasColumnType("decimal(18,6)");
            modelBuilder.Entity<Ride>().Property(r => r.DropoffLat).HasColumnType("decimal(18,6)");
            modelBuilder.Entity<Ride>().Property(r => r.DropoffLng).HasColumnType("decimal(18,6)");

            modelBuilder.Entity<Ride>().Property(r => r.Fare).HasColumnType("decimal(18,0)");
            modelBuilder.Entity<Ride>().Property(r => r.DistanceKm).HasColumnType("decimal(10,2)");

            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Ride>().HasQueryFilter(r => !r.IsDeleted);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.Customer)
                .WithMany(u => u.CustomerRides)
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.Driver)
                .WithMany(u => u.DriverRides)
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}