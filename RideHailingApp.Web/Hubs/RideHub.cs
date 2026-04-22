using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RideHailingApp.Web.Hubs
{
    [Authorize] // Chỉ người đã đăng nhập mới được kết nối
    public class RideHub : Hub
    {
        // SignalR tự động map ConnectionId với User.Identity.NameIdentifier (Id của User)
        // Nên chúng ta không cần viết code lưu trữ ConnectionId thủ công.
    }
}