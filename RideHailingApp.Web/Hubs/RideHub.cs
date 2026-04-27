using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RideHailingApp.BLL.Services;
using System;
using System.Threading.Tasks;

namespace RideHailingApp.Web.Hubs
{
    [Authorize]
    public class RideHub : Hub
    {
        private readonly IRideService _rideService;

        public RideHub(IRideService rideService)
        {
            _rideService = rideService;
        }

        // --- BỔ SUNG: HÀM ĐẨY TỌA ĐỘ CHO KHÁCH HÀNG ---
        public async Task SendLocationToCustomer(string customerId, decimal lat, decimal lng)
        {
            // Bắn tín hiệu "UpdateDriverLocation" kèm tọa độ tới riêng ID của khách hàng
            await Clients.User(customerId).SendAsync("UpdateDriverLocation", lat, lng);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdStr = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                if (Context.User.IsInRole("Driver"))
                {
                    _rideService.SetDriverAvailability(userId, false);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}