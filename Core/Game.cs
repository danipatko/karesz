using Microsoft.VisualStudio.Threading;
using karesz.Components;
using karesz.Runner;

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

			await Output.StartCaptureAsync();
			Console.WriteLine("--- CONSOLE OUTPUT ---");

            var result = await CompilerSerivce.CompileAsync(WorkspaceService.Code, CompilerSerivce.CompilationMode.Async, cancellationToken);
            if(!result.Success)
            {
				Console.WriteLine("--- COMPILATION FAILED ---");
				Console.WriteLine(string.Join("\n", result.Diagnostics.Select(DiagnosticsProvider.FmtMessage)));
				await Output.ResetCaptureAsync();
				return;
            }

            var invokeSuccess = CompilerSerivce.LoadAndInvoke(); // will write error to stdout
            if(!invokeSuccess)
            {
				Console.WriteLine($"--- INVOKE FAILED ---");
				await Output.ResetCaptureAsync();
				return;
			}

            // invoke plugins (if any)
            Plugin.Get(Robot.CurrentLevel.LevelName)?.TANAR_ROBOTJAI();

			await Robot.RunAsync(RenderFunction, autoCleanup: false, cancellationToken: cancellationToken)
				.ContinueWith(async (_) =>
				{
					Console.WriteLine("--- GAME ENDED ---");
					await Output.ResetCaptureAsync();
				}, TaskScheduler.Default);
		}
    }

	// basic utility class because we don't want to expose robot instance position
	public struct RobotInfo(string Name, Position Position, bool alt, int[] Stones)
	{
		public string Name { get; set; } = Name;
		public bool Alt { get; set; } = alt;
		public Position Position { get; set; } = Position;
        public int[] Stones { get; set; } = Stones;
        public readonly bool IsEmpty { get => string.IsNullOrEmpty(Name); }
		public readonly override string ToString() => $"'{Name}' at {Position}";
	}

	public record RenderUpdate(RobotInfo[] Robots, Position[]? Projectiles = null, (int x, int y, Level.Tile tile)[]? Tiles = null)
	{
		public RobotInfo[] Robots { get; set; } = Robots;
		public Position[]? Projectiles { get; set; } = Projectiles;
		public (int x, int y, Level.Tile tile)[]? Tiles { get; set; } = Tiles;
	}

	// "multiplayer"
	public partial class Robot
    {
        #region Params

        public delegate Task RenderCallback(RenderUpdate data);

		// TODO: move to settings
		private const int TICK_INTERVAL = 50; // ms

		// game state
		private static readonly Dictionary<string, Robot> Robots = [];

        public static Level CurrentLevel { get; set; } = Level.Default;

		public static bool IsRunning { get; private set; } = false;
		private static bool DidChangeMap = false;
		private static int TickCount = 0;

        // used for signalling & waiting
        private static readonly AsyncManualResetEvent resetEvent = new(false);

		#endregion

		#region State getters

		private static RobotInfo[] StatusQuo { get => Robots.Values.Select(x => new RobotInfo(x.Név, x.Position, x.Alt, x.Stones)).ToArray(); }
		private static (int x, int y, Level.Tile tile)[]? Tiles { get => DidChangeMap ? CurrentLevel.Enumerate().ToArray() : null; }
		private static RenderUpdate State { get => new(StatusQuo, Projectile.Shots, Tiles); }

		#endregion

		#region Robot utils

		// throws an exception if name is not present in dictionary
		public static Robot Get(string név) => Robots[név]!;

        public static Robot Create(string név, int startX = 0, int startY = 0, Direction startRotation = Direction.Up, bool alt = false)
		{
			var target = new Position(startX, startY, startRotation);
			if (Robots.TryGetValue(név, out var robot))
			{
				Console.Error.WriteLine($"{név} robot már létezik, nem lesz új robot létrehozva.");
				robot.ProposedPosition = robot.CurrentPosition = target;
                return robot;
			}

			var r = new Robot(név) { CurrentPosition = target, ProposedPosition = target, Alt = alt };
            Robots.Add(név, r);
			return r;
		}

        /// <summary>
        /// Overload for modal result
        /// </summary>
        public static RobotInfo[] Create(KareszModal.KareszData record)
        {
            Create(record.Name, record.X, record.Y, record.Direction);
            return StatusQuo;
        }

        /// <summary>
        /// Sets the position of a robot, by name
        /// won't take effect while game is running
        /// </summary>
        public static RobotInfo[] PlaceAt(string name, int x, int y, Direction? rotation = null)
        {
            if (!IsRunning && Robots.TryGetValue(name, out var robot))
			{
                if (rotation.HasValue)
                    robot.ProposedPosition = robot.CurrentPosition = new Position(x, y, rotation.Value);
				else
                    robot.ProposedPosition.Vector = robot.CurrentPosition.Vector = new Vector(x, y);
			}

			return StatusQuo;
        }

        public static RobotInfo[] Move(string name, RelativeDirection direction = RelativeDirection.Right)
        {
			if (!IsRunning && Robots.TryGetValue(name, out var robot))
                robot.CurrentPosition += direction;

			return StatusQuo;
		}

        public static RobotInfo[] Delete(string name)
        {
            if (!IsRunning)
                Robots.Remove(name);
			
			return StatusQuo;
		}

		#endregion

		#region Game lifecycle

		/// <summary>
		/// List all robot objects that are about to die in this round
		/// </summary>
		private static IEnumerable<(string name, string reason, Vector place)> RobotsToExecute()
        {
            foreach (var robot in Robots.Values)
            {
                // steps into wall
                if (CurrentLevel[robot.ProposedPosition.Vector] == Level.Tile.Wall)
                    yield return (robot.Név, "nekiment egy falnak", robot.ProposedPosition.Vector);
				// ...or lava
				if (CurrentLevel[robot.ProposedPosition.Vector] == Level.Tile.Lava)
					yield return (robot.Név, "lávába esett", robot.CurrentPosition.Vector);
				// out of bounds
				if (!CurrentLevel.InBounds(robot.ProposedPosition.Vector))
                    yield return (robot.Név, "kiesett a pályáról", robot.Position.Vector);
                // stepping on the same field or stepping over one another
                if (Robots.Values.Any(other => other.Név != robot.Név 
                    && (robot.ProposedPosition.Vector == other.ProposedPosition.Vector 
                    || (robot.ProposedPosition.Vector == other.Position.Vector && other.ProposedPosition.Vector == robot.Position.Vector))))
                    yield return (robot.Név, "összeütközött", robot.ProposedPosition.Vector);
                // hit by projectile
                if (Projectile.IsHit(robot.ProposedPosition))
                    yield return (robot.Név, "találat érte", robot.ProposedPosition.Vector);
            }
        }

        /// <summary>
        /// Perform a round in game
        /// </summary>
		private static async Task MakeRoundAsync()
		{
			// move projectiles
			Projectile.TickAll();

			// remove killed robots
			foreach (var info in RobotsToExecute().Distinct().ToArray())
			{
                await KillAsync(info.name, info.place);
				Console.WriteLine($"[{info.name}] {info.reason}");
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
            IsRunning = true;
			await render.Invoke(State); // render before start

            // create a combined token so token can be cancelled after the while loop
            var manualCTS = new CancellationTokenSource();
			var mct = cancellationToken.CombineWith(manualCTS.Token).Token;

			List<Task> runningTasks = [];
			foreach (var robot in Robots.Values)
			{
				// make the combined CancellationToken available to async tick() calls
				// cancelling Task.Run will not dispose the task it is running, just request a cancellation
				// if the task is blocking at a resetEvent.WaitOne() call, the task will run indefinitely
				robot.FeladatHandle = new();
                robot.CancellationToken = robot.FeladatHandle.Token.CombineWith(cancellationToken, manualCTS.Token).Token;
				var task = Task.Run(robot.FeladatAsync.Invoke, robot.CancellationToken);
				runningTasks.Add(task);
			}

            // run cleanup on cancel
            mct.Register(() =>
			{
				foreach (var item in runningTasks)
				{
					if (item.Status != TaskStatus.WaitingForActivation)
						item.Dispose();
					else
						Console.Error.WriteLine("A task is still running... well fuck");
				}

				IsRunning = false;
				if (autoCleanup)
					Cleanup();

				DidChangeMap = true; // force re-render map
				_ = render.Invoke(State);
			});

            while (!mct.IsCancellationRequested && Robots.Count > 0)
            {
                // block Tick() calls again
                resetEvent.Reset();

                // time for robot tasks to block again
                await Task.Delay(TICK_INTERVAL, mct);

                // run multiplayer logic
                await MakeRoundAsync();

				// trigger UI render
				await render.Invoke(State);
				DidChangeMap = false;

				// unblock Tick() calls
				resetEvent.Set();

                // DEBUG
                //Console.WriteLine("--- ROUND {0} ---\n{1}\n---", TickCount, string.Join(", ", Robots.Values));
            }

			// tasks may be still running, block them!
			resetEvent.Reset();

			// still need to cancel
			if (!mct.IsCancellationRequested)
                await manualCTS.CancelAsync();
		}

        /// <summary>
        /// Remove a robot from players and place a black rock to the place of death
        /// </summary>
		private static async Task KillAsync(string name, Vector? graveLocation)
		{
			if (Robots.Remove(name, out var robot))
			{
				await robot.FeladatHandle.CancelAsync();
	            CurrentLevel[graveLocation ?? robot.Position.Vector] = Level.Tile.Black;
                DidChangeMap = true;
            }
		}

		/// <summary>
		/// Resets tick counter, removes all rocks placed in the run.
		/// If removeRobots is true, every player's robot will be deleted.
		/// </summary>
		public static RenderUpdate Cleanup(bool removeRobots = false)
		{
			if (removeRobots)
				Robots.Clear();

			Plugin.Get(CurrentLevel.LevelName)?.Cleanup();
			CurrentLevel.Reset();
            Projectile.Clear();
			TickCount = 0;

			return State;
		}

		#endregion
	}
}
