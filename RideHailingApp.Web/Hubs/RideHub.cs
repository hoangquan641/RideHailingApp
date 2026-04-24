using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RideHailingApp.BLL.Services;
using System;
using System.Threading.Tasks;

namespace RideHailingApp.Web.Hubs
{
    [Authorize] // Chỉ người đã đăng nhập mới được kết nối
    public class RideHub : Hub
    {
        private readonly IRideService _rideService;

        // Tiêm Service từ tầng BLL vào để xử lý logic Database
        public RideHub(IRideService rideService)
        {
            _rideService = rideService;
        }

        /// <summary>
        /// Tự động kích hoạt khi người dùng đóng trình duyệt, tắt tab hoặc rớt mạng
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Lấy User ID từ định danh kết nối (thường là ClaimTypes.NameIdentifier)
            var userIdStr = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                // CHỈ XỬ LÝ CHO TÀI XẾ: Tắt trạng thái nhận chuyến khi họ tắt Web/Chuyển tab
                if (Context.User.IsInRole("Driver"))
                {
                    _rideService.SetDriverAvailability(userId, false);
                }

                // KHÁCH HÀNG: Không hủy chuyến Pending khi họ chuyển tab
                // => Không cần xử lý gì thêm
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
