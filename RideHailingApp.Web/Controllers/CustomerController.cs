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
                // Lấy cuốc xe mới nhất chưa hoàn thành
                var activeRide = _context.Rides
                    .Include(r => r.Driver)
                    .Where(r => r.CustomerId == customerId && r.Status != RideHailingApp.Common.Enums.RideStatusEnum.Completed && r.Status != RideHailingApp.Common.Enums.RideStatusEnum.Cancelled)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                ViewBag.ActiveRide = activeRide;
            }
            return View();
        }

        // --- LỊCH SỬ CHUYẾN ĐI (KHÁCH HÀNG) ---
        [HttpGet]
        public IActionResult RideHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int customerId = int.Parse(userIdClaim.Value);

            // Lấy danh sách chuyến đi của khách hàng (Bao gồm cả thông tin tài xế)
            var historyRides = _context.Rides
                .Include(r => r.Driver)
                .Where(r => r.CustomerId == customerId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(historyRides);
        }
        // =======================================================
        // ĐÃ SỬA: Chặn không cho mở Form đặt xe nếu đang có cuốc chờ
        // =======================================================
        [HttpGet]
        public IActionResult BookRide()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                int customerId = int.Parse(userIdClaim.Value);

                // KIỂM TRA: Khách có cuốc xe nào ĐANG CHỜ hoặc ĐANG ĐI không?
                var activeRide = _context.Rides.FirstOrDefault(r =>
                    r.CustomerId == customerId &&
                    (r.Status == RideStatusEnum.Pending ||
                     r.Status == RideStatusEnum.Accepted ||
                     r.Status == RideStatusEnum.InProgress));

                // NẾU ĐANG CÓ CUỐC: Đẩy họ sang trang theo dõi (Index)
                if (activeRide != null)
                {
                    return RedirectToAction("Index");
                }
            }

            // NẾU KHÔNG CÓ CUỐC: Cho họ mở Form đặt xe như bình thường
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

            var customer = _context.Users.Find(model.CustomerId);
            string customerName = customer != null ? customer.FullName : "Khách hàng";
            string customerPhone = customer != null ? customer.PhoneNumber : "Đang cập nhật";

            if (ModelState.IsValid)
            {
                // 1. Luôn tạo cuốc xe trong DB với trạng thái Pending
                var ride = _rideService.BookRide(model);

                // 2. Tìm xem có tài xế nào đang rảnh ngay lúc này không
                var nearestDriverId = _rideService.FindNearestAvailableDriver(model.PickupLat, model.PickupLng, new List<int>());

                // 3. Nếu có tài xế rảnh -> Bắn sóng SignalR cho tài xế đó ngay lập tức
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

                // 4. FIX: Bỏ luôn thẻ báo lỗi (else). Luôn đẩy khách về màn hình Radar!
                // Dù hiện tại chưa có tài xế, nhưng 1 phút sau có tài xế bật App lên thì họ vẫn thấy cuốc này.
                TempData["Success"] = "Đang kết nối với tài xế gần nhất...";
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

        // --- HỦY CHUYẾN XE ---
        [HttpPost]
        public async Task<IActionResult> CancelRide(int rideId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                int customerId = int.Parse(userIdClaim.Value);

                // Kéo cuốc xe lên (kèm theo thông tin tài xế nếu có)
                var ride = _context.Rides.Include(r => r.Driver)
                    .FirstOrDefault(r => r.Id == rideId && r.CustomerId == customerId);

                // Chỉ cho phép hủy nếu chuyến xe chưa hoàn thành hoặc chưa bị hủy trước đó
                if (ride != null &&
                    ride.Status != RideHailingApp.Common.Enums.RideStatusEnum.Completed &&
                    ride.Status != RideHailingApp.Common.Enums.RideStatusEnum.Cancelled)
                {
                    ride.Status = RideHailingApp.Common.Enums.RideStatusEnum.Cancelled;
                    _context.SaveChanges();

                    // NẾU ĐÃ CÓ TÀI XẾ NHẬN: Bắn SignalR báo cho máy tài xế biết để họ quay về trạng thái Rảnh
                    if (ride.DriverId.HasValue)
                    {
                        await _hubContext.Clients.User(ride.DriverId.Value.ToString())
                            .SendAsync("RideCancelledByCustomer");
                    }
                }
            }
            return RedirectToAction("Index");
        }
    }
}