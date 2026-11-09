using FarmConnect.Models;

namespace FarmConnect.Services;

public record OrderResult(bool Success, string Error, Order Order);
public record StatusResult(bool Ok, bool NotFound, string Message);
public record FarmerDashboard(int Active, int Pending, int Completed, decimal Sales);
public record PriceResult(List<PriceHistoryView> Rows, string Source, long Ms);
public record OrderPlacedEvent(int OrderId, string OrderNo, int ListingId, decimal Quantity, string Unit);

public interface IEventHandler
{
    string EventType { get; }
    Task HandleAsync(OutboxEvent e, CancellationToken ct);
}

public interface IFarmerService
{
    Task<ApplicationUser> GetFarmerByIdAsync(string userId);
    Task<FarmerProfile> GetFarmerProfileAsync(string userId);
    Task<FarmerProfile> UpdateFarmerProfileAsync(string userId, string farmName, string location);
    Task<List<Listing>> GetFarmerListingsAsync(string userId);
    Task<List<Order>> GetFarmerOrdersAsync(string userId);
    Task<EarningsVm> GetFarmerEarningsAsync(string userId, string period, DateTime? fromDate, DateTime? toDate);
    Task<(bool Ok, string Message)> AddSellingExpenseAsync(string userId, int orderId, string description, decimal amount);
    Task<FarmerDashboard> GetDashboardAsync(string userId);
}

public interface IProduceService
{
    Task<List<Produce>> GetAllProduceAsync();
    Task<Produce> GetProduceByIdAsync(int produceId);
    Task<List<Listing>> SearchListingsAsync(string q, string grade, string location, decimal? minPrice, decimal? maxPrice, string sort, string farmerId = null);
    Task<Listing> GetListingByIdAsync(int listingId);
    Task<(bool Ok, string Error)> CheckAvailabilityAsync(int listingId, decimal quantity);
    Task<PriceResult> GetPriceHistoryAsync(int produceId, string market, DateTime? fromDate, DateTime? toDate);
}

public interface IOrderService
{
    Task<OrderResult> PlaceOrderAsync(string buyerId, int listingId, decimal quantity);
    Task<List<Order>> GetBuyerOrdersAsync(string buyerId);
    Task<List<Order>> GetFarmerOrdersAsync(string farmerId);
    Task<List<Order>> GetAllOrdersAsync();
    Task<StatusResult> UpdateStatusAsync(int orderId, string userId, bool isFarmer, OrderStatus status);
}

public interface ILogisticsService
{
    Task<LogisticsBooking> BookForOrderAsync(int orderId, CancellationToken ct = default);
    Task<(bool Ok, string Message)> AssignPartnerAsync(int bookingId, int partnerId);
    Task<List<LogisticsBooking>> GetBookingsAsync();
    Task<List<LogisticsPartner>> GetPartnersAsync();
    Task<bool> TogglePartnerAsync(int partnerId);
    Task<int> RematchPendingAsync();
}

public interface IPriceCacheService
{
    Task<PriceResult> GetPricesAsync(int produceId, string market, DateTime? fromDate, DateTime? toDate);
    Task<PriceResult> GetDailyPricesAsync(int produceId, string market, DateTime date);
    Task<List<string>> GetMarketsAsync();
    Task AddPriceAsync(PriceHistory p);
    Task InvalidateProduceAsync(int produceId);
    Task FlushAsync();
}