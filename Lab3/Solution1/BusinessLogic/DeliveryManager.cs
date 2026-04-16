
namespace BusinessLogic;

public class OrderItem {
    public string Name { get; set; }
    public int Price { get; set; }
}

public class DeliveryManager {
    public string CustomerName { get; set; }
    public int TotalAmount { get; private set; }
    public List<OrderItem> Cart { get; } = new();
    public bool IsStoreOpen { get; set; } = true;
    
    public void AddProduct(string name, int price) {
        if (price <= 0) throw new ArgumentException("Цена должна быть больше нуля");
        Cart.Add(new OrderItem { Name = name, Price = price });
        TotalAmount += price;
    }
    
    public async Task<bool> ConfirmOrderAsync(double km) {
        await Task.Delay(50);
        if (!IsStoreOpen || km <= 0 || Cart.Count == 0) return false;
        return true;
    }
    
    public string GetCourierPhone(int id) => id > 0 ? "+7-900-123-45-67" : null;
}