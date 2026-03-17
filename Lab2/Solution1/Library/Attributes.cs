namespace Library;

[AttributeUsage(AttributeTargets.Class)]
public class MyTestClassAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class MyTestAttribute : Attribute 
{
    public string Skip { get; set; } 
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