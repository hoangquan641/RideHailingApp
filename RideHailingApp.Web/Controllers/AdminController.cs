using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RideHailingApp.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IProfileService _profileService;
        private readonly RideHailingApp.DAL.Data.ApplicationDbContext _context;

        public AdminController(IAdminService adminService, IProfileService profileService, RideHailingApp.DAL.Data.ApplicationDbContext context)
        {
            _adminService = adminService;
            _profileService = profileService;
            _context = context;
        }

        // --- 1. BÁO CÁO THỐNG KÊ (UC-ADM-02) ---
        public IActionResult Index(DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.UserName = User.Identity.Name;

            // Kiểm tra tính hợp lệ của thời gian
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                TempData["Error"] = "Khoảng thời gian không hợp lệ (Từ ngày không được lớn hơn Đến ngày).";
                return View(_adminService.GetDashboardData(null, null));
            }

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            // BỔ SUNG: Lấy dữ liệu cảnh báo từ Service
            ViewBag.RevenueWarning = _adminService.CheckRevenueAnomaly();

            var dashboardData = _adminService.GetDashboardData(fromDate, toDate);
            return View(dashboardData);
        }

        // ==========================================================
        // BỔ SUNG MỚI: TÍNH NĂNG BÁO CÁO & XUẤT EXCEL
        // ==========================================================

        [HttpGet]
        public IActionResult Reports(DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.UserName = User.Identity.Name;

            DateTime start = fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime end = toDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

            ViewBag.FromDate = start.ToString("yyyy-MM-dd");
            ViewBag.ToDate = end.ToString("yyyy-MM-dd");

            // Lấy danh sách chuyến đi đã hoàn thành để báo cáo
            var completedRides = _context.Rides
                .Include(r => r.Driver)
                .Include(r => r.Customer)
                .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed
                         && !r.IsDeleted
                         && r.CompletedAt >= start
                         && r.CompletedAt <= end)
                .OrderByDescending(r => r.CompletedAt)
                .ToList();

            ViewBag.TotalRevenue = completedRides.Sum(r => r.Fare);
            return View(completedRides);
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate)
        {
            DateTime start = fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime end = toDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var completedRides = _context.Rides
                .Include(r => r.Driver)
                .Include(r => r.Customer)
                .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed
                         && !r.IsDeleted
                         && r.CompletedAt >= start
                         && r.CompletedAt <= end)
                .OrderByDescending(r => r.CompletedAt)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BaoCaoDoanhThu");
                var currentRow = 1;

                // Tạo Header
                worksheet.Cell(currentRow, 1).Value = "Mã Chuyến";
                worksheet.Cell(currentRow, 2).Value = "Khách Hàng";
                worksheet.Cell(currentRow, 3).Value = "Tài Xế";
                worksheet.Cell(currentRow, 4).Value = "Thời Gian Hoàn Thành";
                worksheet.Cell(currentRow, 5).Value = "Quãng Đường (km)";
                worksheet.Cell(currentRow, 6).Value = "Cước Phí (VNĐ)";
                worksheet.Row(currentRow).Style.Font.Bold = true;

                // Ghi dữ liệu
                foreach (var ride in completedRides)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = ride.Id;
                    worksheet.Cell(currentRow, 2).Value = ride.Customer?.FullName ?? "N/A";
                    worksheet.Cell(currentRow, 3).Value = ride.Driver?.FullName ?? "N/A";
                    worksheet.Cell(currentRow, 4).Value = ride.CompletedAt?.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 5).Value = ride.DistanceKm;
                    worksheet.Cell(currentRow, 6).Value = ride.Fare;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }

        // --- 2. QUẢN LÝ NGƯỜI DÙNG (UC-ADM-01) ---
        public IActionResult ManageUsers(string roleFilter, string searchString)
        {
            ViewBag.RoleFilter = roleFilter;
            ViewBag.SearchString = searchString;

            var users = _adminService.GetAllUsers(roleFilter, searchString);
            return View(users);
        }

        [HttpPost]
        public IActionResult ToggleUserBan(int userId)
        {
            if (_adminService.ToggleUserBan(userId))
                TempData["Success"] = "Cập nhật trạng thái tài khoản thành công!";
            else
                TempData["Error"] = "Không tìm thấy người dùng.";

            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public IActionResult DeleteUser(int userId)
        {
            if (_adminService.DeleteUser(userId))
                TempData["Success"] = "Đã xóa tài khoản khỏi hệ thống (Soft Delete).";
            else
                TempData["Error"] = "Không tìm thấy người dùng.";

            return RedirectToAction("ManageUsers");
        }

        // --- 3. QUẢN LÝ HỒ SƠ (PROFILE) ---
        [HttpGet]
        public IActionResult Profile()
        {
            ViewBag.UserName = User.Identity.Name;
            ViewBag.RoleName = User.IsInRole("Driver") ? "Tài xế đối tác"
                             : User.IsInRole("Admin") ? "Quản trị viên"
                             : "Khách hàng thành viên";

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int userId = int.Parse(userIdClaim.Value);
            var user = _context.Users.Find(userId);

            if (user == null) return RedirectToAction("Login", "Auth");

            var model = new RideHailingApp.Common.DTOs.UpdateProfileDTO
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl // <--- ĐÂY CHÍNH LÀ ĐIỂM QUAN TRỌNG ĐỂ HIỂN THỊ LẠI ẢNH
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(RideHailingApp.Common.DTOs.UpdateProfileDTO model, IFormFile? avatarFile) // <-- BỔ SUNG Ở ĐÂY
        {
            if (ModelState.IsValid)
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    int userId = int.Parse(userIdClaim.Value);

                    // XỬ LÝ LƯU FILE ẢNH (Dùng avatarFile thay vì model.AvatarFile)
                    if (avatarFile != null)
                    {
                        string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                        string filePath = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await avatarFile.CopyToAsync(stream);
                        }

                        // Gán đường dẫn vào model để lưu xuống Database
                        model.AvatarUrl = "/uploads/avatars/" + fileName;
                    }

                    bool isSuccess = _profileService.UpdateProfile(userId, model);

                    if (isSuccess)
                    {
                        TempData["Success"] = "Cập nhật hồ sơ thành công!";
                        return RedirectToAction("Profile");
                    }
                }
            }
            TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào.";
            return View("Profile", model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                return RedirectToAction("ChangePassword");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int userId = int.Parse(userIdClaim.Value);
            bool isSuccess = _profileService.ChangePassword(userId, currentPassword, newPassword);

            if (!isSuccess)
            {
                TempData["Error"] = "Mật khẩu hiện tại không chính xác!";
                return RedirectToAction("ChangePassword");
            }

            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public IActionResult Notifications()
        {
            ViewBag.UserName = User.Identity.Name;

            // 1. Lấy cảnh báo doanh thu từ Service (Tính năng B đã bàn)
            ViewBag.RevenueWarning = _adminService.CheckRevenueAnomaly();

            // 2. Lấy 5 cuốc xe bị hủy gần nhất để Admin kiểm tra lý do (Phát hiện lỗi hệ thống)
            var recentIssues = _context.Rides
                .Include(r => r.Customer)
                .Include(r => r.Driver)
                .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToList();

            return View(recentIssues);
        }
    }
}