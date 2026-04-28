using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RideHailingApp.DAL.Entities
{
    public class DriverProfile
    {
        [Key, ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }

        public bool IsDriverAvailable { get; set; } = false;
        public decimal? CurrentLat { get; set; }
        public decimal? CurrentLng { get; set; }

        [MaxLength(20)]
        public string? LicensePlate { get; set; }
        [MaxLength(50)]
        public string? VehicleType { get; set; }
    }
}   