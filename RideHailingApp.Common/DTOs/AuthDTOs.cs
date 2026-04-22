using System.ComponentModel.DataAnnotations;
using RideHailingApp.Common.Enums;

namespace RideHailingApp.Common.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; }

        public RoleEnum Role { get; set; } = RoleEnum.Customer; // Mặc định là Khách hàng
    }

    public class LoginDTO
    {
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        public string Password { get; set; }
    }
}