using System.Collections.Concurrent;
using Microsoft.VisualStudio.Threading;

namespace karesz.Core
{
    public class Robot
    {
        #region Instance Properties
        private Robot(string név)
        {
            Név = név;
        }

        private Position CurrentPosition = new(0, 0);
        private Position ProposedPosition = new(0, 0);

        private Position Position
        {
            get => CurrentPosition;
            set
            {
                // TODO: maybe add a friendly mode to warn user?
                //if (!CurrentLevel.InBounds(value.Vector))
                //    throw new Exception($"Érvénytelen lépés! A megadott pozició {value.Vector} kívül esik a pályán (szélesség: {CurrentLevel.Width}, magasság: {CurrentLevel.Height})!");

                // CurrentPosition will be updated to this after the next tick
                ProposedPosition = value;
            }
        }


        public Action? Feladat { get; set; } = null;

        private bool IsDead { get; set; } = false;

        private int[] Stones { get; set; } = [];

        public string Név { get; }

        // CONSTANTS (for karesz interface)
        public const int fekete = (int)Level.Tile.Black;
        public const int piros = (int)Level.Tile.Red;
        public const int zöld = (int)Level.Tile.Green;
        public const int sárga = (int)Level.Tile.Yellow;
        public const int hó = (int)Level.Tile.Snow;
        public const int víz = (int)Level.Tile.Water;
        #endregion

        #region Instance methods

        private void Say(string message)
        {
            Console.WriteLine($"[{Név}]: {message}");
            // TODO: create event so message can be shown in frontent aswell
        }

        private static void Tick()
        {
            //Task.Run(async () =>
            //{
            //    // this should ensure that our async function runs in an async context
            //    await Task.Yield();
            //    // block until released
            //    await resetEvent.WaitAsync();
            //}).Wait();
        }

        public override string ToString()
        {
            return $"{Név} at {Position} (Proposed {ProposedPosition})";
        }

        #endregion

        #region Karesz API methods

        /// <summary>
        /// Elhelyezi a Robotot a megadott helyre.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Teleport(int x, int y)
        {
            Position = new(x, y, CurrentPosition.Rotation);
            Tick();
        }

        /// <summary>
        /// Lépteti a robotot a megfelelő irányba.
        /// </summary>
        public void Lépj()
        {
            Position += RelativeDirection.Forward;
            Tick();
        }

        /// <summary>
        /// Elforgatja a robotot a megadott irányban. (Csak normális irányokra reagál.)
        /// </summary>
        /// <param name="forgásirány"></param>
        public void Fordulj(int forgásirány)
        {
            Position += forgásirány switch
            {
                -1 => RelativeDirection.Left,
                1 => RelativeDirection.Right,
                _ => throw new Exception($"A megadott forgásirány ({forgásirány}) érvénytelen! A forgásirány értéke -1 (balra) vagy 1 (jobbra) lehet.")
            };
            Tick();
        }

        /// <summary>
        /// Lerakja az adott színű követ a pályán a robot helyére.
        /// </summary>
        /// <param name="szín"></param>
        public void Tegyél_le_egy_kavicsot(int szín = fekete)
        {
            if(CurrentLevel[CurrentPosition.Vector] != Level.Tile.Empty)
            {
                Say("Nem tudom a kavicsot lerakni, mert van lerakva kavics!");
                return;
            }

            if (Stones[szín - 2] <= 0)
            {   
                // TODO: nameof probably doesn't work like that
                Say($"Nem tudom a kavicsot lerakni, mert nincs {nameof(szín)} színű kavicsom!");
                return;
            }

            CurrentLevel[CurrentPosition.Vector] = (Level.Tile)szín;
            Stones[szín - 2]--;
            Tick();
        }

        /// <summary>
        /// Felveszi azt, amin éppen áll -- feltéve ha az nem fal, stb.
        /// </summary>
        public void Vegyél_fel_egy_kavicsot()
        {
            var tileUnder = CurrentLevel[CurrentPosition.Vector];
            if (tileUnder <= Level.Tile.Wall)
            {
                Say("Nem tudom a kavicsot felvenni!");
                return;
            }

            Stones[(int)tileUnder - 2]++;
            CurrentLevel[CurrentPosition.Vector] = Level.Tile.Empty;
            Tick();
        }

        // --- SZENZOROK ---
        // doesn't trigger tick

