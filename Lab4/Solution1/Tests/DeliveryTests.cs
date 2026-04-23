using Library;
using BusinessLogic;

namespace Tests;

[MyTestClass]
public class DeliveryTests
{
    private DeliveryManager _manager = null!;

    [MyBeforeTest]
    public void Setup() 
    {
        _manager = new DeliveryManager();
        _manager.CustomerName = "Алексей";
        _manager.AddProduct("Базовый соус", 50);
    }

    [MyAfterTest]
    public void Teardown() 
    {
        _manager = null!;
    }

    [MyTest]
    [MyTestTimeout(1000)]
    public async Task SlowTest_ShouldPass()
    {
        await Task.Delay(200);
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
    [MyTestCase(950,  1000)]
    [MyTestCase(450,  500)]
    [MyTestCase(100,  150)]
    [MyTestCase(200,  250)]
    [MyTestCase(300,  350)]
    [MyTestCase(750,  800)]
    [MyTestCase(1950, 2000)]
    [MyTestCase(50,   100)]
    [MyTestCase(1000, 1050)]
    [MyTestCase(9950, 10000)]
    public void AddProduct_ValidPrice(int price, int expected) 
    {
        _manager.AddProduct("Тестовый товар", price);
        MyAssert.AreEqual(expected, _manager.TotalAmount);
    }
    

    [MyTest]
    [MyTestCase("Пицца",    600)]
    [MyTestCase("Бургер",   350)]
    [MyTestCase("Суши",     480)]
    [MyTestCase("Паста",    290)]
    [MyTestCase("Стейк",    890)]
    [MyTestCase("Салат",    180)]
    [MyTestCase("Десерт",   220)]
    [MyTestCase("Кофе",     120)]
    [MyTestCase("Сок",      80)]
    [MyTestCase("Вода",     60)]
    public void AddProduct_ChangesTotal(string name, int price) 
    {
        decimal before = _manager.TotalAmount;
        _manager.AddProduct(name, price);
        MyAssert.AreNotEqual(before, _manager.TotalAmount);
    }

    [MyTest]
    [MyTestCase(1.0)]
    [MyTestCase(3.0)]
    [MyTestCase(5.5)]
    [MyTestCase(10.0)]
    [MyTestCase(0.1)]
    public async Task ConfirmOrderAsync_NormalConditions(double km) 
    {
        bool result = await _manager.ConfirmOrderAsync(km);
        MyAssert.IsTrue(result);
    }

    [MyTest]
    public async Task ConfirmOrderAsync_StoreIsClosed() 
    {
        _manager.IsStoreOpen = false;
        bool result = await _manager.ConfirmOrderAsync(1.0);
        MyAssert.IsFalse(result);
    }

    [MyTest]
    public async Task ConfirmOrderAsync_ZeroKm() 
    {
        bool result = await _manager.ConfirmOrderAsync(0);
        MyAssert.IsFalse(result);
    }

    [MyTest]
    public async Task ConfirmOrderAsync_NegativeKm() 
    {
        bool result = await _manager.ConfirmOrderAsync(-5);
        MyAssert.IsFalse(result);
    }

    [MyTest]
    public void GetCourierPhone_UnknownId() 
    {
        MyAssert.IsNull(_manager.GetCourierPhone(-1));
    }

    [MyTest]
    [MyTestCase(1)]
    [MyTestCase(2)]
    [MyTestCase(100)]
    public void GetCourierPhone_ValidId(int id) 
    {
        MyAssert.IsNotNull(_manager.GetCourierPhone(id));
    }

    [MyTest]
    public void Setup_WhenCalled() 
    {
        MyAssert.IsNotNull(_manager);
    }

    [MyTest]
    public void Cart_AfterSetup() 
    {
        MyAssert.IsNotEmpty(_manager.Cart);
    }

    [MyTest]
    public void Cart_LastAddedItem() 
    {
        _manager.AddProduct("Кола", 100);
        var item = _manager.Cart[_manager.Cart.Count - 1];
        MyAssert.Contains(item, _manager.Cart);
    }

    [MyTest]
    public void CartItem_WhenIndexed() 
    {
        MyAssert.IsInstanceOf<OrderItem>(_manager.Cart[0]);
    }
    

    [MyTest]
    public void AddProduct_InvalidPrice() 
    {
        MyAssert.Throws<ArgumentException>(() => _manager.AddProduct("Ошибка", -100));
    }

    [MyTest]
    public void AddProduct_ZeroPrice() 
    {
        MyAssert.Throws<ArgumentException>(() => _manager.AddProduct("Ноль", 0));
    }
    

    [MyTest]
    public void CustomerName_IsSet() 
    {
        MyAssert.AreEqual("Алексей", _manager.CustomerName);
    }

    [MyTest]
    [MyTestCase("Иван")]
    [MyTestCase("Мария")]
    [MyTestCase("Дмитрий")]
    [MyTestCase("Анна")]
    [MyTestCase("Сергей")]
    public void CustomerName_CanBeChanged(string name) 
    {
        _manager.CustomerName = name;
        MyAssert.AreEqual(name, _manager.CustomerName);
    }

    [MyTest]
    [MyTestCase(1)]
    [MyTestCase(3)]
    [MyTestCase(5)]
    public void Cart_CountAfterAdding(int count) 
    {
        for (int i = 0; i < count; i++)
            _manager.AddProduct($"Товар{i}", 100);
        // изначально 1 продукт из Setup, добавляем count
        MyAssert.AreEqual(1 + count, _manager.Cart.Count);
    }

    [MyTest(Skip = "Ожидаем интеграции с картами")]
    public void CalculateRoute_FeatureNotReady() 
    {
        MyAssert.IsTrue(false);
    }
}
