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

            // Lấy tên khách hàng từ Claim (đã được lưu lúc Login)
            string customerName = User.Identity.Name ?? "Khách hàng";

            if (ModelState.IsValid)
            {
                // 1. Lưu chuyến đi vào DB
                var ride = _rideService.BookRide(model);

                // 2. Tìm tài xế gần nhất
                var nearestDriverId = _rideService.FindNearestAvailableDriver(model.PickupLat, model.PickupLng, new List<int>());

                if (nearestDriverId != null)
                {
                    // 3. Đẩy thông báo qua SignalR
                    // Đã bổ sung biến customerName vào ĐÚNG VỊ TRÍ THAM SỐ THỨ 2
                    await _hubContext.Clients.User(nearestDriverId.Value.ToString()).SendAsync(
                        "ReceiveRideRequest",
                        ride.Id,
                        customerName,      // <--- Tham số vừa được bổ sung để khớp với Javascript
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
    }
}