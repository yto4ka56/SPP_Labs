using System.Collections.Concurrent;

namespace ThreadPool;

public sealed class CustomThreadPool : IDisposable
{
    private readonly int _minThreads;
    private readonly int _maxThreads;
    private readonly int _idleTimeoutMs;          // время простоя до завершения потока
    private readonly int _scaleUpQueueThreshold;  // порог очереди для добавления потока
    private readonly int _hangTimeoutMs;          // время после которого поток считается зависшим
    private readonly int _monitorIntervalMs;      // интервал мониторинга
    
    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private readonly Semaphore _workAvailable;  
    
    private readonly object _threadLock = new();
    private readonly List<WorkerThread> _workers = new();
    private volatile bool _disposed;
    
    private readonly Thread _monitorThread;
    private static readonly object _consoleLock = new();
    
    public int QueueLength => _queue.Count;
    public int ActiveThreadCount { get { lock (_threadLock) return _workers.Count(w => w.IsAlive); } }
    public int BusyThreadCount  { get { lock (_threadLock) return _workers.Count(w => w.IsBusy); } }

    public CustomThreadPool(
        int minThreads = 2,
        int maxThreads = 8,
        int idleTimeoutMs = 3000,
        int scaleUpQueueThreshold = 3,
        int hangTimeoutMs = 5000,
        int monitorIntervalMs = 500)
    {
        _minThreads = minThreads;
        _maxThreads = maxThreads;
        _idleTimeoutMs = idleTimeoutMs;
        _scaleUpQueueThreshold = scaleUpQueueThreshold;
        _hangTimeoutMs = hangTimeoutMs;
        _monitorIntervalMs = monitorIntervalMs;
        
        _workAvailable = new Semaphore(0, int.MaxValue);
        
        for (int i = 0; i < _minThreads; i++)
            SpawnWorker();
        
        _monitorThread = new Thread(MonitorLoop) 
        { 
            IsBackground = true, 
            Name = "PoolMonitor" 
        };
        _monitorThread.Start();
    }
    
