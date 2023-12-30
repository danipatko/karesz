using Microsoft.VisualStudio.Threading;

namespace karesz.Core
{
    public class Game
    {
        public static async Task Run()
        {
            var karesz = Robot.Create("karesz");
            karesz.Feladat = async delegate ()
            {
                while (true)
                {
                    for (var i = 0; i < 3; i++)
                        await karesz.LépjAsync();
                    await karesz.ForduljAsync(1);
                } 
            };

            var cts = new CancellationTokenSource();

            _ = Robot.RunAsync(cts.Token);

            await Task.Delay(2500);
            await Console.Out.WriteLineAsync("cancelling now");
            await cts.CancelAsync();

            // proof of concept
            //Console.WriteLine("thread {0} has been called", Thread.CurrentThread.ManagedThreadId);

            //var resetEvent = new AsyncManualResetEvent(false);

            //_ = Task.Run(async () =>
            //{
            //    Console.WriteLine("Waiting for parent task");
            //    for (int i = 0; i < 3; i++)
            //    {
            //       await resetEvent.WaitAsync();
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
                
            //    await Task.Delay(1000);
            //    resetEvent.PulseAll();

            //    Console.WriteLine("--- reset");
            //}
        }
    }

    // "multiplayer"
    public partial class Robot
    {
        public static Robot Create(string név)
        {
            var r = new Robot(név);
            Robots.Add(név, r);
            return r;
        }

        private static int TickCount = 0;

        private const int TICK_INTERVAL = 200;

        // used for signalling & waiting
        private static readonly AsyncManualResetEvent resetEvent = new(false);

        private static readonly Dictionary<string, Robot> Robots = [];

        private static readonly Level CurrentLevel = Level.Default;

        // throws an exception if name is not present in dictionary
        public static Robot Get(string név) => Robots[név]!;

        private static bool IsPositionOccupied(Vector position) => Robots.Any(x => x.Value.CurrentPosition.Vector == position);

        public static void MakeRound()
        {
            // move projectiles
            Projectile.TickAll();

            // remove killed robots
            foreach (string name in RobotsToExecute().Distinct())
                Kill(name);

            // step survivors
            foreach (Robot robot in Robots.Values)
                robot.CurrentPosition = robot.ProposedPosition;

            TickCount++;
        }

        private static void Kill(string name)
        {
            if (Robots.Remove(name, out var robot))
                CurrentLevel[robot.Position.Vector] = Level.Tile.Black;
        }

        private static IEnumerable<string> RobotsToExecute()
        {
            foreach (var robot in Robots.Values)
            {
                // steps into wall
                if (CurrentLevel[robot.ProposedPosition.Vector] == Level.Tile.Wall)
                    yield return robot.Név;
                // out of bounds
                if (!CurrentLevel.InBounds(robot.ProposedPosition.Vector))
                    yield return robot.Név;
                // stepping on the same field or stepping over one another
                if (Robots.Values.Any(other => other.Név != robot.Név 
                    && (robot.ProposedPosition.Vector == other.ProposedPosition.Vector 
                    || (robot.ProposedPosition.Vector == other.Position.Vector && other.ProposedPosition.Vector == robot.Position.Vector))))
                    yield return robot.Név;
                // hit by projectile
                if (Projectile.Projectiles.Any(x => x.CurrentPosition.Vector == robot.ProposedPosition.Vector))
                    yield return robot.Név;
            }
        }

        public static async Task RunAsync(CancellationToken cancellationToken)
        {
            foreach (var robot in Robots.Values)
            {
                _ = Task.Run(robot.Feladat.Invoke, cancellationToken);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                // block Tick() calls again
                resetEvent.Reset();
                // time for robot tasks to block again
                await Task.Delay(TICK_INTERVAL, cancellationToken);
                // run multiplayer logic
                MakeRound();
                // unblock Tick() calls
                resetEvent.Set();
                Console.WriteLine(string.Join(", ", Robots.Values));
            }
        }
    }
}
