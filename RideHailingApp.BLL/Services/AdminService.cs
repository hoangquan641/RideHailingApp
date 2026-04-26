using Microsoft.EntityFrameworkCore;
using RideHailingApp.Common.DTOs;
using RideHailingApp.Common.Enums;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;

namespace RideHailingApp.BLL.Services
{
    public interface IAdminService
    {
        AdminDashboardDTO GetDashboardData(DateTime? fromDate, DateTime? toDate);
        List<User> GetAllUsers(string roleFilter, string searchString);
        bool ToggleUserBan(int userId);
        bool DeleteUser(int userId);
    }

    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;

        public AdminService(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- BÁO CÁO THỐNG KÊ (UC-ADM-02) ---
        public AdminDashboardDTO GetDashboardData(DateTime? fromDate, DateTime? toDate)
        {
            var dto = new AdminDashboardDTO();

            // Đếm số lượng User (Bỏ qua những user đã bị xóa logic)
            dto.TotalCustomers = _context.Users.Count(u => u.Role == RoleEnum.Customer && !u.IsDeleted);
            dto.TotalDrivers = _context.Users.Count(u => u.Role == RoleEnum.Driver && !u.IsDeleted);

            // Mặc định là tháng hiện tại nếu không chọn ngày
            DateTime startDate = fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime endDate = toDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

            // Chỉ tính toán doanh thu dựa trên những chuyến xe Hoàn thành 
            var completedRides = _context.Rides
                .Where(r => r.Status == RideStatusEnum.Completed && !r.IsDeleted && r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                .ToList();

            dto.TotalCompletedRides = completedRides.Count;
            dto.TotalRevenue = completedRides.Sum(r => r.Fare);

            // Xử lý dữ liệu biểu đồ (Nhóm theo ngày)
            var revenueByDay = completedRides
                .GroupBy(r => r.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Fare));

            foreach (var item in revenueByDay)
            {
                dto.ChartLabels.Add(item.Key.ToString("dd/MM"));
                dto.ChartData.Add(item.Value);
            }

            return dto;
        }

        // --- QUẢN LÝ NGƯỜI DÙNG (UC-ADM-01) ---
        public List<User> GetAllUsers(string roleFilter, string searchString)
        {
            var query = _context.Users.Where(u => !u.IsDeleted && u.Role != RoleEnum.Admin).AsQueryable();

            if (!string.IsNullOrEmpty(roleFilter) && Enum.TryParse(roleFilter, out RoleEnum role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(u => u.FullName.Contains(searchString) || u.PhoneNumber.Contains(searchString));
            }

            return query.OrderByDescending(u => u.Id).ToList();
        }

        public bool ToggleUserBan(int userId)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.IsBanned = !user.IsBanned; // Đảo trạng thái Khóa/Mở khóa
                _context.SaveChanges();
                return true;
            }
            return false;
        }

        public bool DeleteUser(int userId)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.IsDeleted = true; // Thực hiện Xóa logic (Soft Delete) 
                _context.SaveChanges();
                return true;
            }
            return false;
        }
    }
}