namespace Library;

[AttributeUsage(AttributeTargets.Class)]
public class MyTestClassAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class MyTestAttribute : Attribute 
{
    public string? Skip { get; set; } 
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MyTestCaseAttribute : Attribute 
{
    public object[] Params { get; }
    public MyTestCaseAttribute(params object[] parameters) => Params = parameters;
}

[AttributeUsage(AttributeTargets.Method)]
public class MyBeforeTestAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class MyAfterTestAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class MyTestTimeoutAttribute : Attribute 
{
    public int Milliseconds { get; }
    public MyTestTimeoutAttribute(int ms) => Milliseconds = ms;
}

// Для фильтрации тестов 
[AttributeUsage(AttributeTargets.Method)]
public class MyCategoryAttribute : Attribute 
{
    public string Category { get; }
    public MyCategoryAttribute(string category) => Category = category;
}

[AttributeUsage(AttributeTargets.Method)]
public class MyPriorityAttribute : Attribute 
{
    public int Level { get; }
    public MyPriorityAttribute(int level) => Level = level;
}

// Для параметризации через итераторы 
[AttributeUsage(AttributeTargets.Method)]
public class MyMethodDataSourceAttribute : Attribute 
{
    public string MethodName { get; }
    public MyMethodDataSourceAttribute(string methodName) => MethodName = methodName;
}
