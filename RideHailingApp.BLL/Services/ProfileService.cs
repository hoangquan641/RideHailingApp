using RideHailingApp.Common.Helpers;
using RideHailingApp.DAL.Data;

namespace RideHailingApp.BLL.Services
{
    public interface IProfileService
    {
        bool ChangePassword(int userId, string currentPassword, string newPassword);

        // Bổ sung hàm cập nhật thông tin
        bool UpdateProfile(int userId, RideHailingApp.Common.DTOs.UpdateProfileDTO model);
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

        public bool UpdateProfile(int userId, RideHailingApp.Common.DTOs.UpdateProfileDTO model)
        {
            var user = _context.Users.Find(userId);
            if (user == null) return false;

            // Chỉ cập nhật các trường cơ bản
            user.FullName = model.FullName;
            user.Email = model.Email;

            // BỔ SUNG: Nếu có upload ảnh mới (AvatarUrl không rỗng) thì mới gán vào DB
            if (!string.IsNullOrEmpty(model.AvatarUrl))
            {
                user.AvatarUrl = model.AvatarUrl;
            }

            _context.SaveChanges();
            return true;
        }
    }
}