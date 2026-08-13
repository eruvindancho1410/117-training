using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_ReturnsProductsAtOrBelowThreshold()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 9, sku: "SKU-LOW009");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-LOW010");
        TestSetup.AddProduct(db, stock: 11, sku: "SKU-HIGH11");

        var result = await service.GetLowStockAsync(10);
        var products = result.Value!;

        Assert.True(result.Success);
        Assert.Equal(2, products.Count);
        Assert.Contains(products, p => p.StockQuantity == 9);
        Assert.Contains(products, p => p.StockQuantity == 10);
        Assert.DoesNotContain(products, p => p.StockQuantity == 11);
    }

    [Fact]
    public async Task GetLowStock_SumsSalesWithinLast30DaysExcludingCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 5);
        AddOrder(db, customer.Id, product.Id, 3, OrderStatus.Confirmed, DateTime.UtcNow.AddDays(-5));
        AddOrder(db, customer.Id, product.Id, 7, OrderStatus.Cancelled, DateTime.UtcNow.AddDays(-3));
        AddOrder(db, customer.Id, product.Id, 11, OrderStatus.Shipped, DateTime.UtcNow.AddDays(-31));

        var result = await service.GetLowStockAsync(10);

        Assert.True(result.Success);
        Assert.Equal(3, Assert.Single(result.Value!).SoldQuantityLast30Days);
    }

    [Fact]
    public async Task GetLowStock_ProductWithoutRecentSales_ReturnsZeroSoldQuantity()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 5);

        var result = await service.GetLowStockAsync(10);

        Assert.True(result.Success);
        Assert.Equal(0, Assert.Single(result.Value!).SoldQuantityLast30Days);
    }

    private static void AddOrder(
        Infrastructure.Data.OrderHubDbContext db,
        int customerId,
        int productId,
        int quantity,
        OrderStatus status,
        DateTime createdAt)
    {
        db.Orders.Add(new Order
        {
            CustomerId = customerId,
            Status = status,
            CreatedAt = createdAt,
            Items =
            {
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPriceSnapshot = 100m
                }
            }
        });
        db.SaveChanges();
    }
}
