using Microsoft.VisualStudio.Threading;
using karesz.Runner;
using Microsoft.FluentUI.AspNetCore.Components.DesignTokens;
using karesz.Components;

namespace karesz.Core
{
    public class Game
    {
		public static Robot.RenderCallback? RenderFunction { get; set; }

        /// <summary>
        /// Starts game
        /// </summary>
		public static async Task StartAsync(CancellationToken cancellationToken = default)
        {
			if (RenderFunction == null)
                throw new Exception("No render callback was specified for Game.RunAsync!");

            Robot.Cleanup();

			// await Output.StartCaptureAsync();
            Output.WriteLine("--- CONSOLE OUTPUT ---");

            var result = await CompilerSerivce.CompileAsync(WorkspaceService.Code, CompilerSerivce.CompilationMode.Async, cancellationToken);
            if(!result.Success)
            {
				Output.WriteLine("--- COMPILATION FAILED ---");
				Output.WriteLine(string.Join("\n", result.Diagnostics.Select(DiagnosticsProvider.FmtMessage)));
				await Output.ResetCaptureAsync();
				return;
            }

            var invokeSuccess = CompilerSerivce.LoadAndInvoke(); // will write error to stdout
            if(!invokeSuccess)
            {
				Output.WriteLine($"--- INVOKE FAILED ---");
				await Output.ResetCaptureAsync();
				return;
			}

			Output.WriteLine("--- INVOKE FINISHED ---");

            await Robot.RunAsync(RenderFunction, autoCleanup: false, cancellationToken: cancellationToken);

			Output.WriteLine("--- GAME ENDED ---");

			// await Output.ResetCaptureAsync();
		}
    }

    // basic utility class because we don't want to expose robot instance position
    public struct RobotInfo(string Name, Position Position)
    {
        public string Name { get; set; } = Name;
        public Position Position { get; set; } = Position;
        public readonly bool IsEmpty { get => string.IsNullOrEmpty(Name); }
		public readonly override string ToString() => $"'{Name}' at {Position}";
	}

    // "multiplayer"
    public partial class Robot
    {
        #region Params

        public delegate Task RenderCallback(RobotInfo[] positions, (int x, int y, Level.Tile tile)[]? tiles);

		// TODO: move to settings
		private const int TICK_INTERVAL = 50; // ms

		// game state
		private static readonly Dictionary<string, Robot> Robots = [];

        public static Level CurrentLevel { get; set; } = Level.Default;

		private static bool IsRunning = false;
		private static bool DidChangeMap = false;
		private static int TickCount = 0;

        // used for signalling & waiting
        private static readonly AsyncManualResetEvent resetEvent = new(false);
		private static CancellationToken CancellationToken = CancellationToken.None;
		
        // current state for rendering
        private static RobotInfo[] StatusQuo { get => Robots.Values.Select(x => new RobotInfo(x.Név, x.Position)).ToArray(); }

        #endregion

        #region Robot utils

        // throws an exception if name is not present in dictionary
        public static Robot Get(string név) => Robots[név]!;

        public static Robot Create(string név, int startX = 0, int startY = 0, Direction startRotation = Direction.Up)
		{
			if (Robots.TryGetValue(név, out var robot))
			{
				Console.Error.WriteLine($"{név} robot már létezik, nem lesz új robot létrehozva.");
				robot.Position = new Position(startX, startY, startRotation);
                return robot;
			}

			var r = new Robot(név) { CurrentPosition = new Position(startX, startY, startRotation) };
            Console.WriteLine(r);
            
            Robots.Add(név, r);
			return r;
		}

        /// <summary>
        /// Overload for modal result
        /// </summary>
        public static RobotInfo[] Create(KareszModal.KareszData record)
        {
            Console.WriteLine("Create overload {0}", record);
            Create(record.Name, record.X, record.Y, record.Direction);
            return StatusQuo;
        }

