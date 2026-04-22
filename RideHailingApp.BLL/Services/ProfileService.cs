using RideHailingApp.Common.Helpers;
using RideHailingApp.DAL.Data;

namespace RideHailingApp.BLL.Services
{
    public interface IProfileService
    {
        bool ChangePassword(int userId, string currentPassword, string newPassword);
    }

    public class ProfileService : IProfileService
    {
        private readonly ApplicationDbContext _context;

        public ProfileService(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            // 1. Tìm user trong Database theo ID
            var user = _context.Users.Find(userId);
            if (user == null) return false;

            // 2. Mã hóa mật khẩu cũ người dùng nhập vào để so sánh với Hash trong DB
            var oldHash = PasswordHasher.HashPassword(currentPassword);
            if (user.PasswordHash != oldHash)
            {
                return false; // Sai mật khẩu hiện tại
            }

            // 3. Mã hóa mật khẩu mới và cập nhật
            user.PasswordHash = PasswordHasher.HashPassword(newPassword);

            _context.SaveChanges();
            return true;
        }
    }
}