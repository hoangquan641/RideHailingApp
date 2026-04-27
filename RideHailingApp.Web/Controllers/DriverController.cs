using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RideHailingApp.BLL.Services;
using RideHailingApp.DAL.Data;
using RideHailingApp.Web.Hubs;
using System.Security.Claims;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RideHailingApp.Web.Controllers
{
    // Phân quyền: Chỉ User có Role là Driver mới được truy cập
    [Authorize(Roles = "Driver")]
    public class DriverController : Controller
    {
        private readonly IRideService _rideService;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<RideHub> _hubContext; // Hub SignalR
        private readonly IProfileService _profileService;

        public DriverController(IRideService rideService, ApplicationDbContext context, IHubContext<RideHub> hubContext, IProfileService profileService)
        {
            _rideService = rideService;
            _context = context;
            _hubContext = hubContext;
            _profileService = profileService;
        }

        // --- MÀN HÌNH CHÍNH (MAP) ---
        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;

            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);

                var driver = _context.Users.Find(driverId);
                ViewBag.IsOnline = driver?.IsDriverAvailable ?? false;

                // Lấy cuốc xe ĐANG CHẠY kèm theo thông tin Khách hàng (Customer)
                var activeRide = _context.Rides
                    .Include(r => r.Customer)
                    .Where(r => r.DriverId == driverId &&
                          (r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted ||
                           r.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress))
                    .FirstOrDefault();

                ViewBag.ActiveRide = activeRide;
            }

            return View();
        }

        // --- LỊCH SỬ CHUYẾN ĐI VÀ THU NHẬP (TÀI XẾ) ---
        [HttpGet]
        public IActionResult RideHistory()
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim == null) return RedirectToAction("Login", "Auth");

            int driverId = int.Parse(driverIdClaim.Value);

            var historyRides = _context.Rides
                .Include(r => r.Customer)
                .Where(r => r.DriverId == driverId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            ViewBag.TotalRevenue = historyRides
                .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed)
                .Sum(r => r.Fare);

            return View(historyRides);
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
            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Profile");
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

        [HttpPost]
        public IActionResult ToggleStatus(bool isOnline)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);
                _rideService.SetDriverAvailability(driverId, isOnline);
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ArrivedAtPickup(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.InProgress;
            _context.SaveChanges();
            await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("DriverArrived", ride.DropoffLat, ride.DropoffLng);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRide(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Completed;
            ride.CompletedAt = DateTime.Now;
            _context.SaveChanges();
            await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideCompleted");
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRide(int rideId)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int driverId = int.Parse(driverIdClaim.Value);
            if (_rideService.AcceptRide(rideId, driverId))
            {
                var ride = _context.Rides.Include(r => r.Driver).First(r => r.Id == rideId);
                await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideAccepted",
                    ride.Driver.FullName, ride.Driver.PhoneNumber, ride.Driver.CurrentLat, ride.Driver.CurrentLng);
                return RedirectToAction("Index");
            }
            return BadRequest();
        }

        // --- CẬP NHẬT VỊ TRÍ REALTIME VÀ TỰ ĐỘNG BẮT CUỐC CHỜ ---
        [HttpPost]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateModel model)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);
                var driver = _context.Users.Find(driverId);
                if (driver != null)
                {
                    driver.CurrentLat = model.Lat;
                    driver.CurrentLng = model.Lng;
                    _context.SaveChanges();

                    // 1. KIỂM TRA TÀI XẾ CÓ ĐANG TRONG CHUYẾN ĐI KHÔNG?
                    var activeRide = _context.Rides.FirstOrDefault(r =>
                        r.DriverId == driverId &&
                        (r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted ||
                         r.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress));

                    if (activeRide != null)
                    {
                        // ĐẨY TỌA ĐỘ TRỰC TIẾP CHO KHÁCH HÀNG THEO DÕI
                        await _hubContext.Clients.User(activeRide.CustomerId.ToString())
                            .SendAsync("UpdateDriverLocation", model.Lat, model.Lng);
                    }
                    // 2. NẾU ĐANG RẢNH -> QUÉT CUỐC MỚI NHƯ CŨ
                    else if (driver.IsDriverAvailable == true)
                    {
                        string searchStr = $"[{driverId}]";

                        var pendingRides = _context.Rides
                            .Include(r => r.Customer)
                            .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Pending &&
                                  (string.IsNullOrEmpty(r.DeclinedDriverIds) || !r.DeclinedDriverIds.Contains(searchStr)))
                            .ToList();

                        var pendingRide = pendingRides
                            .OrderBy(r => Math.Abs((double)(r.PickupLat - model.Lat)) + Math.Abs((double)(r.PickupLng - model.Lng)))
                            .FirstOrDefault();

                        if (pendingRide != null)
                        {
                            await _hubContext.Clients.User(driverId.ToString()).SendAsync(
                                "ReceiveRideRequest",
                                pendingRide.Id,
                                pendingRide.Customer?.FullName ?? "Khách hàng",
                                pendingRide.Customer?.PhoneNumber ?? "",
                                pendingRide.PickupAddress,
                                pendingRide.DropoffAddress,
                                pendingRide.Fare,
                                pendingRide.DistanceKm,
                                pendingRide.PickupLat,
                                pendingRide.PickupLng,
                                pendingRide.DeclinedDriverIds ?? "[]"
                            );
                        }
                    }

                    return Json(new { success = true });
                }
            }
            return Json(new { success = false });
        }

        public class LocationUpdateModel
        {
            public decimal Lat { get; set; }
            public decimal Lng { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> DeclineRide(int rideId, string pickupLat, string pickupLng, string declinedIdsJson)
        {
            var excludedIds = string.IsNullOrWhiteSpace(declinedIdsJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(declinedIdsJson) ?? new List<int>();

            var currentDriverId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            excludedIds.Add(currentDriverId);

            _rideService.MarkRideAsDeclined(rideId, currentDriverId);

            decimal pLat = decimal.Parse(pickupLat);
            decimal pLng = decimal.Parse(pickupLng);
            var nextDriverId = _rideService.FindNearestAvailableDriver(pLat, pLng, excludedIds);

            if (nextDriverId != null)
            {
                var ride = _context.Rides.Include(r => r.Customer).FirstOrDefault(r => r.Id == rideId);

                if (ride != null)
                {
                    await _hubContext.Clients.User(nextDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        ride.Customer.FullName,
                        ride.Customer.PhoneNumber,
                        ride.PickupAddress,
                        ride.DropoffAddress,
                        ride.Fare,
                        ride.DistanceKm,
                        ride.PickupLat,
                        ride.PickupLng,
                        JsonSerializer.Serialize(excludedIds)
                    );
                }
            }

            return Ok();
        }
    }
}