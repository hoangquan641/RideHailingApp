using Microsoft.EntityFrameworkCore;
using RideHailingApp.Common.DTOs;
using RideHailingApp.Common.Enums;
using RideHailingApp.DAL.Data;

namespace RideHailingApp.BLL.Services
{
    public interface IAdminService
    {
        AdminDashboardDTO GetDashboardData();
    }

    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;

        public AdminService(ApplicationDbContext context)
        {
            _context = context;
        }

        public AdminDashboardDTO GetDashboardData()
        {
            var dto = new AdminDashboardDTO();

            // Đếm số lượng User
            dto.TotalCustomers = _context.Users.Count(u => u.Role == RoleEnum.Customer && !u.IsDeleted);
            dto.TotalDrivers = _context.Users.Count(u => u.Role == RoleEnum.Driver && !u.IsDeleted);

            // Tính tổng doanh thu và số chuyến hoàn thành
            var completedRides = _context.Rides
                .Where(r => r.Status == RideStatusEnum.Completed && !r.IsDeleted)
                .ToList();

            dto.TotalCompletedRides = completedRides.Count;
            dto.TotalRevenue = completedRides.Sum(r => r.Fare);

            // Xử lý dữ liệu biểu đồ: Doanh thu 7 ngày qua
            var last7Days = DateTime.Today.AddDays(-6);

            var revenueByDay = completedRides
                .Where(r => r.CreatedAt.Date >= last7Days)
                .GroupBy(r => r.CreatedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Fare));

            // Đảm bảo ngày nào không có cuốc vẫn hiển thị số 0
            for (int i = 0; i < 7; i++)
            {
                var date = last7Days.AddDays(i);
                dto.ChartLabels.Add(date.ToString("dd/MM"));

                if (revenueByDay.ContainsKey(date))
                    dto.ChartData.Add(revenueByDay[date]);
                else
                    dto.ChartData.Add(0);
            }

            return dto;
        }
    }
}