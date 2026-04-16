using Library;
using BusinessLogic;

namespace Tests;

[MyTestClass]
public class DeliveryTests
{
    private DeliveryManager _manager;

    [MyBeforeTest]
    public void Setup() {
        _manager = new DeliveryManager();
        _manager.CustomerName = "Алексей";
        _manager.AddProduct("Базовый соус", 50);
    }

    [MyAfterTest]
    public void Teardown() {
        _manager = null;
    }
    
    [MyTest]
    [MyTestTimeout(1000)] 
    public async Task SlowTest_ShouldPass()
    {
        await Task.Delay(500);
        MyAssert.IsTrue(true);
    }

    [MyTest]
    [MyTestTimeout(200)] 
    public async Task TooSlowTest_ShouldTimeout()
    {
        await Task.Delay(1000); 
        MyAssert.IsTrue(true);
    }
    
    [MyTest]
    [MyTestCase(950, 1000)]
    [MyTestCase(450, 500)]
    public void AddProduct_ValidPrice(int price, int expected) {
        _manager.AddProduct("Тестовый товар", price);
        MyAssert.AreEqual(expected, _manager.TotalAmount);
    }
    
    [MyTest]
    public void AddProduct_Execution() {
        decimal startPrice = _manager.TotalAmount;
        _manager.AddProduct("Пицца", 600);
        MyAssert.AreNotEqual(startPrice, _manager.TotalAmount);
    }
    
    [MyTest]
    public async Task ConfirmOrderAsync_NormalConditions() {
        bool result = await _manager.ConfirmOrderAsync(3);
        MyAssert.IsTrue(result);
    }
    
    [MyTest]
    public async Task ConfirmOrderAsync_StoreIsClosed() {
        _manager.IsStoreOpen = false;
        bool result = await _manager.ConfirmOrderAsync(1.0);
        MyAssert.IsFalse(result);
    }
    
    [MyTest]
    public void GetCourierPhone_UnknownId() {
        MyAssert.IsNull(_manager.GetCourierPhone(-1));
    }
    
    [MyTest]
    public void Setup_WhenCalled() {
        MyAssert.IsNotNull(_manager);
    }
    
    [MyTest]
    public void Cart_AfterSetup() {
        MyAssert.IsNotEmpty(_manager.Cart);
    }
    
    [MyTest]
    public void Cart_LastAddedItem() {
        _manager.AddProduct("Кола", 100);
        var item = _manager.Cart[_manager.Cart.Count - 1];
        MyAssert.Contains(item, _manager.Cart);
    }
    
    [MyTest]
    public void CartItem_WhenIndexed() {
        MyAssert.IsInstanceOf<OrderItem>(_manager.Cart[0]);
    }
    
    [MyTest]
    public void AddProduct_InvalidPrice() {
        MyAssert.Throws<ArgumentException>(() => _manager.AddProduct("Ошибка", -100));
    }

    [MyTest(Skip = "Ожидаем интеграции с картами")]
    public void CalculateRoute_FeatureNotReady() {
        MyAssert.IsTrue(false);
    }
}