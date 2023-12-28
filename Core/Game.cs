using Microsoft.VisualStudio.Threading;

namespace karesz.Core
{
    public class Game
    {
        public static async Task Run()
        {
            await Console.Out.WriteLineAsync("hello moderator");

            var karesz = Robot.Create("karesz");
            var karoly = Robot.Create("karoly");
            karesz.Teleport(10, 10);
            karoly.Teleport(12, 10);
            Robot.MakeRound();
            
            karesz.Fordulj(1);
            karoly.Fordulj(-1);
            Robot.MakeRound();

            await Console.Out.WriteLineAsync("here");

            Console.WriteLine(karesz.ToString());
            Console.WriteLine(karoly.ToString());

            //await Console.Out.WriteLineAsync(string.Join(", ", Robot.Robots.Values));

            //var asm = Assembly.Load(CompilerSerivce.AssemblyBytes);

            //// proof of concept
            //Console.WriteLine("thread {0} has been called", Thread.CurrentThread.ManagedThreadId);

            //var resetEvent = new AsyncManualResetEvent(false);

            //_ = Task.Run(async () =>
            //{
            //    Console.WriteLine("Waiting for parent task");
            //    for (int i = 0; i < 3; i++)
            //    {
            //        await resetEvent.WaitAsync();
            //        Console.WriteLine("Task #1 at {0}", i);
            //    }
            //    Console.WriteLine("Task #1 finished");
            //});

            //_ = Task.Run(async () =>
            //{
            //    Console.WriteLine("Waiting for parent task");
            //    for (int i = 0; i < 3; i++)
            //    {
            //        await resetEvent.WaitAsync();
            //        Console.WriteLine("Task #2 at {0}", i);
            //    }
            //    Console.WriteLine("Task #2 finished");
            //});


            //for (int i = 0; i < 3; i++)
            //{
            //    Console.WriteLine("--- waiting a sec");

            //    await Task.Delay(100);
            //    resetEvent.Set();   // allow the tasks to complete their job

            //    Console.WriteLine("--- released");
            //    resetEvent.Reset();
            //    Console.WriteLine("--- reset");
            //}
        }
    }
}

