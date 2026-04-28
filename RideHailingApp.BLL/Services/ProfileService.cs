using Microsoft.EntityFrameworkCore;
using RideHailingApp.Common.Helpers;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;

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
            // BỔ SUNG .Include(u => u.DriverProfile)
            var user = _context.Users.Include(u => u.DriverProfile).FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.FullName = model.FullName;
            user.Email = model.Email;

            if (!string.IsNullOrEmpty(model.AvatarUrl)) user.AvatarUrl = model.AvatarUrl;

            // Xử lý riêng cho Tài xế
            if (user.Role == RideHailingApp.Common.Enums.RoleEnum.Driver)
            {
                // Phòng hờ tài xế cũ chưa có profile
                if (user.DriverProfile == null)
                {
                    user.DriverProfile = new DriverProfile { UserId = userId };
                    _context.DriverProfiles.Add(user.DriverProfile);
                }

                if (!string.IsNullOrEmpty(model.LicensePlate)) user.DriverProfile.LicensePlate = model.LicensePlate;
                if (!string.IsNullOrEmpty(model.VehicleType)) user.DriverProfile.VehicleType = model.VehicleType;
            }

            _context.SaveChanges();
            return true;
        }
    }
}