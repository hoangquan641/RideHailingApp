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

        public CustomerController(IRideService rideService, IHubContext<RideHub> hubContext, ApplicationDbContext context)
        {
            _rideService = rideService;
            _hubContext = hubContext;
            _context = context;
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
    }
}