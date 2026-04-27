using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RideHailingApp.Common.Enums;

namespace RideHailingApp.DAL.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; }

        public RoleEnum Role { get; set; }

        public bool? IsDriverAvailable { get; set; }
        public decimal? CurrentLat { get; set; }
        public decimal? CurrentLng { get; set; }

        public bool IsDeleted { get; set; } = false;

        [InverseProperty("Customer")]
        public virtual ICollection<Ride> CustomerRides { get; set; }

        [InverseProperty("Driver")]
        public virtual ICollection<Ride> DriverRides { get; set; }

        public bool IsBanned { get; set; } = false; // Phục vụ tính năng Khóa tài khoản
        public string? LicensePlate { get; set; } // Biển số xe
        public string? VehicleType { get; set; }  // Loại xe
    }
}