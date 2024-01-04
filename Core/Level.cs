﻿using System.Collections.Immutable;
using System.ComponentModel;

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

        #endregion

        #region Level loading

        private static readonly Dictionary<string, Tile[,]> CachedMaps = [];

        public static async Task<Level?> LoadAsync(HttpClient httpClient, string levelName)
        {
            // use cache if available
            if(CachedMaps.TryGetValue(levelName, out var levelMap))
                return new Level(levelName, levelMap);

            // load & parse
            try
            {
                var levelContent = await FetchLevelTextAsync(httpClient, levelName);

                var parsedMap = Parse(levelName, levelContent);
                CachedMaps.Add(levelName, parsedMap);
                var copy = parsedMap;

                var level = new Level(levelName, copy);
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
            var result = await httpClient.GetAsync($"/levels/{levelName}");
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Parse tab-split map text file
        /// </summary>
        /// <exception cref="FormatException"><paramref name="levelContent"/> cannot be parsed.</exception>
        /// <exception cref="OverflowException"><paramref name="levelContent"/> cannot be parsed.</exception>
        private static Tile[,] Parse(string levelName, string levelContent)
        {
            var jagged = levelContent.Replace("\r", string.Empty).Split('\n').Select(x => x.Split('\t').Select(y => (Tile)Convert.ToInt32(y)).ToImmutableArray()).ToImmutableArray();
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
		public static Level Reset()
		{
			if (CachedMaps.TryGetValue(Robot.CurrentLevel.LevelName, out var levelMap))
            {
                var copy = levelMap;
                Console.Error.WriteLine("real");
                return new Level(Robot.CurrentLevel.LevelName, copy);
            }
            else
                return Default;
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
    }
}
