using Microsoft.VisualStudio.Threading;
using karesz.Runner;

namespace karesz.Core
{
    public class Game
    {
		public static Robot.RenderCallback? RenderFunction { get; set; }

        private static int Counter = 0;

        public static async Task Test()
        {
            var karesz = Robot.Create("Karesz");

            

			karesz.FeladatAsync = async delegate ()
            {
                Console.WriteLine("FELADAT SERIAL {0}", Counter);
                await karesz.ForduljAsync(1);
				while (!karesz.Ki_fog_lépni_a_pályáról())
				{
					await karesz.LépjAsync();
					await karesz.Tegyél_le_egy_kavicsotAsync();

					// Console.Error.WriteLine("looped");
				}
			};
            Counter++;
        }

		public static async Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (RenderFunction == null)
                throw new Exception("No render callback was specified for Game.RunAsync!");

            Robot.Cleanup(true);
			await RenderFunction.Invoke([], []);

			await Test();
            await Robot.RunAsync(RenderFunction, cancellationToken);

			return;

            // cleanup and rerender
            Robot.Cleanup(true);
            await RenderFunction.Invoke([], []);

			await Output.StartCaptureAsync();
            Output.WriteLine("--- CONSOLE OUTPUT ---");

            var result = await CompilerSerivce.CompileAsync(WorkspaceService.Code, CompilerSerivce.CompilationMode.Async, cancellationToken);

            if(!result.Success)
            {
				Output.WriteLine("--- COMPILATION FAILED ---");
				Output.WriteLine(string.Join("\n", result.Diagnostics.Select(DiagnosticsProvider.FmtMessage)));
				await Output.ResetCaptureAsync();
				return;
            }

            CompilerSerivce.LoadAndInvoke();

            Output.WriteLine("--- INVOKE FINISHED ---");

            await Robot.RunAsync(RenderFunction, cancellationToken)
                .ContinueWith(async _ => await Output.ResetCaptureAsync());
		}
    }

    // "multiplayer"
    public partial class Robot
    {
        private static bool DidChangeMap = false;

        public static Robot Create(string név)
        {
            if (Robots.TryGetValue(név, out var robot))
            {
                Console.Error.WriteLine($"{név} robot már létezik, nem lesz új robot létrehozva.");
                return robot;
            }

            var r = new Robot(név);
            Robots.Add(név, r);
            return r;
        }

        public static void Cleanup(bool removeAll = false)
		{
            if (removeAll)
                Robots.Clear();

			Console.WriteLine("before {0}", string.Join("\n", CurrentLevel.Enumerate()));
			CurrentLevel = Level.Reset();
            Console.WriteLine("after {0}", string.Join("\n", CurrentLevel.Enumerate()));
            TickCount = 0;
		}

		private static int TickCount = 0;

        private const int TICK_INTERVAL = 100; // ms

        // used for signalling & waiting
        private static readonly AsyncManualResetEvent resetEvent = new(false);

        private static readonly Dictionary<string, Robot> Robots = [];

        public static Level CurrentLevel { get; set; } = Level.Default;

        // throws an exception if name is not present in dictionary
        public static Robot Get(string név) => Robots[név]!;

        private static bool IsPositionOccupied(Vector position) => Robots.Any(x => x.Value.CurrentPosition.Vector == position);

        public delegate Task RenderCallback(Position[] positions, (int x, int y, Level.Tile tile)[]? tiles);

		public static async Task MakeRoundAsync(RenderCallback render)
        {
            // move projectiles
            Projectile.TickAll();

            // remove killed robots
            foreach (string name in RobotsToExecute().Distinct())
            {
                Kill(name);
                Output.WriteLine($"[{name}] died");
            }

            // step survivors
            foreach (Robot robot in Robots.Values)
                robot.CurrentPosition = robot.ProposedPosition;

			TickCount++;

            // trigger UI render
            await render.Invoke(Robots.Values.Select(x => x.Position).ToArray(), DidChangeMap ? CurrentLevel.Enumerate().ToArray() : null);
            DidChangeMap = false;
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

        public static async Task RunAsync(RenderCallback render, CancellationToken cancellationToken)
        {
            foreach (var robot in Robots.Values)
            {
 				_ = Task.Run(new FeladatAction(robot.FeladatAsync).Invoke, cancellationToken);
            }

            while (!cancellationToken.IsCancellationRequested && Robots.Count > 0)
            {
                // block Tick() calls again
                resetEvent.Reset();
                // time for robot tasks to block again
                await Task.Delay(TICK_INTERVAL, cancellationToken);
                // run multiplayer logic
                await MakeRoundAsync(render);
                // unblock Tick() calls
                resetEvent.Set();
                // DEBUG
                Console.WriteLine("--- ROUND {0} ---\n{1}\n---", TickCount, string.Join(", ", Robots.Values));
            }
        }
    }
}
