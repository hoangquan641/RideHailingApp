namespace RideHailingApp.Common.Enums
{
    public enum RideStatusEnum
    {
        Pending = 0,     // Khách vừa đặt, đang tìm tài xế
        Accepted = 1,    // Tài xế đã nhận cuốc
        InProgress = 2,  // Đang di chuyển
        Completed = 3,   // Hoàn thành
        Cancelled = 4    // Đã hủy
    }
}