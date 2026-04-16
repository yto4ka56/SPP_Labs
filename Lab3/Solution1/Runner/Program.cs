using System.Reflection;
using System.Diagnostics;
using Library;
using Tests;
using MyThreadPool;

namespace Runner;

class Program
{
    private static readonly object _consoleLock = new();

    private static int _passed  = 0;
    private static int _failed  = 0;
    private static int _skipped = 0;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        var assembly    = Assembly.GetAssembly(typeof(DeliveryTests))!;
        var testItems   = CollectTests(assembly);
        
        Console.WriteLine($"Всего тестов: {testItems.Count}");
        Console.WriteLine("Настройки пула: min=2  max=8  idle=3000ms  hangTimeout=5000ms\n");
        
        using var pool = new CustomThreadPool(
            minThreads:           2,
            maxThreads:           8,
            idleTimeoutMs:        3000,
            scaleUpQueueThreshold: 3,
            hangTimeoutMs:        5000,
            monitorIntervalMs:    500
        );

        var sw = Stopwatch.StartNew();

        CustomThreadPool.Log("═══ ФАЗА 1: Одиночные подачи ═══", ConsoleColor.Magenta);
        for (int i = 0; i < Math.Min(5, testItems.Count); i++)
        {
            EnqueueTest(pool, testItems[i]);
            Thread.Sleep(300);
        }

        pool.WaitAll();

        CustomThreadPool.Log("\n═══ ФАЗА 2: Пиковая нагрузка (все тесты) ═══", ConsoleColor.Magenta);
        foreach (var item in testItems)
            EnqueueTest(pool, item);

        pool.WaitAll();

        CustomThreadPool.Log("\n═══ ФАЗА 3: Пауза (пул сжимается) ═══", ConsoleColor.Magenta);
        Thread.Sleep(4000);  // дольше idle timeout — пул должен сжаться до минимума

        CustomThreadPool.Log("\n═══ ФАЗА 4: Повторный пик нагрузки ═══", ConsoleColor.Magenta);
        foreach (var item in testItems)
            EnqueueTest(pool, item);

        pool.WaitAll();

        CustomThreadPool.Log("\n═══ ФАЗА 5: Единичные подачи с интервалами ═══", ConsoleColor.Magenta);
        foreach (var item in testItems.Take(10))
        {
            EnqueueTest(pool, item);
            Thread.Sleep(200);
        }

        pool.WaitAll();
        sw.Stop();
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  ПРОЙДЕНО: {_passed,4}  ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"ПРОВАЛЕНО: {_failed,4}  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"ПРОПУЩЕНО: {_skipped,4}");
        Console.ResetColor();
        Console.WriteLine($"  Время выполнения: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Активных потоков в конце: {pool.ActiveThreadCount}");
        Console.WriteLine("═══════════════════════════════════════════════════════");
    }
    

    static void EnqueueTest(CustomThreadPool pool, TestItem item)
    {
        pool.Enqueue(() => RunSingleTest(item), item.DisplayName);
    }

    static void RunSingleTest(TestItem item)
    {
        var testAttr = item.Method.GetCustomAttribute<MyTestAttribute>()!;

        // Skip
        if (!string.IsNullOrEmpty(testAttr.Skip))
        {
            LogResult(item.DisplayName, "SKIP", ConsoleColor.Yellow, testAttr.Skip);
            Interlocked.Increment(ref _skipped);
            return;
        }

        var instance = Activator.CreateInstance(item.Type)!;
        var setup= item.Type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyBeforeTestAttribute>() != null);
        var teardown= item.Type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<MyAfterTestAttribute>() != null);
        var timeout = item.Method.GetCustomAttribute<MyTestTimeoutAttribute>();

        try
        {
            setup?.Invoke(instance, null);

            if (timeout != null)
            {
                // Запускаем тест в отдельном Thread с учётом таймаута
                // (без Task.Run — используем Thread напрямую)
                Exception? testEx = null;
                bool completed = false;
                var taskThread = new Thread(() =>
                {
                    try
                    {
                        var res = item.Method.Invoke(instance, item.Args);
                        if (res is System.Threading.Tasks.Task t)
                            t.GetAwaiter().GetResult();
                        completed = true;
                    }
                    catch (Exception ex)
                    {
                        testEx = ex.InnerException ?? ex;
                    }
                });
                taskThread.Start();
                bool finished = taskThread.Join(timeout.Milliseconds);

                if (!finished)
                {
                    taskThread.Interrupt();
                    throw new Exception($"Timeout ({timeout.Milliseconds}ms)");
                }

                if (testEx != null) throw testEx;
                if (!completed) throw new Exception("Тест не завершился");
            }
            else
            {
                var res = item.Method.Invoke(instance, item.Args);
                if (res is System.Threading.Tasks.Task t)
                    t.GetAwaiter().GetResult();
            }

            teardown?.Invoke(instance, null);

            LogResult(item.DisplayName, "PASS", ConsoleColor.Green);
            Interlocked.Increment(ref _passed);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            LogResult(item.DisplayName, "FAIL", ConsoleColor.Red, inner.Message);
            Interlocked.Increment(ref _failed);
        }
    }

    static List<TestItem> CollectTests(Assembly assembly)
    {
        var list = new List<TestItem>();
        foreach (var type in assembly.GetTypes().Where(t => t.GetCustomAttribute<MyTestClassAttribute>() != null))
        {
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<MyTestAttribute>() != null))
            {
                var cases = method.GetCustomAttributes<MyTestCaseAttribute>()
                                  .Select(c => c.Params)
                                  .DefaultIfEmpty(null)
                                  .ToList();
                foreach (var args in cases)
                {
                    string displayName = args == null
                        ? method.Name
                        : $"{method.Name}({string.Join(", ", args)})";
                    list.Add(new TestItem(type, method, args, displayName));
                }
            }
        }
        return list;
    }

    static void LogResult(string name, string status, ConsoleColor color, string msg = "")
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = color;
            Console.Write($"  [{status}] ");
            Console.ResetColor();
            Console.Write(name);
            if (!string.IsNullOrEmpty(msg))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" → {msg}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
}

record TestItem(Type Type, MethodInfo Method, object[]? Args, string DisplayName);
