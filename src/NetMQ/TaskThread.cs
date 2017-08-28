#if UAP
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace System.Threading
{
    internal delegate void ParameterizedThreadStart(object obj);
    internal delegate void ThreadStart();

    internal class ThreadStateException : Exception
    {
        public ThreadStateException(string message = "", Exception innerException = null)
            : base(message, innerException)
        { }
    }

    internal class Thread
    {
        private Task _task;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public string Name { get; set; }
        public bool IsBackground { get; set; }
        public bool IsAlive => _task != null && !(_task.IsCanceled || _task.IsCompleted || _task.IsFaulted);
        public CultureInfo CurrentCulture => throw new NotImplementedException();
        private static SemaphoreSlim _unavailable = new SemaphoreSlim(0, 1);

        private enum StartType
        {
            Standard,
            Parameterized
        };

        StartType _startType;
        ParameterizedThreadStart _parameterizedStart;
        ThreadStart _start;

        public Thread(ParameterizedThreadStart threadStart, int maxStackSize = 0)
        {
            _startType = StartType.Parameterized;
            _parameterizedStart = threadStart;
        }

        public Thread(ThreadStart threadStart, int maxStackSize = 0)
        {
            _startType = StartType.Standard;
            _start = threadStart;
        }

        public void Start()
        {
            if (_startType == StartType.Parameterized)
            {
                throw new InvalidOperationException("Must supply argument for ParameterizedThreadStart!");
            }

            if (_task != null)
            {
                throw new ThreadStateException("Thread already started!");
            }

            _task = new Task(() => _start(), _tokenSource.Token, TaskCreationOptions.LongRunning);
            _task.Start();
        }

        public void Start(object obj)
        {
            if (_startType == StartType.Standard)
            {
                throw new InvalidOperationException("Must use parameterless Start() method instead!");
            }

            if (_task != null)
            {
                throw new ThreadStateException("Thread already started!");
            }

            _task = new Task(() => _parameterizedStart(obj), _tokenSource.Token, TaskCreationOptions.LongRunning);
            _task.Start();
        }

        public void Join()
        {
            _task.Wait();
        }

        public bool Join(Int32 milliseconds)
        {
            return _task.Wait(milliseconds);
        }

        public bool Join(TimeSpan timeout)
        {
            return _task.Wait(timeout);
        }

        public static void Sleep(int milliseconds)
        {
            _unavailable.Wait(milliseconds);
        }

        public static void Sleep(TimeSpan duration)
        {
            _unavailable.Wait(duration);
        }
    }
}
#endif