        /// <summary>
        /// Sets the position of a robot, by name
        /// won't take effect while game is running
        /// </summary>
        public static RobotInfo[] PlaceAt(string name, int x, int y)
        {
            // invalid call
            if (IsRunning)
                return StatusQuo;
            
            if (Robots.TryGetValue(name, out var robot))
                robot.CurrentPosition.Vector = new Vector(x, y);

            return StatusQuo;
        }

        public static RobotInfo[] Move(string name, RelativeDirection direction = RelativeDirection.Right)
        {
			if (IsRunning)
				return StatusQuo;

            if (Robots.TryGetValue(name, out var robot))
                robot.CurrentPosition += direction;

			return StatusQuo;
		}

		#endregion

		#region Game lifecycle

		/// <summary>
		/// List all robot objects that are about to die in this round
		/// </summary>
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

        /// <summary>
        /// Perform a round in game
        /// </summary>
		private static void MakeRound()
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
		}


        /// <summary>
        /// Runs the entire game...
        /// </summary>
        /// <param name="render">Callback delegate that gets the robot positions and stone list (if changed).</param>
        /// <param name="autoCleanup">Call Cleanup after finishing or cancellation</param>
        /// <param name="cancellationToken"></param>
		public static async Task RunAsync(RenderCallback render, bool autoCleanup = false, CancellationToken cancellationToken = default)
        {
			// render before start
            await render.Invoke(StatusQuo, CurrentLevel.Enumerate().ToArray());

			var cts = new CancellationTokenSource();
            // create a combined token so token can be cancelled after the while loop
            var cct = cts.Token.CombineWith(cancellationToken);
            // make the combined CancellationToken available to async tick() calls
            // cancelling Task.Run will not dispose the task it is running, just request a cancellation
            // if the task is blocking at a resetEvent.WaitOne() call, the task will run indefinitely
            CancellationToken = cct.Token;

            // run cleanup on cancel
			if (autoCleanup)
            {
				CancellationToken.Register(() =>
                {
					Cleanup();
                    _ = render.Invoke(StatusQuo, CurrentLevel.Enumerate().ToArray());
                });
            }

		    List<Task> runningTasks = [];
            foreach (var robot in Robots.Values)
            {
                var task = Task.Run(robot.FeladatAsync.Invoke, cct.Token);
                runningTasks.Add(task);
            }

            while (!CancellationToken.IsCancellationRequested && Robots.Count > 0)
            {
                // block Tick() calls again
                resetEvent.Reset();

                // time for robot tasks to block again
                await Task.Delay(TICK_INTERVAL, CancellationToken);

                // run multiplayer logic
                MakeRound();

				// trigger UI render
				await render.Invoke(StatusQuo, DidChangeMap ? CurrentLevel.Enumerate().ToArray() : null);
				DidChangeMap = false;

				// unblock Tick() calls
				resetEvent.Set();

                // DEBUG
                //Console.WriteLine("--- ROUND {0} ---\n{1}\n---", TickCount, string.Join(", ", Robots.Values));
            }

			// tasks may be still running, block them!
			resetEvent.Reset();

			// still need to cancel
			if (!CancellationToken.IsCancellationRequested)
                await cts.CancelAsync();

			foreach (var item in runningTasks)
            {
                if (item.Status != TaskStatus.WaitingForActivation)
                    item.Dispose();
                else
                    await Console.Error.WriteLineAsync("A task is still running... well fuck");
            }
		}

        /// <summary>
        /// Remove a robot from players and place a black rock to the place of death
        /// </summary>
		private static void Kill(string name)
		{
			if (Robots.Remove(name, out var robot))
				CurrentLevel[robot.Position.Vector] = Level.Tile.Black;
		}

		/// <summary>
        /// Resets tick counter, removes all rocks placed in the run.
        /// If removeRobots is true, every player's robot will be deleted.
        /// </summary>
        public static (int x, int y, Level.Tile tile)[] Cleanup(bool removeRobots = false)
		{
			if (removeRobots)
				Robots.Clear();

			CurrentLevel.Reset();
			TickCount = 0;

			return CurrentLevel.Enumerate().ToArray();
		}

		#endregion
	}
}
