namespace Distributor.Api.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class User : Entity
{
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Name { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public string Territory { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class Company : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class Shop : Entity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public decimal CreditLimit { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class Product : Entity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Sku { get; set; }
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public string Category { get; set; } = "";
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class InventoryTransaction : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;
    public required string Type { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid? ReferenceId { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class StockIn : Entity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string StockInNumber { get; set; }
    public DateOnly StockInDate { get; set; }
    public decimal StockTotal { get; set; }
    public string PaymentStatus { get; set; } = "PENDING";
    public string Notes { get; set; } = "";
    public List<StockInProduct> Products { get; set; } = [];
    public List<StockInPayment> Payments { get; set; } = [];
}

public sealed class StockInProduct : Entity
{
    public Guid StockInId { get; set; }
    public StockIn? StockIn { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class StockInPayment : Entity
{
    public Guid StockInId { get; set; }
    public StockIn? StockIn { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal PaidAmount { get; set; }
    public required string Method { get; set; }
    public string Reference { get; set; } = "";
}

public sealed class Order : Entity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid ShopId { get; set; }
    public Shop? Shop { get; set; }
    public Guid SalesRepId { get; set; }
    public User? SalesRep { get; set; }
    public required string OrderNumber { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public decimal OrderTotal { get; set; }
    public string PaymentStatus { get; set; } = "PENDING";
    public string Status { get; set; } = "CONFIRMED";
    public string DeliveryAddress { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<OrderProduct> Products { get; set; } = [];
    public List<OrderPayment> Payments { get; set; } = [];
}

public sealed class OrderProduct : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal FreeIssueQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineSubtotal { get; set; }
}

public sealed class OrderPayment : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal PaidAmount { get; set; }
    public required string Method { get; set; }
    public string Reference { get; set; } = "";
}

public sealed class Cheque : Entity
{
    public Guid ShopId { get; set; }
    public Shop? Shop { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public required string ChequeNumber { get; set; }
    public required string BankName { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ChequeDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public int RemindBeforeDays { get; set; } = 3;
    public string Notes { get; set; } = "";
}
