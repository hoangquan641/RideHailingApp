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

namespace RideHailingApp.Web.Controllers
{
    // Phân quyền: Chỉ User có Role là Driver mới được truy cập
    [Authorize(Roles = "Driver")]
    public class DriverController : Controller
    {
        private readonly IRideService _rideService;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<RideHub> _hubContext; // Hub SignalR

        public DriverController(IRideService rideService, ApplicationDbContext context, IHubContext<RideHub> hubContext)
        {
            _rideService = rideService;
            _context = context;
            _hubContext = hubContext;
        }

        // --- 1. MÀN HÌNH CHÍNH (MAP) ---
        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;

            // 1. Lấy ID tài xế hiện tại
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);

                // 2. Lấy cuốc xe ĐANG CHẠY (Accepted: Đang đi đón, InProgress: Đang chở khách)
                var activeRide = _context.Rides
                    .Where(r => r.DriverId == driverId &&
                          (r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted ||
                           r.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress))
                    .FirstOrDefault();

                // 3. Truyền cuốc xe đang chạy xuống View để vẽ bản đồ
                ViewBag.ActiveRide = activeRide;
            }

            return View();
        }

        // --- 2. XÁC NHẬN ĐÃ TỚI ĐIỂM ĐÓN (BẮT ĐẦU CHỞ KHÁCH) ---
        [HttpPost]
        public IActionResult ArrivedAtPickup(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            if (ride != null && ride.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted)
            {
                // Đổi trạng thái sang "Đang di chuyển"
                ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.InProgress;
                _context.SaveChanges();
                TempData["Success"] = "Đã đón khách, bắt đầu di chuyển tới điểm đến!";
            }
            return RedirectToAction("Index"); // Tải lại trang để cập nhật UI bản đồ
        }

        // --- 3. XÁC NHẬN HOÀN THÀNH CHUYẾN ĐI ---
        [HttpPost]
        public IActionResult CompleteRide(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            if (ride != null && ride.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress)
            {
                // Đổi trạng thái sang "Hoàn thành"
                ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Completed;
                ride.CompletedAt = DateTime.Now;
                _context.SaveChanges();
                TempData["Success"] = "Chuyến đi đã hoàn thành! Hệ thống đang dò tìm cuốc mới.";
            }
            return RedirectToAction("Index");
        }

        // --- 4. NHẬN CUỐC XE ---
        [HttpPost]
        public IActionResult AcceptRide(int rideId)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);
                bool isSuccess = _rideService.AcceptRide(rideId, driverId);

                if (isSuccess)
                {
                    TempData["Success"] = "Nhận cuốc thành công! Hãy di chuyển đến điểm đón.";
                }
                else
                {
                    TempData["Error"] = "Rất tiếc, cuốc xe này không còn khả dụng.";
                }
            }

            return RedirectToAction("Index");
        }

        // --- 5. CẬP NHẬT VỊ TRÍ REALTIME ---
        [HttpPost]
        public IActionResult UpdateLocation([FromBody] LocationUpdateModel model)
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
                    driver.IsDriverAvailable = true; // Bật cờ rảnh rỗi
                    _context.SaveChanges();
                    return Json(new { success = true });
                }
            }
            return Json(new { success = false });
        }

        // Model phụ trợ cho UpdateLocation
        public class LocationUpdateModel
        {
            public decimal Lat { get; set; }
            public decimal Lng { get; set; }
        }

        // --- 6. TỪ CHỐI CUỐC XE ---
        [HttpPost]
        public async Task<IActionResult> DeclineRide(int rideId, string pickupLat, string pickupLng, string declinedIdsJson)
        {
            var excludedIds = JsonSerializer.Deserialize<List<int>>(declinedIdsJson) ?? new List<int>();

            var currentDriverId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            excludedIds.Add(currentDriverId);

            _rideService.MarkRideAsDeclined(rideId, currentDriverId);

            decimal pLat = decimal.Parse(pickupLat);
            decimal pLng = decimal.Parse(pickupLng);
            var nextDriverId = _rideService.FindNearestAvailableDriver(pLat, pLng, excludedIds);

            if (nextDriverId != null)
            {
                // BỔ SUNG .Include(r => r.Customer) để lấy được tên khách hàng
                var ride = _context.Rides.Include(r => r.Customer).FirstOrDefault(r => r.Id == rideId);

                if (ride != null)
                {
                    // Bắn SignalR cho tài xế tiếp theo kèm TÊN KHÁCH HÀNG (Tham số thứ 2)
                    await _hubContext.Clients.User(nextDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        ride.Customer.FullName, // Truyền thêm Tên khách
                        ride.PickupAddress,
                        ride.DropoffAddress,
                        ride.Fare,
                        ride.DistanceKm, // Quãng đường của cuốc xe
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