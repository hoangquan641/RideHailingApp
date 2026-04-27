using Microsoft.AspNetCore.Mvc;
using RideHailingApp.Web.Models;
using System.Diagnostics;

namespace RideHailingApp.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // 1. Nếu người dùng CHƯA đăng nhập -> Đẩy về trang Đăng nhập
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            // 2. Nếu ĐÃ đăng nhập -> Điều hướng theo Vai trò (Role)
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            if (User.IsInRole("Driver"))
            {
                return RedirectToAction("Index", "Driver");
            }

            // 3. Mặc định là Khách hàng (Customer)
            return RedirectToAction("Index", "Customer");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}