using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using RideHailingApp.DAL.Data;
using RideHailingApp.Web.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
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

                var driverProfile = _context.DriverProfiles.FirstOrDefault(d => d.UserId == driverId);
                ViewBag.IsOnline = driverProfile?.IsDriverAvailable ?? false;

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
            ViewBag.RoleName = "Tài xế đối tác";

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int userId = int.Parse(userIdClaim.Value);
            var user = _context.Users.Find(userId);
            var driverProfile = _context.DriverProfiles.FirstOrDefault(d => d.UserId == userId);

            if (user == null) return RedirectToAction("Login", "Auth");

            // Tạo đối tượng Model chứa đầy đủ thông tin để ném ra Giao diện (View)
            var model = new RideHailingApp.Common.DTOs.UpdateProfileDTO
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,

                // Load thông tin xe từ DriverProfile
                LicensePlate = driverProfile?.LicensePlate,
                VehicleType = driverProfile?.VehicleType
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // --- TÍNH NĂNG 1: VÍ VÀ DOANH THU ---
        [HttpGet]
        // --- TÍNH NĂNG 1: VÍ VÀ DOANH THU ---
        [HttpGet]
        public IActionResult Wallet()
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim == null) return RedirectToAction("Login", "Auth");

            int driverId = int.Parse(driverIdClaim.Value);

            // 1. Phải có Include(u => u.Wallet) để Entity Framework lấy kèm dữ liệu Ví
            var driver = _context.Users
                .Include(u => u.Wallet)
                .FirstOrDefault(u => u.Id == driverId);

            // 2. Nếu DB bị reset mà Cookie vẫn còn (driver = null), tự động đẩy ra trang Đăng xuất
            if (driver == null)
            {
                return RedirectToAction("Logout", "Auth");
            }

            // Lấy cuốc xe hoàn thành trong 7 ngày qua để vẽ biểu đồ
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
            var recentRides = _context.Rides
                .Where(r => r.DriverId == driverId && r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed && r.CompletedAt >= sevenDaysAgo)
                .ToList();

            var chartLabels = new List<string>();
            var chartData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.Date.AddDays(-i);
                chartLabels.Add(date.ToString("dd/MM"));
                chartData.Add(recentRides.Where(r => r.CompletedAt?.Date == date).Sum(r => r.Fare));
            }

            ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(chartLabels);
            ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(chartData);
            ViewBag.TodayRevenue = recentRides.Where(r => r.CompletedAt?.Date == DateTime.Now.Date).Sum(r => r.Fare);

            return View(driver);
        }

        // --- TÍNH NĂNG 2: NHIỆM VỤ VÀ CẢNH BÁO ---
        [HttpGet]
        public IActionResult Notifications()
        {
            var driverId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 1. Tính tiến độ nhiệm vụ (Số cuốc hoàn thành hôm nay)
            int todayCompleted = _context.Rides.Count(r =>
                r.DriverId == driverId &&
                r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed &&
                r.CompletedAt != null && r.CompletedAt.Value.Date == DateTime.Now.Date);

            ViewBag.QuestProgress = todayCompleted;

            // 2. Tính tỷ lệ hủy (Cảnh báo)
            int totalAssigned = _context.Rides.Count(r => r.DriverId == driverId);
            int totalCancelled = _context.Rides.Count(r => r.DriverId == driverId && r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Cancelled && r.CancelReason != null);

            double cancelRate = totalAssigned > 0 ? ((double)totalCancelled / totalAssigned) * 100 : 0;
            ViewBag.CancelRate = Math.Round(cancelRate, 1);

            return View();
        }

        //-------------------------- POST ACTIONS -------------------//
        [HttpPost]
        public async Task<IActionResult> UpdateInfo(RideHailingApp.Common.DTOs.UpdateProfileDTO model, Microsoft.AspNetCore.Http.IFormFile? avatarFile)
        {
            if (ModelState.IsValid)
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    int userId = int.Parse(userIdClaim.Value);

                    // XỬ LÝ LƯU FILE ẢNH
                    if (avatarFile != null)
                    {
                        string folder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                        if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                        string fileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(avatarFile.FileName);
                        string filePath = System.IO.Path.Combine(folder, fileName);

                        using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
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

        // NÂNG CẤP: Cập nhật doanh thu vào ví khi hoàn thành chuyến đi
        [HttpPost]
        public async Task<IActionResult> CompleteRide(int rideId)
        {
            var ride = _context.Rides
                .Include(r => r.Driver).ThenInclude(d => d.Wallet)
                .Include(r => r.Driver).ThenInclude(d => d.DriverProfile)
                .FirstOrDefault(r => r.Id == rideId);

            if (ride != null && ride.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress)
            {
                // 1. Cập nhật trạng thái chuyến xe
                ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Completed;
                ride.CompletedAt = DateTime.Now;

                // 2. CỘNG TIỀN CƯỚC VÀO VÍ TÀI XẾ
                if (ride.Driver != null)
                {
                    ride.Driver.Wallet.CashBalance += ride.Fare;
                    ride.Driver.DriverProfile.IsDriverAvailable = true;
                }

                // Lưu lần 1 để DB ghi nhận chuyến này đã hoàn thành
                _context.SaveChanges();

                // 3. LOGIC NHIỆM VỤ: Kiểm tra số cuốc hoàn thành trong ngày
                int todayCompletedRides = _context.Rides.Count(r =>
                    r.DriverId == ride.DriverId &&
                    r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Completed &&
                    r.CompletedAt != null && r.CompletedAt.Value.Date == DateTime.Now.Date);

                string successMessage = $"Chúc mừng! Bạn đã nhận được {ride.Fare.ToString("N0")}đ vào ví.";

                // Nếu VỪA ĐÚNG đạt mốc 10 cuốc, cộng thêm 50.000đ tiền thưởng
                if (todayCompletedRides == 10 && ride.Driver != null)
                {
                    ride.Driver.Wallet.CashBalance += 50000m;
                    _context.SaveChanges(); // Lưu lần 2 cho tiền thưởng

                    successMessage = $"Tuyệt vời! Bạn nhận được {ride.Fare.ToString("N0")}đ cước phí và THƯỞNG 50.000đ (Hoàn thành 10 cuốc)!";
                }

                // 4. Bắn SignalR thông báo cho máy Khách hàng
                await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideCompleted");

                TempData["Success"] = successMessage;
                return RedirectToAction("Index");
            }
            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRide(int rideId)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int driverId = int.Parse(driverIdClaim.Value);
            if (_rideService.AcceptRide(rideId, driverId))
            {
                var ride = _context.Rides.Include(r => r.Driver).ThenInclude(d => d.DriverProfile).First(r => r.Id == rideId);

                // BỔ SUNG: Truyền thêm Biển số và Loại xe
                await _hubContext.Clients.User(ride.CustomerId.ToString()).SendAsync("RideAccepted",
                    ride.Driver.FullName,
                    ride.Driver.PhoneNumber,
                    ride.Driver.DriverProfile?.CurrentLat,
                    ride.Driver.DriverProfile?.CurrentLng,
                    ride.Driver.DriverProfile?.LicensePlate ?? "Đang cập nhật",
                    ride.Driver.DriverProfile?.VehicleType ?? "Car");

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
                var driverProfile = _context.DriverProfiles.FirstOrDefault(d => d.UserId == driverId);

                if (driverProfile != null)
                {
                    driverProfile.CurrentLat = model.Lat;
                    driverProfile.CurrentLng = model.Lng;
                    _context.SaveChanges();

                    // KIỂM TRA TÀI XẾ CÓ ĐANG TRONG CHUYẾN ĐI KHÔNG?
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
                    else if (driverProfile.IsDriverAvailable == true)
                    {
                        string searchStr = $"[{driverId}]";

                        var pendingRides = _context.Rides
                            .Include(r => r.Customer)
                            .Where(r => r.Status == RideHailingApp.Common.Enums.RideStatusEnum.Pending &&
                                  r.RequestedVehicleType == driverProfile.VehicleType && // TÀI XẾ CHỈ THẤY CUỐC KHỚP XE CỦA MÌNH
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

            var ride = _context.Rides.Include(r => r.Customer).FirstOrDefault(r => r.Id == rideId);
            var nextDriverId = _rideService.FindNearestAvailableDriver(pLat, pLng, excludedIds, ride?.RequestedVehicleType);

            if (nextDriverId != null)
            {
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

        // --- TÀI XẾ CHỦ ĐỘNG HỦY CHUYẾN ---
        [HttpPost]
        public async Task<IActionResult> CancelRide(int rideId, string cancelReason)
        {
            var driverIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (driverIdClaim != null)
            {
                int driverId = int.Parse(driverIdClaim.Value);

                // Lấy chuyến xe mà tài xế này đang nhận
                var ride = _context.Rides
                    .Include(r => r.Driver).ThenInclude(d => d.Wallet)
                    .Include(r => r.Driver).ThenInclude(d => d.DriverProfile)
                    .FirstOrDefault(r => r.Id == rideId && r.DriverId == driverId);

                // Cho phép hủy khi đang ở trạng thái Accepted (Đang đến đón) hoặc InProgress
                if (ride != null &&
                   (ride.Status == RideHailingApp.Common.Enums.RideStatusEnum.Accepted ||
                    ride.Status == RideHailingApp.Common.Enums.RideStatusEnum.InProgress))
                {
                    // 1. Cập nhật trạng thái chuyến xe và lý do
                    ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Cancelled;
                    ride.CancelReason = cancelReason;

                    // 2. Trả tài xế về trạng thái Sẵn sàng nhận cuốc mới
                    if (ride.Driver != null && ride.Driver.DriverProfile != null)
                    {
                        ride.Driver.DriverProfile.IsDriverAvailable = true;
                    }

                    _context.SaveChanges();

                    // 3. Bắn SignalR thông báo cho máy Khách hàng biết tài xế đã hủy
                    await _hubContext.Clients.User(ride.CustomerId.ToString())
                        .SendAsync("RideCancelledByDriver", cancelReason);

                    TempData["Success"] = "Đã hủy chuyến thành công.";
                }
            }
            return RedirectToAction("Index");
        }
    }
}