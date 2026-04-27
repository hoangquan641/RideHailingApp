using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using RideHailingApp.Common.Enums;
using RideHailingApp.DAL.Data;
using RideHailingApp.Web.Hubs;
using System.Security.Claims;

namespace RideHailingApp.Web.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly IRideService _rideService;
        private readonly IHubContext<RideHub> _hubContext;
        private readonly ApplicationDbContext _context;
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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                int customerId = int.Parse(userIdClaim.Value);
                var activeRide = _context.Rides
                    .Include(r => r.Driver)
                    .Where(r => r.CustomerId == customerId && r.Status != RideHailingApp.Common.Enums.RideStatusEnum.Completed && r.Status != RideHailingApp.Common.Enums.RideStatusEnum.Cancelled)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                ViewBag.ActiveRide = activeRide;
            }
            return View();
        }

        [HttpGet]
        public IActionResult RideHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int customerId = int.Parse(userIdClaim.Value);

            var historyRides = _context.Rides
                .Include(r => r.Driver)
                .Where(r => r.CustomerId == customerId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(historyRides);
        }

        [HttpGet]
        public IActionResult BookRide()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                int customerId = int.Parse(userIdClaim.Value);

                var activeRide = _context.Rides.FirstOrDefault(r =>
                    r.CustomerId == customerId &&
                    (r.Status == RideStatusEnum.Pending ||
                     r.Status == RideStatusEnum.Accepted ||
                     r.Status == RideStatusEnum.InProgress));

                if (activeRide != null)
                {
                    return RedirectToAction("Index");
                }
            }

            return View(new BookRideDTO());
        }

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
                AvatarUrl = user.AvatarUrl
            };

            return View(model);
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

            var customer = _context.Users.Find(model.CustomerId);
            string customerName = customer != null ? customer.FullName : "Khách hàng";
            string customerPhone = customer != null ? customer.PhoneNumber : "Đang cập nhật";

            if (ModelState.IsValid)
            {
                decimal distance = RideHailingApp.BLL.Algorithms.GeoCalculator.CalculateDistance(
                    model.PickupLat, model.PickupLng,
                    model.DropoffLat, model.DropoffLng);

                if (distance < 1m)
                {
                    TempData["Error"] = "Khoảng cách quá ngắn (Tối thiểu 1 km). Vui lòng chọn lại điểm đến.";
                    return RedirectToAction("BookRide");
                }
                if (distance > 50m)
                {
                    TempData["Error"] = "Khoảng cách quá xa (Tối đa 50 km). Vui lòng chọn lại điểm đến.";
                    return RedirectToAction("BookRide");
                }

                var ride = _rideService.BookRide(model);
                var nearestDriverId = _rideService.FindNearestAvailableDriver(model.PickupLat, model.PickupLng, new List<int>());

                if (nearestDriverId != null)
                {
                    await _hubContext.Clients.User(nearestDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        customerName,
                        customerPhone,
                        ride.PickupAddress,
                        ride.DropoffAddress,
                        ride.Fare,
                        ride.DistanceKm,
                        ride.PickupLat,
                        ride.PickupLng,
                        "[]"
                    );
                }

                TempData["Success"] = "Đang kết nối với tài xế gần nhất...";
                return RedirectToAction("Index");
            }

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
            return RedirectToAction("Profile");
        }

        // --- HỦY CHUYẾN XE (ĐÃ CẬP NHẬT NHÁNH 4.2) ---
        [HttpPost]
        public async Task<IActionResult> CancelRide(int rideId, string cancelReason)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                int customerId = int.Parse(userIdClaim.Value);

                var ride = _context.Rides.Include(r => r.Driver)
                    .FirstOrDefault(r => r.Id == rideId && r.CustomerId == customerId);

                // Chỉ cho phép hủy khi chưa hoàn thành hoặc chưa bị hủy
                if (ride != null &&
                    ride.Status != RideHailingApp.Common.Enums.RideStatusEnum.Completed &&
                    ride.Status != RideHailingApp.Common.Enums.RideStatusEnum.Cancelled)
                {
                    // 1. Cập nhật trạng thái và Lưu lý do
                    ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Cancelled;
                    ride.CancelReason = cancelReason;

                    // 2. Nếu đã có tài xế nhận, giải phóng tài xế và gửi thông báo
                    if (ride.DriverId.HasValue)
                    {
                        var driver = _context.Users.Find(ride.DriverId.Value);
                        if (driver != null)
                        {
                            driver.IsDriverAvailable = true; // Trả tài xế về trạng thái Sẵn sàng
                        }

                        // Bắn SignalR báo cho máy tài xế
                        await _hubContext.Clients.User(ride.DriverId.Value.ToString())
                            .SendAsync("RideCancelledByCustomer");
                    }

                    _context.SaveChanges();
                    TempData["Success"] = "Đã hủy chuyến xe thành công.";
                }
            }
            return RedirectToAction("Index");
        }
    }
}