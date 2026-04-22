using RideHailingApp.Common.DTOs;
using RideHailingApp.Common.Helpers;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;

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
            // Kiểm tra xem số điện thoại đã tồn tại chưa
            if (_context.Users.Any(u => u.PhoneNumber == model.PhoneNumber))
            {
                return false;
            }

            var user = new User
            {
                PhoneNumber = model.PhoneNumber,
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
            // Băm mật khẩu người dùng nhập vào để so sánh với chuỗi Hash trong DB
            var hash = PasswordHasher.HashPassword(model.Password);

            return _context.Users.FirstOrDefault(u =>
                u.PhoneNumber == model.PhoneNumber &&
                u.PasswordHash == hash &&
                !u.IsDeleted);
        }
    }
}