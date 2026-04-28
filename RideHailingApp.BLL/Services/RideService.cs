using RideHailingApp.BLL.Algorithms;
using RideHailingApp.Common.DTOs;
using RideHailingApp.DAL.Data;
using RideHailingApp.DAL.Entities;

namespace RideHailingApp.BLL.Services
{
    public interface IRideService
    {
        decimal CalculateFare(decimal distanceKm, string vehicleType); // Thêm vehicleType
        List<Ride> GetPendingRides(int currentDriverId);
        bool AcceptRide(int rideId, int driverId);
        Ride BookRide(BookRideDTO model);

        int? FindNearestAvailableDriver(decimal pickupLat, decimal pickupLng, List<int> excludedDriverIds, string requestedVehicleType);

        void MarkRideAsDeclined(int rideId, int driverId);
        void SetDriverAvailability(int driverId, bool isAvailable);
        void CancelPendingRidesForCustomer(int customerId);
    }

    public class RideService : IRideService
    {
        private readonly ApplicationDbContext _context;

        public RideService(ApplicationDbContext context)
        {
            _context = context;
        }

        public decimal CalculateFare(decimal distanceKm, string vehicleType)
        {
            decimal baseFare = vehicleType == "Xe máy (Bike)" ? 15000m : 20000m;
            decimal perKmFare = vehicleType == "Xe máy (Bike)" ? 5000m : 10000m;

            if (distanceKm <= 2m) return baseFare;
            return baseFare + ((distanceKm - 2m) * perKmFare);
        }

        public Ride BookRide(BookRideDTO model)
        {
            decimal distance = GeoCalculator.CalculateDistance(model.PickupLat, model.PickupLng, model.DropoffLat, model.DropoffLng);
            decimal fare = CalculateFare(distance, model.VehicleType); // Truyền loại xe vào

            var ride = new Ride
            {
                CustomerId = model.CustomerId,
                RequestedVehicleType = model.VehicleType, // Lưu vào DB
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
            return ride;
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
        public int? FindNearestAvailableDriver(decimal pickupLat, decimal pickupLng, List<int> excludedDriverIds, string requestedVehicleType)
        {
            var availableDrivers = _context.Users
                .Where(u => u.Role == Common.Enums.RoleEnum.Driver
                         && u.IsDriverAvailable == true
                         && u.CurrentLat.HasValue
                         && u.CurrentLng.HasValue
                         && u.VehicleType == requestedVehicleType // CHỈ QUÉT TÀI XẾ KHỚP LOẠI XE
                         && !excludedDriverIds.Contains(u.Id)
                         && !u.IsDeleted)
                .ToList();

            int? nearestDriverId = null;
            decimal minDistance = decimal.MaxValue;

            foreach (var driver in availableDrivers)
            {
                decimal distance = Algorithms.GeoCalculator.CalculateDistance(
                    pickupLat, pickupLng, driver.CurrentLat.Value, driver.CurrentLng.Value);

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

        // ==============================================================================
        // BỔ SUNG MỚI: CẬP NHẬT TRẠNG THÁI TÀI XẾ (Dùng cho API và SignalR Disconnect)
        // ==============================================================================
        public void SetDriverAvailability(int driverId, bool isAvailable)
        {
            var driver = _context.Users.Find(driverId);
            if (driver != null)
            {
                driver.IsDriverAvailable = isAvailable;
                _context.SaveChanges();
            }
        }

        // ==============================================================================
        // BỔ SUNG MỚI: HỦY CUỐC XE PENDING (Dùng khi khách hàng tắt web/rớt mạng)
        // ==============================================================================
        public void CancelPendingRidesForCustomer(int customerId)
        {
            // Lấy tất cả các cuốc xe đang Pending của khách hàng này
            var pendingRides = _context.Rides
                .Where(r => r.CustomerId == customerId && r.Status == Common.Enums.RideStatusEnum.Pending)
                .ToList();

            if (pendingRides.Any())
            {
                foreach (var ride in pendingRides)
                {
                    ride.Status = Common.Enums.RideStatusEnum.Cancelled;
                }
                _context.SaveChanges();
            }
        }
    }
}