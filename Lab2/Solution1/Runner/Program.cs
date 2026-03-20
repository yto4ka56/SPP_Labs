using System.Reflection;
using Library;
using Tests; 
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;


namespace Runner;

class Program
{
    private static readonly object _consoleLock = new object();
    private static int _maxParallelism = 3; 
    
    private static int _passed = 0;
    private static int _failed = 0;
    private static int _skipped = 0;

    static async Task Main()
    {
        var assembly = Assembly.GetAssembly(typeof(DeliveryTests));
        var testMethods = new List<(MethodInfo method, Type type, object[] args)>();

        foreach (var type in assembly.GetTypes().Where(t => t.GetCustomAttribute<MyTestClassAttribute>() != null))
        {
            var methods = type.GetMethods().Where(m => m.GetCustomAttribute<MyTestAttribute>() != null);
            foreach (var m in methods)
            {
                var cases = m.GetCustomAttributes<MyTestCaseAttribute>().Select(c => c.Params).DefaultIfEmpty(null);
                foreach (var args in cases) 
                    testMethods.Add((m, type, args));
            }
        }
        
        Console.WriteLine($"Параллелизм: {_maxParallelism} | Всего тестов: {testMethods.Count}\n");
        
        var sw = Stopwatch.StartNew();
        using var semaphore = new SemaphoreSlim(_maxParallelism); 
        var tasks = testMethods.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await RunSingleTestAsync(item.method, item.type, item.args);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();
        
        Console.WriteLine($"Пройдено: {_passed}, Провалено: {_failed}, Пропущено: {_skipped}");
        Console.WriteLine($"Время выполнения: {sw.ElapsedMilliseconds} мс.");
    }

    static async Task RunSingleTestAsync(MethodInfo method, Type type, object[] args)
    {
        var testAttr = method.GetCustomAttribute<MyTestAttribute>();
        
        if (!string.IsNullOrEmpty(testAttr?.Skip))
        {
            LogResult(method.Name, "SKIP", ConsoleColor.Yellow, testAttr.Skip);
            Interlocked.Increment(ref _skipped); 
            return;
        }

        var instance = Activator.CreateInstance(type);
        var setup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyBeforeTestAttribute>() != null);
        var teardown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyAfterTestAttribute>() != null);
        var timeoutAttr = method.GetCustomAttribute<MyTestTimeoutAttribute>();

        try
        {
            setup?.Invoke(instance, null);
            
            using var cts = new CancellationTokenSource();
            Task testTask = Task.Run(() => {
                var result = method.Invoke(instance, args);
                if (result is Task t) t.GetAwaiter().GetResult();
            }, cts.Token);

            if (timeoutAttr != null)
            {
                if (await Task.WhenAny(testTask, Task.Delay(timeoutAttr.Milliseconds)) != testTask)
                {
                    cts.Cancel(); 
                    throw new Exception($"Timeout ({timeoutAttr.Milliseconds}ms)");
                }
            }
            
            await testTask;
            teardown?.Invoke(instance, null);
            
            LogResult(method.Name, "PASS", ConsoleColor.Green);
            Interlocked.Increment(ref _passed);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            LogResult(method.Name, "FAIL", ConsoleColor.Red, inner.Message);
            Interlocked.Increment(ref _failed);
        }
    }

    static void LogResult(string name, string status, ConsoleColor color, string msg = "")
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.Write($"[{status}] ");
            Console.ResetColor();
            Console.WriteLine($"{name} {(string.IsNullOrEmpty(msg) ? "" : "-> " + msg)}");
        }
    }
}