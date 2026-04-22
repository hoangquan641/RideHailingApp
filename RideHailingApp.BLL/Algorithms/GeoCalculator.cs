using System;

namespace RideHailingApp.BLL.Algorithms
{
    public static class GeoCalculator
    {
        // Bán kính trái đất tính bằng km
        private const double EarthRadiusKm = 6371.0;

        public static decimal CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            // Trả về khoảng cách (km), làm tròn 2 chữ số thập phân
            return Math.Round((decimal)(EarthRadiusKm * c), 2);
        }

        private static double ToRadians(decimal angleIn10thofaDegree)
        {
            return (double)angleIn10thofaDegree * Math.PI / 180.0;
        }
    }
}