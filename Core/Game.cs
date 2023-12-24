using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("browser")]

namespace karesz.Core
{
    public class Game
    {
        static readonly Barrier Bar = new Barrier(2, (b) => { Console.WriteLine(b.CurrentPhaseNumber.ToString()); });

        public static void Run()
        {
             // Parallel.Invoke(action, action2);

            new Thread(new ThreadStart(SecondThread)).Start();
            Console.WriteLine("thread {0} has been called", Thread.CurrentThread.ManagedThreadId);

        }

        static void SecondThread()
        {
            Console.WriteLine($"Hello from Thread {Thread.CurrentThread.ManagedThreadId}");
            for (int i = 0; i < 5; ++i)
            {
                Console.WriteLine($"Ping {i}");
                Thread.Sleep(1000);
            }
        }

        //static Action action = () =>
        //{
        //    for (int i = 0; i < 3; i++)
        //    {
        //        Bar.SignalAndWait();
        //        Console.WriteLine("shit doned");
        //    }
        //};

        //static Action action2 = () =>
        //{
        //    for (int i = 0; i < 3; i++)
        //    {
        //        Bar.SignalAndWait();
        //        Console.WriteLine("shit doned on 2");
        //    }
        //};

    }
}

