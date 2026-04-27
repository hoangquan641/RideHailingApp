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
        private readonly RideHailingApp.DAL.Data.ApplicationDbContext _context; // BỔ SUNG DÒNG NÀY

        // BỔ SUNG ApplicationDbContext VÀO CONSTRUCTOR
        public AdminController(IAdminService adminService, IProfileService profileService, RideHailingApp.DAL.Data.ApplicationDbContext context)
        {
            _adminService = adminService;
            _profileService = profileService;
            _context = context; // BỔ SUNG DÒNG NÀY
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
    }
}