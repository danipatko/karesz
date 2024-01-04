#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods (bullshit warning)

using karesz.Runner;
using Microsoft.FluentUI.AspNetCore.Components;

namespace karesz.Core
{
    public partial class Robot
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

        public delegate Task FeladatAction();
        public Action Feladat { get; set; }
		public FeladatAction FeladatAsync { get; set; }

		private int[] Stones { get; set; } = new int[8] { 100, 100, 100, 100, 100, 100, 100, 100 };

        public string Név { get; }

        #endregion

        #region Instance methods

        private void Say(string message)
        {
            Console.WriteLine($"[{Név}]: {message}");
            // TODO: create event so message can be shown in frontent aswell
        }

        private static async Task Tick()
        {
            // block until released
            await resetEvent.WaitAsync();
        }

        public override string ToString() => $"{Név} at {Position}";

        #endregion

        #region Karesz API methods

        public Vector H { get => CurrentPosition.Vector; }
    
        /// <summary>
        /// Elhelyezi a Robotot a megadott helyre.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Teleport(int x, int y)
        {
            Position = new(x, y, CurrentPosition.Rotation);
        }

        public async Task TeleportAsync(int x, int y)
        {
            Teleport(x, y);
            await Tick();
        }

        /// <summary>
        /// Lépteti a robotot a megfelelő irányba.
        /// </summary>
        public void Lépj()
        {
			Position += RelativeDirection.Forward;
			Say($"lépj ide {ProposedPosition}");
        }

        public async Task LépjAsync()
        {
            Lépj();
            await Tick();
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
        }

        public async Task ForduljAsync(int forgásirány)
        {
            Fordulj(forgásirány);
            await Tick();
        }

        public void Fordulj_jobbra() => Fordulj(1);
        public void Fordulj_balra() => Fordulj(-1);
        public async Task Fordulj_jobbraAsync() => await ForduljAsync(1);
        public async Task Fordulj_balraAsync() => await ForduljAsync(-1);

        /// <summary>
        /// Lerakja az adott színű követ a pályán a robot helyére.
        /// </summary>
        /// <param name="szín"></param>
        public void Tegyél_le_egy_kavicsot(int szín = Karesz.Form.fekete)
        {
			Say($"kavics ide {CurrentPosition.Vector}");

			if (CurrentLevel[CurrentPosition.Vector] != Level.Tile.Empty)
            {
                Say($"Nem tudom a kavicsot lerakni ide {CurrentPosition.Vector}, mert van lerakva kavics!");
                return;
            }

			if (Stones[szín - 2] <= 0)
            {
                Say($"Nem tudom a kavicsot lerakni ide {CurrentPosition.Vector}, mert nincs {szín} színű kavicsom!");
                return;
            }

			CurrentLevel[CurrentPosition.Vector] = (Level.Tile)szín;
            Stones[szín - 2]--;

            DidChangeMap = true;
        }

        public async Task Tegyél_le_egy_kavicsotAsync(int szín = Karesz.Form.fekete)
        {
            Tegyél_le_egy_kavicsot(szín);
            await Tick();
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

			DidChangeMap = true;
		}

		public async Task Vegyél_fel_egy_kavicsotAsync()
        {
            Vegyél_fel_egy_kavicsot();
            await Tick();
        }

        public void Lőjj() =>
            Projectile.Shoot(CurrentPosition + RelativeDirection.Forward, this);

        public async Task LőjjAsync()
        {
            Lőjj();
            await Tick();
        }

#pragma warning disable CA1822 // Mark members as static
        public async Task Várj() => await Tick();
#pragma warning restore CA1822

        // --- SZENZOROK ---
        // doesn't trigger tick => no async alternatives

        public (int x, int y) Hol_vagyok() => (CurrentPosition.Vector.x, CurrentPosition.Vector.y);

        public int Merre_néz() => (int)CurrentPosition.Rotation;

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

        public int Hőmérő() => CurrentLevel.GetHeat(Position.Vector);

        private int Distance(Direction direction)
        {
            int distance = 1;
            var target = Position.Vector + direction;

            while (CurrentLevel.InBounds(target) && !(CurrentLevel[target] == Level.Tile.Wall || IsPositionOccupied(target)))
            {
                target += direction;
                distance++;
            }

            return CurrentLevel.InBounds(target) ? distance : -1; // ??
        }

        /// <summary>
        /// MessageBox híján karesz logol egy sort
        /// </summary>
        public void Mondd(string ezt) => Output.WriteLine($"[{Név}]: {ezt}");

        #endregion
    }
}
