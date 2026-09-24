namespace Distributor.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);
public sealed record CreateUserRequest(Guid? CompanyId, string Name, string Username, string Password, string Role, string Territory, bool Active = true);
public sealed record UpdateUserRequest(Guid? CompanyId, string Name, string Role, string Territory, bool Active);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminResetPasswordRequest(string NewPassword);
public sealed record CompanyRequest(string Code, string Name, string ContactName, string Phone, string Address, bool Active = true);
public sealed record ShopRequest(Guid CompanyId, string Code, string Name, string ContactName, string Phone, string Address, string City, decimal CreditLimit, bool Active = true);
public sealed record ProductRequest(Guid CompanyId, string Sku, string Barcode, string Name, string Category, decimal SellingPrice, decimal CostPrice, decimal ReorderLevel, DateOnly? ExpiryDate, decimal OpeningStock = 0, bool Active = true);
public sealed record StockAdjustmentRequest(decimal Quantity, string Direction, string Notes);
public sealed record StockInLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record StockInRequest(Guid CompanyId, string StockInNumber, DateOnly StockInDate, string Notes, List<StockInLineRequest> Products);
public sealed record PaymentRequest(DateOnly PaymentDate, decimal PaidAmount, string Method, string Reference, string? BankName = null, DateOnly? ChequeDate = null);
public sealed record OrderLineRequest(Guid ProductId, decimal Quantity, decimal FreeIssueQuantity, decimal UnitPrice);
public sealed record OrderRequest(Guid ShopId, Guid SalesRepId, string OrderNumber, DateOnly OrderDate, DateOnly DeliveryDate, string DeliveryAddress, string Notes, List<OrderLineRequest> Products);
public sealed record DeliveryLineRequest(Guid ProductId, decimal Quantity);
public sealed record CompleteOrdersRequest(List<Guid> OrderIds, DateOnly DeliveryDate, List<DeliveryLineRequest> Products);
public sealed record ChequeRequest(Guid ShopId, Guid? OrderId, string ChequeNumber, string BankName, decimal Amount, DateOnly ChequeDate, string Status, int RemindBeforeDays, string Notes);