        /// <summary>
        /// Megadja, hogy az adott színből mennyi köve van a robotnak.
        /// </summary>
        /// <param name="szín"></param>
        /// <returns></returns>
        public int Köveinek_száma_ebből(int szín) => Stones[szín - 2];

        /// <summary>
        /// Megadja, hogy kavicson áll-e a robot.
        /// </summary>
        public bool Alatt_van_kavics() => CurrentLevel[CurrentPosition.Vector] > Level.Tile.Wall;

        /// <summary>
        /// Megadja, hogy min áll a robot
        /// </summary>
        public int Alatt_ez_van() => (int)CurrentLevel[CurrentPosition.Vector];

        /// <summary>
        /// Megadja, hogy mi van a robot előtt az adott helyen -- (1 = fal, -1 = kilép)
        /// </summary>
        public int MiVanElőttem()
        {
            var oneStepForward = Position + RelativeDirection.Forward;
            if (!CurrentLevel.InBounds(oneStepForward.Vector)) return -1;
            return (int)CurrentLevel[oneStepForward.Vector];
        }

        /// <summary>
        /// Pontosan akkor igaz, ha a robot előtt fal van.
        /// </summary>
        public bool Előtt_fal_van() => MiVanElőttem() == (int)Level.Tile.Wall;

        /// <summary>
        /// Pontosan akkor igaz, ha a robot a pálya szélén van és a következő lépéssel kizuhanna a pályáról.
        /// </summary>
        public bool Ki_fog_lépni_a_pályáról() => MiVanElőttem() == (int)Level.Tile.None;

        /// <summary>
        /// Megadja, hogy milyen messze van a robot előtti legközelebbi olyan objektum, amely vissza tudja verni a hangot (per pill. másik robot vagy fal)
        /// </summary>
        public int UltrahangSzenzor() => Distance(Position.Rotation);

        /// <summary>
        /// Megadja, hogy milyen messze van a robot előtti legközelebbi olyan objektum, amely vissza tudja verni a hangot (per pill. másik robot vagy fal)
        /// </summary>
        public (int, int, int) SzélesUltrahangSzenzor() 
            => (Distance((Position + RelativeDirection.Left).Rotation), Distance(Position.Rotation), Distance((Position + RelativeDirection.Right).Rotation));

        private int Distance(Direction direction)
        {
            int distance = 1;
            var target = Position.Vector + direction;

            while (CurrentLevel.InBounds(target) && !(CurrentLevel[target] == Level.Tile.Wall || IsPositionOccupied(target)))
            {
                target += direction;
                distance++;
            }

            return CurrentLevel.InBounds(target) ? distance : -1;
        }

        public int Hőmérő() => CurrentLevel.GetHeat(Position.Vector);

        #pragma warning disable CA1822 // Mark members as static
        public void Várj() => Tick();
        #pragma warning restore CA1822

        // public void Mondd(string ezt) => MessageBox.Show(Név + ": " + ezt);

        public void Lőjj()
        {
            Projectile.Shoot(CurrentPosition + RelativeDirection.Forward, this);
            Tick();
        }

        #endregion

        #region Static Props
        public static Robot Create(string név)
        {
            var r = new Robot(név);
            Robots.Add(név, r);
            return r;
        }

        private static int TickCount = 0;

        // used for signalling & waiting
        private static readonly AsyncManualResetEvent resetEvent = new (false);

        private static readonly Dictionary<string, Robot> Robots = [];

        private static readonly Level CurrentLevel = Level.Default;

        // throws an exception if name is not present in dictionary
        // (instead of nullable handling)
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
                // stepping on the same field
                if (Robots.Values.Any(other => other.Név != robot.Név && robot.ProposedPosition.Vector == other.ProposedPosition.Vector))
                    yield return robot.Név;
                // stepping over one another
                if (Robots.Values.Any(other => other.Név != robot.Név && robot.ProposedPosition.Vector == other.Position.Vector && other.ProposedPosition.Vector == robot.Position.Vector))
                    yield return robot.Név;
                // hit by projectile
                if (Projectile.Projectiles.Any(x => x.CurrentPosition.Vector == robot.ProposedPosition.Vector))
                    yield return robot.Név;
            }
        }

        public static async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // block Tick() calls again
                resetEvent.Reset();
                // time for robot tasks to block again
                await Task.Delay(100, cancellationToken);
                // run multiplayer logic
                MakeRound();
                // unblock Tick() calls
                resetEvent.Set();
            }
        }

        #endregion
    }
}
