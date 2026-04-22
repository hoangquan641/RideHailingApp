using Microsoft.EntityFrameworkCore;
using RideHailingApp.DAL.Entities;

namespace RideHailingApp.DAL.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Ride> Rides { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().Property(u => u.CurrentLat).HasColumnType("decimal(18,6)");
            modelBuilder.Entity<User>().Property(u => u.CurrentLng).HasColumnType("decimal(18,6)");

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