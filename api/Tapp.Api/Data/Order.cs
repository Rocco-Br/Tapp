namespace Tapp.Api.Data;

public enum OrderStatus { New, Preparing, Served, Cancelled }

public class Order
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string GuestId { get; set; } = "";
    // Who placed it: the guest, or staff ordering on the guest's behalf.
    public string PlacedById { get; set; } = "";
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

// Name, options and price are copied at order time so later menu edits never rewrite history.
public class OrderLine
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Options { get; set; }
    public int UnitPriceCents { get; set; }
    public int Quantity { get; set; }
}
