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

        [Required, MaxLength(100), EmailAddress]
        public string Email { get; set; }

        public string? AvatarUrl { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; }

        public RoleEnum Role { get; set; }

        public bool IsDeleted { get; set; } = false;
        public bool IsBanned { get; set; } = false;

        [InverseProperty("Customer")]
        public virtual ICollection<Ride> CustomerRides { get; set; }

        [InverseProperty("Driver")]
        public virtual ICollection<Ride> DriverRides { get; set; }

        // BỔ SUNG: Quan hệ 1-1
        public virtual DriverProfile? DriverProfile { get; set; }
        public virtual UserWallet? Wallet { get; set; }
    }
}