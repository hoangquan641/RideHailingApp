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
                    .Include(r => r.Customer) // BẮT BUỘC PHẢI THÊM DÒNG NÀY
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

            // Lấy danh sách chuyến đi của tài xế này (Bao gồm thông tin Khách hàng)
            // Chỉ lấy các chuyến tài xế đã nhận (có DriverId)
            var historyRides = _context.Rides
                .Include(r => r.Customer)
                .Where(r => r.DriverId == driverId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            // Tính tổng doanh thu các chuyến đã hoàn thành
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
            return RedirectToAction("Profile"); // Trở về trang Profile
        }

        // --- CẬP NHẬT TRẠNG THÁI ONLINE/OFFLINE (TỪ NÚT BẤM GIAO DIỆN) ---
        [HttpPost]
        public IActionResult ToggleStatus(bool isOnline)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);
                // Gọi xuống BLL để lưu DB thay vì viết logic DB ở Controller
                _rideService.SetDriverAvailability(driverId, isOnline);
            }
            return Ok();
        }

        // --- XÁC NHẬN ĐÃ TỚI ĐIỂM ĐÓN (BẮT ĐẦU CHỞ KHÁCH) ---
        [HttpPost]
        public async Task<IActionResult> ArrivedAtPickup(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.InProgress; // Chuyển sang đang di chuyển
            _context.SaveChanges();
            // Báo cho khách: Bắt đầu hành trình đến điểm đích
            await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("DriverArrived", ride.DropoffLat, ride.DropoffLng);
            return RedirectToAction("Index");
        }

        // --- XÁC NHẬN HOÀN THÀNH CHUYẾN ĐI ---
        [HttpPost]
        public async Task<IActionResult> CompleteRide(int rideId)
        {
            var ride = _context.Rides.Find(rideId);
            ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Completed;
            ride.CompletedAt = DateTime.Now;
            _context.SaveChanges();
            // Báo cho khách: Chuyến đi kết thúc
            await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideCompleted");
            return RedirectToAction("Index");
        }

        // --- NHẬN CUỐC XE ---
        [HttpPost]
        public async Task<IActionResult> AcceptRide(int rideId)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int driverId = int.Parse(driverIdClaim.Value);
            if (_rideService.AcceptRide(rideId, driverId))
            {
                var ride = _context.Rides.Include(r => r.Driver).First(r => r.Id == rideId);
                // Báo cho khách: Tài xế đã nhận
                await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideAccepted",
                    ride.Driver.FullName, ride.Driver.PhoneNumber, ride.Driver.CurrentLat, ride.Driver.CurrentLng);
                return RedirectToAction("Index");
            }
            return BadRequest();
        }

        // --- CẬP NHẬT VỊ TRÍ REALTIME VÀ TỰ ĐỘNG BẮT CUỐC CHỜ ---
        [HttpPost]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateModel model) // Thêm async Task<>
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

                    // MỚI: TỰ ĐỘNG "QUÉT" CUỐC XE KHI TÀI XẾ ĐANG ONLINE
                    if (driver.IsDriverAvailable == true)
                    {
                        // 1. Kiểm tra xem tài xế có đang bận chở khách không
                        bool isBusy = _context.Rides.Any(r => r.DriverId == driverId &&
                            (r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted ||
                             r.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress));

                        if (!isBusy)
                        {
                            // 2. Tìm cuốc Pending gần nhất mà tài xế này CHƯA từng bấm "Bỏ qua"
                            string searchStr = $"[{driverId}]";

                            var pendingRides = _context.Rides
                                .Include(r => r.Customer)
                                .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Pending &&
                                      (string.IsNullOrEmpty(r.DeclinedDriverIds) || !r.DeclinedDriverIds.Contains(searchStr)))
                                .ToList(); // Load vào bộ nhớ để tính khoảng cách cho an toàn

                            var pendingRide = pendingRides
                                .OrderBy(r => Math.Abs((double)(r.PickupLat - model.Lat)) + Math.Abs((double)(r.PickupLng - model.Lng)))
                                .FirstOrDefault();

                            // 3. Nếu tìm thấy, lập tức bắn thẳng SignalR cho chính tài xế này
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
                    }

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

        // --- TỪ CHỐI CUỐC XE ---
        [HttpPost]
        public async Task<IActionResult> DeclineRide(int rideId, string pickupLat, string pickupLng, string declinedIdsJson)
        {
            // FIX LỖI: Kiểm tra an toàn trước khi Deserialize để tránh ArgumentNullException
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
                // BỔ SUNG .Include(r => r.Customer) để lấy được tên khách hàng
                var ride = _context.Rides.Include(r => r.Customer).FirstOrDefault(r => r.Id == rideId);

                if (ride != null)
                {
                    // Bắn SignalR cho tài xế tiếp theo kèm TÊN KHÁCH HÀNG (Tham số thứ 2)
                    await _hubContext.Clients.User(nextDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        ride.Customer.FullName, // Truyền thêm Tên khách
                        ride.Customer.PhoneNumber,
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