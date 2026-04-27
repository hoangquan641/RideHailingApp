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
            // Kiểm tra xem số điện thoại HOẶC email đã tồn tại chưa (Xử lý ngoại lệ A2)
            if (_context.Users.Any(u => u.PhoneNumber == model.PhoneNumber || u.Email == model.Email))
            {
                return false;
            }

            var user = new User
            {
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                FullName = model.FullName,
                Role = model.Role,
                IsDriverAvailable = model.Role == Common.Enums.RoleEnum.Driver ? false : null
            };

            _context.Users.Add(user);
            _context.SaveChanges();
            return true;
        }

        public User Login(LoginDTO model)
        {
            // 1. Băm mật khẩu người dùng nhập vào để so sánh với chuỗi Hash trong DB
            var hash = PasswordHasher.HashPassword(model.Password);

            var user = _context.Users.FirstOrDefault(u =>
                u.PhoneNumber == model.PhoneNumber &&
                u.PasswordHash == hash &&
                !u.IsDeleted);

            // 2. BỔ SUNG LOGIC: Reset trạng thái tài xế về Offline khi đăng nhập thành công
            // Điều này ép tài xế phải chủ động bật "Sẵn sàng" trên App mới nhận được cuốc xe.
            if (user != null && user.Role == Common.Enums.RoleEnum.Driver)
            {
                user.IsDriverAvailable = false;
                _context.SaveChanges();
            }

            return user;
        }
    }
}