    public void Enqueue(Action task, string name = "")
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CustomThreadPool));

        _queue.Enqueue(new WorkItem(task, name));
        _workAvailable.Release();        

        TryScaleUp();
    }

    private void TryScaleUp()
    {
        lock (_threadLock)
        {
            int active = _workers.Count(w => w.IsAlive);
            int busy = _workers.Count(w => w.IsBusy);
            int idle = active - busy;
            
            if (idle == 0 && active < _maxThreads)
            {
                SpawnWorker();
                Log($"[SCALE↑] Потоков: {active + 1} | Очередь: {_queue.Count}", ConsoleColor.Cyan);
            }
            else if (_queue.Count >= _scaleUpQueueThreshold && active < _maxThreads)
            {
                SpawnWorker();
                Log($"[SCALE↑] Рост очереди ({_queue.Count}). Потоков: {active + 1}", ConsoleColor.Cyan);
            }
        }
    }

    private void SpawnWorker()
    {
        var worker = new WorkerThread(_idleTimeoutMs, _hangTimeoutMs);
        _workers.Add(worker);

        var thread = new Thread(() => WorkerLoop(worker))
        {
            IsBackground = true,
            Name = $"Worker-{worker.Id}"
        };
        worker.Thread = thread;
        thread.Start();
    }

    private void WorkerLoop(WorkerThread worker)
    {
        while (!_disposed)
        {
            bool signaled = _workAvailable.WaitOne(_idleTimeoutMs);

            if (!signaled)
            {
                lock (_threadLock)
                {
                    int alive = _workers.Count(w => w.IsAlive);
                    if (alive > _minThreads)
                    {
                        worker.MarkDead();
                        _workers.Remove(worker);
                        Log($"[SCALE↓] Поток {worker.Id} завершён (idle). Потоков: {alive - 1}", ConsoleColor.DarkYellow);
                        return;
                    }
                }
                continue; 
            }

            if (_disposed) break;
            
            if (!_queue.TryDequeue(out var item))
                continue;

            worker.StartWork(item.Name);
            try
            {
                item.Action();
                worker.EndWork(success: true);
            }
            catch (Exception ex)
            {
                worker.EndWork(success: false);
                Log($"[ERROR] Поток {worker.Id}, задача '{item.Name}': {ex.Message}", ConsoleColor.Red);
            }
        }

        worker.MarkDead();
    }

    private void MonitorLoop()
    {
        while (!_disposed)
        {
            Thread.Sleep(_monitorIntervalMs);
            if (_disposed) break;

            int alive, busy, queue;
            List<WorkerThread> hung;

            lock (_threadLock)
            {
                _workers.RemoveAll(w => !w.IsAlive);

                alive = _workers.Count(w => w.IsAlive);
                busy  = _workers.Count(w => w.IsBusy);
                queue = _queue.Count;

                hung = _workers.Where(w => w.IsHung(_hangTimeoutMs)).ToList();
            }
            
            foreach (var h in hung)
            {
                Log($"[HUNG] Поток {h.Id} завис (>{_hangTimeoutMs}ms). Заменяем.", ConsoleColor.Magenta);
                lock (_threadLock)
                {
                    h.MarkDead();
                    _workers.Remove(h);
                    if (_workers.Count(w => w.IsAlive) < _maxThreads)
                        SpawnWorker();
                }
                h.Thread?.Interrupt();
            }

            if (queue == 0 && busy == 0)
            {
                lock (_threadLock)
                {
                    while (_workers.Count(w => w.IsAlive) > _minThreads)
                    {
                        var idle = _workers.FirstOrDefault(w => !w.IsBusy);
                        if (idle == null) break;
                        idle.MarkDead();
                        _workers.Remove(idle);
                        _workAvailable.Release(); // разбудить чтобы вышел из WaitOne
                        Log($"[SCALE↓] Плановое сжатие. Потоков: {_workers.Count(w => w.IsAlive)}", ConsoleColor.DarkYellow);
                    }
                }
            }
            
            LogStatus(alive, busy, queue);
            
            if (queue >= _scaleUpQueueThreshold)
                TryScaleUp();
        }
    }

    public void WaitAll()
    {
        while (true)
        {
            if (_queue.IsEmpty && BusyThreadCount == 0)
                return;
            Thread.Sleep(50);
        }
    }

    public static void Log(string message, ConsoleColor color = ConsoleColor.White)
    {
        lock (_consoleLock)
        {
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }

    private void LogStatus(int alive, int busy, int queue)
    {
        lock (_consoleLock)
        {
            Console.ResetColor();
            Console.Write("Потоков: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{alive}");
            Console.ResetColor();
            Console.Write(" | Занято: ");
            Console.ForegroundColor = busy > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.Write($"{busy}");
            Console.ResetColor();
            Console.Write(" | Очередь: ");
            Console.ForegroundColor = queue > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine($"{queue}\n");
            Console.ResetColor();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_threadLock)
        {
            _workAvailable.Release(_workers.Count + 1);
        }
    }
}


internal class WorkItem
{
    public Action Action { get; }
    public string Name   { get; }
    public WorkItem(Action action, string name) { Action = action; Name = name; }
}

internal class WorkerThread
{
    private static int _idCounter;
    public int Id { get; } = Interlocked.Increment(ref _idCounter);

    public Thread? Thread { get; set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsBusy { get; private set; }

    private DateTime _workStarted;
    private readonly int _idleTimeoutMs;
    private readonly int _hangTimeoutMs;

    private int _completedTasks;
    private int _failedTasks;

    public WorkerThread(int idleTimeoutMs, int hangTimeoutMs)
    {
        _idleTimeoutMs = idleTimeoutMs;
        _hangTimeoutMs = hangTimeoutMs;
    }

    public void StartWork(string taskName)
    {
        IsBusy = true;
        _workStarted = DateTime.UtcNow;
    }

    public void EndWork(bool success)
    {
        if (success) 
            Interlocked.Increment(ref _completedTasks);
        else 
            Interlocked.Increment(ref _failedTasks);
        IsBusy = false;
    }

    public bool IsHung(int hangMs) =>
        IsBusy && (DateTime.UtcNow - _workStarted).TotalMilliseconds > hangMs;

    public void MarkDead() { IsAlive = false; IsBusy = false; }
}
