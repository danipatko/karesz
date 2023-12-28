using Microsoft.VisualStudio.Threading;

namespace karesz.Core
{
    public class Game
    {
        public static async Task Run()
        {
            // proof of concept
            Console.WriteLine("thread {0} has been called", Thread.CurrentThread.ManagedThreadId);

            var resetEvent = new AsyncManualResetEvent(false);

            _ = Task.Run(async () =>
            {
                Console.WriteLine("Waiting for parent task");
                for (int i = 0; i < 3; i++)
                {
                    await resetEvent.WaitAsync();
                    Console.WriteLine("Task #1 at {0}", i);
                }
                Console.WriteLine("Task #1 finished");
            });

            _ = Task.Run(async () =>
            {
                Console.WriteLine("Waiting for parent task");
                for (int i = 0; i < 3; i++)
                {
                    await resetEvent.WaitAsync();
                    Console.WriteLine("Task #2 at {0}", i);
                }
                Console.WriteLine("Task #2 finished");
            });

            
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine("--- waiting a sec");

                await Task.Delay(1000);
                resetEvent.Set();   // allow the tasks to complete their job

                Console.WriteLine("--- released");
                resetEvent.Reset();
                Console.WriteLine("--- reset");
            }
        }
    }
}

