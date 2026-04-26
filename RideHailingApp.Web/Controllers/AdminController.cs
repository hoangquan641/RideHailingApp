using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideHailingApp.BLL.Services;
using System;
using System.Linq;
using System.Security.Claims;

namespace RideHailingApp.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IProfileService _profileService;

        public AdminController(IAdminService adminService, IProfileService profileService)
        {
            _adminService = adminService;
            _profileService = profileService;
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

            var dashboardData = _adminService.GetDashboardData(fromDate, toDate);
            return View(dashboardData);
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
            return View();
        }

        [HttpPost]
        public IActionResult UpdateInfo(string fullName, string phone)
        {
            // Tương lai: Thêm logic cập nhật vào DB qua _profileService tại đây
            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Profile");
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
    }
}