using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideHailingApp.BLL.Services;

namespace RideHailingApp.Web.Controllers
{
    // Bắt buộc quyền Admin mới được vào
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

        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;
            var dashboardData = _adminService.GetDashboardData();
            return View(dashboardData);
        }

        [HttpGet]
        public IActionResult Profile()
        {
            ViewBag.UserName = User.Identity.Name;
            ViewBag.RoleName = User.IsInRole("Driver") ? "Tài xế đối tác"
                             : User.IsInRole("Admin") ? "Quản trị viên"
                             : "Khách hàng thành viên";
            return View();
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        //-------------------------- POST ACTIONS -------------------//

        [HttpPost]
        public IActionResult UpdateInfo(string fullName, string phone)
        {
            // Tương lai: Thêm logic cập nhật tên/sđt vào DB tại đây
            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Profile"); // Trở về trang Profile
        }

        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                return RedirectToAction("ChangePassword");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int userId = int.Parse(userIdClaim.Value);
            bool isSuccess = _profileService.ChangePassword(userId, currentPassword, newPassword);

            if (!isSuccess)
            {
                TempData["Error"] = "Mật khẩu hiện tại không chính xác!";
                return RedirectToAction("ChangePassword");
            }

            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Profile"); // Trở về trang Profile
        }
    }
}