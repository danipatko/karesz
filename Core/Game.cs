namespace karesz.Core
{
    public class Game
    {
        public static void Run()
        {
            Console.WriteLine("thread {0} has been called", Thread.CurrentThread.ManagedThreadId);
        }
    }
}

