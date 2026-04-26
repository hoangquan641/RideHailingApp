using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RideHailingApp.Common.Enums;

namespace RideHailingApp.DAL.Entities
{
    public class Ride
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual User Customer { get; set; }

        public int? DriverId { get; set; }
        [ForeignKey("DriverId")]
        public virtual User Driver { get; set; }

        [Required, MaxLength(255)]
        public string PickupAddress { get; set; }
        public decimal PickupLat { get; set; }
        public decimal PickupLng { get; set; }

        [Required, MaxLength(255)]
        public string DropoffAddress { get; set; }
        public decimal DropoffLat { get; set; }
        public decimal DropoffLng { get; set; }

        public decimal DistanceKm { get; set; }
        public decimal Fare { get; set; }

        public RideStatusEnum Status { get; set; } = RideStatusEnum.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public string DeclinedDriverIds { get; set; } = ""; // Lưu ID tài xế từ chối

        [MaxLength(500)]
        public string CancelReason { get; set; } = ""; // Lưu lý do hủy chuyến
    }
}