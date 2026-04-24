using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using System.Security.Claims;

namespace RideHailingApp.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // Hàm phụ trợ giúp chuyển hướng người dùng đã đăng nhập về đúng trang
        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole(Common.Enums.RoleEnum.Admin.ToString()) || User.IsInRole("Admin"))
                return RedirectToAction("Index", "Admin");

            if (User.IsInRole(Common.Enums.RoleEnum.Driver.ToString()) || User.IsInRole("Driver"))
                return RedirectToAction("Index", "Driver");

            return RedirectToAction("Index", "Customer");
        }

        // --- ĐĂNG KÝ ---
        [HttpGet]
        public IActionResult Register()
        {
            // NẾU ĐÃ ĐĂNG NHẬP -> ĐẨY VỀ TRANG CHỦ
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterDTO model)
        {
            if (ModelState.IsValid)
            {
                bool isSuccess = _authService.Register(model);
                if (isSuccess)
                {
                    TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
                ModelState.AddModelError("", "Số điện thoại đã tồn tại trong hệ thống.");
            }
            return View(model);
        }

        // --- ĐĂNG NHẬP ---
        [HttpGet]
        public IActionResult Login()
        {
            // NẾU ĐÃ ĐĂNG NHẬP -> ĐẨY VỀ TRANG CHỦ
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDTO model)
        {
            if (ModelState.IsValid)
            {
                var user = _authService.Login(model);
                if (user != null)
                {
                    // Tạo thông tin định danh cho Cookie (Claims)
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.FullName),
                        new Claim(ClaimTypes.Role, user.Role.ToString())
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    // Đăng nhập và lưu Cookie
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    // Phân luồng trang chủ dựa theo Role
                    if (user.Role == Common.Enums.RoleEnum.Admin) return RedirectToAction("Index", "Admin");
                    if (user.Role == Common.Enums.RoleEnum.Driver) return RedirectToAction("Index", "Driver");
                    return RedirectToAction("Index", "Customer");
                }
                ModelState.AddModelError("", "Số điện thoại hoặc mật khẩu không đúng.");
            }
            return View(model);
        }

        // --- ĐĂNG XUẤT ---
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}