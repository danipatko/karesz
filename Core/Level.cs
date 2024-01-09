using System.Collections.Immutable;
using System.ComponentModel;
using karesz.Components;

namespace karesz.Core
{
    public class Level
    {
        // possible objects on the map
        [DefaultValue(Empty)]
        public enum Tile : int
        {
            None = -1,
            Empty = 0,
            Wall = 1,
            Black = 2,
            Red = 3,
            Green = 4,
            Yellow = 5,
            Snow = 6,
            Lava = 7,
            Water = 8,
        }

        protected Tile[,] Map;
        protected int[,] HeatMap;

        public string LevelName = string.Empty;

        public int Width { get => Map.GetLength(0); }
        public int Height { get => Map.GetLength(1); }

        public Level(string levelName, Tile[,] map)
        {
            Map = map;
            LevelName = levelName;

            HeatMap = new int[Width, Height];
            MapHeat();
        }

        public Tile this[int x, int y]
        {
            get => InBounds(x, y) ? Map[x, y] : Tile.None;
            set
            {
                if (!InBounds(x, y))
                    throw new Exception($"A megadott pozició ({x}, {y}) kívül esik a pályán (szélesség: {Width}, magasság: {Height})!");
                Map[x, y] = value;
            }
        }

        public Tile this[Vector v]
        {
            get => this[v.x, v.y];
            set => this[v.x, v.y] = value;
        }

        // checks if vector is inside the map
        public bool InBounds(Vector v) => Vector.InBounds(v, Width, Height);
        public bool InBounds(int x, int y) => Vector.InBounds(x, y, Width, Height);

        // get the neighbor tile coordinates of a position
        private IEnumerable<Vector> NeighborsOf(Vector position)
        {
            if (position.x > 0) yield return position + Direction.Left;
            if (position.x < Width - 1) yield return position + Direction.Right;
            if (position.y > 0) yield return position + Direction.Up;
            if (position.y < Height - 1) yield return position + Direction.Down;
        }

        #region Heat stuff (?)

        const int LAVA_TEMP = 1000;

        public int GetHeat(Vector v) => InBounds(v) ? HeatMap[v.x, v.y] : -1;

        private void HeatNextTo(Vector a, Vector b)
        {
            if (200 + HeatMap[b.x, b.y] < HeatMap[a.x, a.y])
                HeatMap[b.x, b.y] = HeatMap[a.x, a.y] - 200;
        }

        private void HeatAtPosition(Vector position)
        {
            foreach (var item in NeighborsOf(position))
                HeatNextTo(position, item);
        }

        private void MapHeat()
        {
            // A láva 1000 fokos... ("Inicializálás")
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (Map[x, y] == Tile.Lava)
                        HeatMap[x, y] = LAVA_TEMP;

            //... és minden szomszédos mezőn 200 fokkal hűvösebb. Tehát 4-szer (1000->800->600->400->200) végigmegyünk, hogy a felmelegedést update-eljük.
            for (int k = 0; k < 4; k++)
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        HeatAtPosition(new(x, y));
        }

        public IEnumerable<(int x, int y, int h)> EnumerateHeat()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (HeatMap[x, y] != 0)
                        yield return (x, y, HeatMap[x, y]);
		}

		#endregion

		#region Level loading

		private static readonly Dictionary<string, Tile[,]> CachedMaps = [];

