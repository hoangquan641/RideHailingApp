using Microsoft.EntityFrameworkCore; // Bổ sung để dùng được .Include()
using RideHailingApp.Common.DTOs;
using RideHailingApp.Common.Helpers;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;
using System.Linq;

namespace RideHailingApp.BLL.Services
{
    // Interface giúp triển khai Dependency Injection linh hoạt
    public interface IAuthService
    {
        bool Register(RegisterDTO model);
        User Login(LoginDTO model);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;

        // Tiêm DbContext từ DAL vào BLL
        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool Register(RegisterDTO model)
        {
            // Kiểm tra xem số điện thoại HOẶC email đã tồn tại chưa
            if (_context.Users.Any(u => u.PhoneNumber == model.PhoneNumber || u.Email == model.Email))
            {
                return false;
            }

            // Tạo User với các thông tin cốt lõi
            var user = new User
            {
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                FullName = model.FullName,
                Role = model.Role
            };

            // 1. Tự động cấp cho mọi User một chiếc Ví rỗng
            user.Wallet = new UserWallet { CashBalance = 0, CreditBalance = 0 };

            // 2. Nếu Role là Driver, tạo luôn hồ sơ tài xế rỗng
            if (model.Role == Common.Enums.RoleEnum.Driver)
            {
                user.DriverProfile = new DriverProfile { IsDriverAvailable = false };
            }

            _context.Users.Add(user);
            _context.SaveChanges();
            return true;
        }

        public User Login(LoginDTO model)
        {
            // 1. Băm mật khẩu người dùng nhập vào để so sánh với chuỗi Hash trong DB
            var hash = PasswordHasher.HashPassword(model.Password);

            // BỔ SUNG: Include(u => u.DriverProfile) để lấy kèm hồ sơ tài xế nếu có
            var user = _context.Users
                .Include(u => u.DriverProfile)
                .FirstOrDefault(u =>
                    u.PhoneNumber == model.PhoneNumber &&
                    u.PasswordHash == hash &&
                    !u.IsDeleted);

            // 2. BỔ SUNG LOGIC: Reset trạng thái tài xế về Offline khi đăng nhập thành công
            // Điều này ép tài xế phải chủ động bật "Sẵn sàng" trên App mới nhận được cuốc xe.
            if (user != null && user.Role == Common.Enums.RoleEnum.Driver)
            {
                // Thay vì sửa trực tiếp trên user, ta sửa trên DriverProfile
                if (user.DriverProfile != null)
                {
                    user.DriverProfile.IsDriverAvailable = false;
                    _context.SaveChanges();
                }
            }

            return user;
        }
    }
}