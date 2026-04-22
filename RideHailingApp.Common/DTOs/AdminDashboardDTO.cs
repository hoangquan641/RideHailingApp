namespace RideHailingApp.Common.DTOs
{
    public class AdminDashboardDTO
    {
        public int TotalCustomers { get; set; }
        public int TotalDrivers { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalCompletedRides { get; set; }

        // Dữ liệu phục vụ vẽ biểu đồ (Doanh thu 7 ngày gần nhất)
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartData { get; set; } = new List<decimal>();
    }
}