        public static async Task<Level?> LoadAsync(HttpClient httpClient, string levelName)
        {
            Plugin.Get(Robot.CurrentLevel.LevelName)?.Cleanup();
            
            if (levelName == "default")
            {
                var level = Default;
				Robot.CurrentLevel = level;
				return level;
			}

            // use cache if available
            if (CachedMaps.TryGetValue(levelName, out var levelMap))
            {
                var level = new Level(levelName, Copy(levelMap)); // make sure this is not passed by reference!
				Robot.CurrentLevel = level;
				return level;
            }

            // load & parse
            try
            {
                var levelContent = await FetchLevelTextAsync(httpClient, levelName);
                var parsedMap = Parse(levelContent);
                CachedMaps.Add(levelName, parsedMap);

                var level = new Level(levelName, parsedMap);
                Robot.CurrentLevel = level;

                return level;
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"Failed to fetch and parse level '{levelName}'. Reason:\n{e.Message}");
                return null;
            }
        }

        private static async Task<string> FetchLevelTextAsync(HttpClient httpClient, string levelName)
        {
            // warn
            if (!LEVEL_NAMES.Contains(levelName))
                await Console.Error.WriteLineAsync($"Warning: '{levelName}' is not present in options.");

            var result = await httpClient.GetAsync($"levels/{levelName}");
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Parse tab-split map text file
        /// </summary>
        /// <exception cref="FormatException"><paramref name="levelContent"/> cannot be parsed.</exception>
        /// <exception cref="OverflowException"><paramref name="levelContent"/> cannot be parsed.</exception>
        private static Tile[,] Parse(string levelContent)
        {
            var jagged = levelContent.Replace("\r", string.Empty).Split('\n').Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Split('\t').Select(y => (Tile)Convert.ToInt32(y)).ToImmutableArray()).ToImmutableArray();
            var map = new Tile[jagged[0].Length, jagged.Length];

            for (int y = 0; y < jagged.Length; y++)
                for (int x = 0; x < jagged[y].Length; x++)
                    map[x, y] = jagged[y][x];

            return map;
        }

        // an empty level
        public static Level Default { get => new("default", new Tile[41, 31]); }

		#endregion

		#region Utils

        // copies a Tile matrix
		private static Tile[,] Copy(Tile[,] from)
		{
			var map = new Tile[from.GetLength(0), from.GetLength(1)];

			for (int y = 0; y < from.GetLength(1); y++)
				for (int x = 0; x < from.GetLength(0); x++)
					map[x, y] = from[x, y];

			return map;
		}

		/// <summary>
		/// Array of tiles that are not empty
		/// </summary>
		public IEnumerable<(int x, int y, Tile tile)> Enumerate()
        {
			for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (Map[x, y] != Tile.Empty)
                        yield return (x, y, Map[x, y]);
		}

		/// <summary>
		/// Reloads the current level from cache, use this to clean up stones placed by robot
		/// </summary>
		public void Reset()
		{
            if (CachedMaps.TryGetValue(Robot.CurrentLevel.LevelName, out var originalMap))
                Map = Copy(originalMap);
            else
                Map = Default.Map;
        }

		public override string ToString()
        {
            var result = string.Empty;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    result += (int)Map[x, y] + " ";
                result += "\n";
            }
            return result;
        }

        public string ToHeatString()
        {
            var result = string.Empty;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    result += string.Format("{0:0000} ", HeatMap[x, y]);
                result += "\n";
            }
            return result;
		}

        #endregion

        #region Hard-coded level options

        // level options for autocomplete
        public static readonly string[] LEVEL_NAMES = ["default",
            "l0.txt",
            "l1.txt",
            "l2.txt",
			"indiana.txt",
			"palya01.txt",
            "palya02.txt",
            "palya03.txt",
            "palya04.txt",
            "palya05.txt",
            "palya06.txt",
            "palya07.txt",
            "palya08.txt",
            "palya09.txt",
            "palya10.txt",
            "palya11.txt",
            "palya12.txt",
            "palya13.txt",
            "palya14.txt",
            "palya15.txt",
            "palya16.txt",
            "palya17.txt",
            "palya18.txt",
            "palya19.txt",
            "palya20.txt",
            "palya21.txt",
            "palya22.txt",
            "palya23.txt",
            "palya24.txt",
            "palya25.txt",
            "palya26.txt",
            "palya27.txt",
            "palya28.txt",
            "palya29.txt",
            "palya30.txt",
            "palya31.txt",
            "palya32.txt",
            "palya33.txt",
            "palya34.txt",
            "palya35.txt",
            "palya36.txt",
            "palya37.txt",
            "palya38.txt",
            "palya39.txt",
            "palya40.txt",
            "palya41.txt",
            "palya42.txt",
            "palya43.txt",
            "palya44.txt",
            "palya45.txt",
            "palya46.txt",
            "v0.txt"];

        #endregion
    }
}
