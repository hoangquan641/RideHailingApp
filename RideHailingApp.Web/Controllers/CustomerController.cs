using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideHailingApp.BLL.Services;
using RideHailingApp.Common.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using RideHailingApp.Web.Hubs;
using System.Collections.Generic; // Cần thiết để dùng List<int>

namespace RideHailingApp.Web.Controllers
{
    // Yêu cầu đăng nhập
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly IRideService _rideService;
        private readonly IHubContext<RideHub> _hubContext;

        public CustomerController(IRideService rideService, IHubContext<RideHub> hubContext)
        {
            _rideService = rideService;
            _hubContext = hubContext;
        }

        // Trang chủ của Khách hàng
        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;
            return View();
        }

        // --- ĐẶT XE ---
        [HttpGet]
        public IActionResult BookRide()
        {
            return View(new BookRideDTO());
        }

        [HttpPost]
        public async Task<IActionResult> BookRide(BookRideDTO model)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null) model.CustomerId = int.Parse(userIdClaim.Value);

            if (ModelState.IsValid)
            {
                // 1. Lưu chuyến đi vào DB
                var ride = _rideService.BookRide(model);

                // 2. Tìm tài xế gần nhất (Truyền vào danh sách rỗng vì cuốc xe mới tinh, chưa ai từ chối)
                var nearestDriverId = _rideService.FindNearestAvailableDriver(model.PickupLat, model.PickupLng, new List<int>());

                if (nearestDriverId != null)
                {
                    // 3. Đẩy thông báo qua SignalR trực tiếp cho tài xế đó, kèm dữ liệu dự phòng
                    await _hubContext.Clients.User(nearestDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        ride.PickupAddress,
                        ride.DropoffAddress,
                        ride.Fare,
                        ride.DistanceKm,
                        ride.PickupLat,    // Gửi thêm Vĩ độ điểm đón
                        ride.PickupLng,    // Gửi thêm Kinh độ điểm đón
                        "[]"               // Gửi danh sách từ chối (hiện tại là rỗng)
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
    }
}