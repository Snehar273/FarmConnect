using System.ComponentModel.DataAnnotations;
namespace FarmConnect.Models;

public enum ListingStatus { Active, Inactive, SoldOut }
public enum OrderStatus { Placed, Accepted, Rejected, Completed, Cancelled }
public enum BookingStatus { PendingAssignment, Assigned, Confirmed, PickedUp, InTransit, Delivered, Failed, Cancelled }
public enum OutboxStatus { Pending, Processing, Processed, Failed }

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)] public string FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
public class FarmerProfile
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    [StringLength(100)] public string FarmName { get; set; }
    [StringLength(80)] public string Location { get; set; }
}
public class BuyerProfile
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    [StringLength(200)] public string Address { get; set; }
}
public class Produce
{
    public int ProduceId { get; set; }
    [Required, StringLength(80)] public string Name { get; set; }
    [StringLength(8)] public string Emoji { get; set; } = "🌿";
}
public class Listing
{
    public int Id { get; set; }
    public string FarmerId { get; set; }
    public ApplicationUser Farmer { get; set; }
    public int ProduceId { get; set; }
    public Produce Produce { get; set; }
    [StringLength(80)] public string Variety { get; set; }
    [Required, StringLength(1)] public string Grade { get; set; } = "A";
    public decimal QuantityAvailable { get; set; }
    public decimal QuantitySold { get; set; }
    [Required, StringLength(20)] public string Unit { get; set; } = "kg";
    public decimal MinOrderQty { get; set; }
    public decimal PricePerUnit { get; set; }
    public DateTime HarvestDate { get; set; }
    [StringLength(500)] public string Description { get; set; }
    [StringLength(200)] public string ImagePath { get; set; }
    [Required, StringLength(80)] public string Location { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
public class Order
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string OrderNo { get; set; }
    public string BuyerId { get; set; }
    public ApplicationUser Buyer { get; set; }
    public string FarmerId { get; set; }
    public ApplicationUser Farmer { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public LogisticsBooking Logistics { get; set; }
}
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int ListingId { get; set; }
    public Listing Listing { get; set; }
    [StringLength(120)] public string ProduceName { get; set; }
    [StringLength(20)] public string Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal AgreedUnitPrice { get; set; }
    public decimal LineTotal => Quantity * AgreedUnitPrice;
}
public class PriceHistory
{
    public int PriceHistoryId { get; set; }
    public int ProduceId { get; set; }
    public Produce Produce { get; set; }
    [Required, StringLength(80)] public string MarketName { get; set; }
    public DateTime PriceDate { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal ModalPrice { get; set; }
    [Required, StringLength(20)] public string Unit { get; set; } = "kg";
    [StringLength(120)] public string Source { get; set; }
}
public class PriceHistoryView
{
    public int PriceHistoryId { get; set; }
    public int ProduceId { get; set; }
    public string ProduceName { get; set; }
    public string MarketName { get; set; }
    public DateTime PriceDate { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal ModalPrice { get; set; }
    public string Unit { get; set; }
    public string Source { get; set; }
}
public class SellingExpense
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public string FarmerId { get; set; }
    [Required, StringLength(200)] public string Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Now;
}

// ---------- M2 ----------
public class LogisticsPartner
{
    public int LogisticsPartnerId { get; set; }
    [Required, StringLength(100)] public string PartnerName { get; set; }
    [StringLength(15)] public string ContactNumber { get; set; }
    [StringLength(300)] public string PickupAreas { get; set; }
    [StringLength(300)] public string DeliveryAreas { get; set; }
    [StringLength(50)] public string VehicleType { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal RatePerKg { get; set; }
    public bool IsAvailable { get; set; } = true;
    [StringLength(20)] public string Status { get; set; } = "Active";
}
public class LogisticsBooking
{
    public int BookingId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int? LogisticsPartnerId { get; set; }
    public LogisticsPartner Partner { get; set; }
    [StringLength(80)] public string PickupLocation { get; set; }
    [StringLength(200)] public string DeliveryLocation { get; set; }
    public decimal? QuantityKg { get; set; }
    public decimal? EstimatedCost { get; set; }
    public BookingStatus BookingStatus { get; set; } = BookingStatus.PendingAssignment;
    [StringLength(300)] public string Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
public class OutboxEvent
{
    public Guid EventId { get; set; }
    [Required, StringLength(60)] public string EventType { get; set; }
    [Required] public string Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int RetryCount { get; set; }
    [StringLength(500)] public string ErrorMessage { get; set; }
}