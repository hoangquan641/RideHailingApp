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

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;
            var dashboardData = _adminService.GetDashboardData();
            return View(dashboardData);
        }
    }
}