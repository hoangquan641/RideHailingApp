using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using RideHailingApp.DAL.Data; // Thêm thư viện này
using RideHailingApp.Web.Hubs;
using System.Security.Claims;

namespace RideHailingApp.Web.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly IRideService _rideService;
        private readonly IHubContext<RideHub> _hubContext;
        private readonly ApplicationDbContext _context; // Bổ sung DbContext
        private readonly IProfileService _profileService;

        public CustomerController(IRideService rideService, IHubContext<RideHub> hubContext, ApplicationDbContext context, IProfileService profileService)
        {
            _rideService = rideService;
            _hubContext = hubContext;
            _context = context;
            _profileService = profileService;
        }

        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;
            return View();
        }

        [HttpGet]
        public IActionResult BookRide()
        {
            return View(new BookRideDTO());
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
        public async Task<IActionResult> BookRide(BookRideDTO model)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null) model.CustomerId = int.Parse(userIdClaim.Value);

            string customerName = User.Identity.Name ?? "Khách hàng";

            // Lấy số điện thoại từ Database
            var customer = _context.Users.Find(model.CustomerId);
            string customerPhone = customer != null ? customer.PhoneNumber : "Đang cập nhật";

            if (ModelState.IsValid)
            {
                var ride = _rideService.BookRide(model);
                var nearestDriverId = _rideService.FindNearestAvailableDriver(model.PickupLat, model.PickupLng, new List<int>());

                if (nearestDriverId != null)
                {
                    // Đẩy SignalR với 10 tham số (Thêm customerPhone vào vị trí số 3)
                    await _hubContext.Clients.User(nearestDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        customerName,
                        customerPhone,     // <--- SỐ ĐIỆN THOẠI
                        ride.PickupAddress,
                        ride.DropoffAddress,
                        ride.Fare,
                        ride.DistanceKm,
                        ride.PickupLat,
                        ride.PickupLng,
                        "[]"
                    );

                    TempData["Success"] = "Đang chờ tài xế gần nhất xác nhận...";
                }
                else
                {
                    TempData["Error"] = "Hiện tại không có tài xế nào ở gần bạn. Vui lòng thử lại sau.";
                }

                return RedirectToAction("Index");
            }
            return View(model);
        }

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