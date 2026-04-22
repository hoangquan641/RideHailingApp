using System.ComponentModel.DataAnnotations;

namespace RideHailingApp.Common.DTOs
{
    public class BookRideDTO
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm đón")]
        public string PickupAddress { get; set; }
        public decimal PickupLat { get; set; }
        public decimal PickupLng { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm đến")]
        public string DropoffAddress { get; set; }
        public decimal DropoffLat { get; set; }
        public decimal DropoffLng { get; set; }
    }
}