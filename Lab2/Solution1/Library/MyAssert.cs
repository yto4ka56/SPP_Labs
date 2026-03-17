using System.Collections;


namespace Library;

public class MyTestFailedException : Exception 
{
    public MyTestFailedException(string message) : base(message) { }
}

public static class MyAssert 
{
    public static void AreEqual(object exp, object act)
    {
        if (!Equals(exp, act)) throw new MyTestFailedException($"Expected {exp}, got {act}");
    }

    public static void AreNotEqual(object v1, object v2)
    {
        if (Equals(v1, v2)) throw new MyTestFailedException($"Values are equal: {v1}");
    }

    public static void IsTrue(bool cond)
    {
        if (!cond) throw new MyTestFailedException("Expected True");
    }

    public static void IsFalse(bool cond)
    {
        if (cond) throw new MyTestFailedException("Expected False");
    }

    public static void IsNull(object obj)
    {
        if (obj != null) throw new MyTestFailedException("Expected null");
    }

    public static void IsNotNull(object obj)
    {
        if (obj == null) throw new MyTestFailedException("Expected not null");
    }

    public static void IsNotEmpty(IEnumerable coll)
    {
        if (coll == null || !coll.Cast<object>().Any()) throw new MyTestFailedException("Collection empty");
    }
    public static void Contains(object item, IEnumerable coll) { 
        if (coll == null || !coll.Cast<object>().Contains(item)) throw new MyTestFailedException($"Item {item} not found"); 
    }

    public static void IsInstanceOf<T>(object obj)
    {
        if (!(obj is T)) throw new MyTestFailedException($"Not instance of {typeof(T).Name}");
    }
    public static void Throws<T>(Action action) where T : Exception {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new MyTestFailedException($"Wrong exception: {ex.GetType().Name}");
        }
        throw new MyTestFailedException($"No exception thrown");
    }
}