using System.Collections.Concurrent;

namespace karesz.Core
{
    public class Robot
    {
        #region Instance Properties

        private Position CurrentPosition = new(0, 0);
        private Position ProposedPosition = new(0, 0);

        private Position Position
        {
            get => CurrentPosition;
            set
            {
                // throw exception if coordinate is out of bounds
                if (!CurrentLevel.InBounds(value.Vector))
                    throw new Exception($"Érvénytelen lépés! A megadott pozició {value.Vector} kívül esik a pályán (szélesség: {CurrentLevel.Width}, magasság: {CurrentLevel.Height})!");
                // CurrentPosition will be updated to this after the next tick
                ProposedPosition = value;
            }
        }

        public Action Feladat { get; set; }

        private bool IsDead { get; set; } = false;

        public int[] Stones { get; set; } = [];

        public string Név { get; private set; }

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

        private void Tick()
        {
            // TODO: signal thread here or ??
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

        #endregion

        #region Static Props

        // use concurrent data types for multithreading
        private static readonly ConcurrentDictionary<string, Robot> Robots = new();

        private static readonly Level CurrentLevel = Level.Default;

        // throws an exception if name is not present in dictionary
        // (instead of nullable handling)
        public static Robot Get(string név) => Robots[név]!;

        private static bool IsPositionOccupied(Vector position) => Robots.Any(x => x.Value.CurrentPosition.Vector == position);

        #endregion
    }
}
