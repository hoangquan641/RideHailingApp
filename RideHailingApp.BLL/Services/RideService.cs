using RideHailingApp.BLL.Algorithms;
using RideHailingApp.Common.DTOs;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;

namespace RideHailingApp.BLL.Services
{
    public interface IRideService
    {
        decimal CalculateFare(decimal distanceKm);
        List<Ride> GetPendingRides(int currentDriverId);
        bool AcceptRide(int rideId, int driverId);
        Ride BookRide(BookRideDTO model);

        // Bổ sung
        int? FindNearestAvailableDriver(decimal pickupLat, decimal pickupLng, List<int> excludedDriverIds);
        void MarkRideAsDeclined(int rideId, int driverId);
    }

    public class RideService : IRideService
    {
        private readonly ApplicationDbContext _context;

        public RideService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Quy tắc: 2km đầu 20k, sau đó 10k/km
        public decimal CalculateFare(decimal distanceKm)
        {
            if (distanceKm <= 2m)
            {
                return 20000m;
            }

            decimal extraDistance = distanceKm - 2m;
            return 20000m + (extraDistance * 10000m);
        }

        public Ride BookRide(BookRideDTO model)
        {
            // 1. Tính khoảng cách
            decimal distance = GeoCalculator.CalculateDistance(
                model.PickupLat, model.PickupLng,
                model.DropoffLat, model.DropoffLng);

            // 2. Tính tiền
            decimal fare = CalculateFare(distance);

            // 3. Tạo Entity lưu xuống Database
            var ride = new Ride
            {
                CustomerId = model.CustomerId,
                PickupAddress = model.PickupAddress,
                PickupLat = model.PickupLat,
                PickupLng = model.PickupLng,
                DropoffAddress = model.DropoffAddress,
                DropoffLat = model.DropoffLat,
                DropoffLng = model.DropoffLng,
                DistanceKm = distance,
                Fare = fare,
                Status = Common.Enums.RideStatusEnum.Pending,
                CreatedAt = DateTime.Now
            };

            _context.Rides.Add(ride);
            _context.SaveChanges();

            return ride; // Trả về thông tin chuyến đi để hiển thị cho khách
        }

        public List<Ride> GetPendingRides(int currentDriverId)
        {
            string searchStr = $"[{currentDriverId}]";

            return _context.Rides
                .Where(r => r.Status == Common.Enums.RideStatusEnum.Pending
                         && !r.IsDeleted
                         && (string.IsNullOrEmpty(r.DeclinedDriverIds) || !r.DeclinedDriverIds.Contains(searchStr)))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public bool AcceptRide(int rideId, int driverId)
        {
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId && r.Status == Common.Enums.RideStatusEnum.Pending);

            if (ride != null)
            {
                ride.DriverId = driverId;
                ride.Status = Common.Enums.RideStatusEnum.Accepted; // Chuyển trạng thái sang Đã nhận
                _context.SaveChanges();
                return true;
            }
            return false; // Cuốc xe có thể đã bị tài xế khác nhận mất hoặc khách hủy
        }

        // Bổ sung: tìm tài xế gần nhất
        public int? FindNearestAvailableDriver(decimal pickupLat, decimal pickupLng, List<int> excludedDriverIds)
        {
            var availableDrivers = _context.Users
                .Where(u => u.Role == Common.Enums.RoleEnum.Driver
                         && u.IsDriverAvailable == true
                         && u.CurrentLat.HasValue
                         && u.CurrentLng.HasValue
                         && !excludedDriverIds.Contains(u.Id) // Loại trừ những người đã từ chối
                         && !u.IsDeleted)
                .ToList();

            int? nearestDriverId = null;
            decimal minDistance = decimal.MaxValue;

            foreach (var driver in availableDrivers)
            {
                decimal distance = Algorithms.GeoCalculator.CalculateDistance(
                    pickupLat, pickupLng,
                    driver.CurrentLat.Value, driver.CurrentLng.Value);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestDriverId = driver.Id;
                }
            }

            return nearestDriverId;
        }

        // Bổ sung: đánh dấu cuốc xe đã bị từ chối bởi tài xế
        public void MarkRideAsDeclined(int rideId, int driverId)
        {
            var ride = _context.Rides.Find(rideId);
            if (ride != null)
            {
                ride.DeclinedDriverIds += $"[{driverId}]";
                _context.SaveChanges();
            }
        }
    }
}
