using System.ComponentModel.DataAnnotations;

namespace RideHailingApp.Common.DTOs
{
    public class UpdateProfileDTO
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [MaxLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? AvatarUrl { get; set; }
    }
}