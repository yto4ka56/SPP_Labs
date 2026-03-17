using System.Reflection;
using Library;
using Tests; 

namespace Runner;

class Program
{
    static async Task Main()
    {
        var assembly = Assembly.GetAssembly(typeof(DeliveryTests));
        int passed = 0, failed = 0, skipped = 0;

        foreach (var type in assembly.GetTypes().Where(t => t.GetCustomAttribute<MyTestClassAttribute>() != null))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\nRunning: {type.Name}");
            Console.ResetColor();
            var methods = type.GetMethods();
            var setup = methods.FirstOrDefault(m => m.GetCustomAttribute<MyBeforeTestAttribute>() != null);
            var teardown = methods.FirstOrDefault(m => m.GetCustomAttribute<MyAfterTestAttribute>() != null);

            foreach (var method in methods.Where(m => m.GetCustomAttribute<MyTestAttribute>() != null))
            {
                var attr = method.GetCustomAttribute<MyTestAttribute>();
                if (!string.IsNullOrEmpty(attr?.Skip)) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [SKIP] {method.Name}: {attr.Skip}");
                    Console.ResetColor();
                    skipped++; continue;
                }

                var cases = method.GetCustomAttributes<MyTestCaseAttribute>().Select(c => c.Params).DefaultIfEmpty(null);
                foreach (var args in cases)
                {
                    var instance = Activator.CreateInstance(type);
                    try {
                        setup?.Invoke(instance, null);
                        var result = method.Invoke(instance, args);
                        if (result is Task task) await task;
                        teardown?.Invoke(instance, null);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [PASS] {method.Name}");
                        Console.ResetColor();
                        passed++;
                    }
                    catch (Exception ex) {
                        var inner = ex is TargetInvocationException ? ex.InnerException : ex;
                        string status = "FAIL";
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [{status}] {method.Name}: {inner.Message}");
                        Console.ResetColor();
                        failed++;
                    }
                }
            }
        }
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\nDone! Passed: {passed}, Failed: {failed}, Skipped: {skipped}");
        Console.ResetColor();
    }
}