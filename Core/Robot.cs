using System.Collections.Concurrent;

namespace karesz.Core
{
    public class Robot
    {
        #region Instance Properties
        public Position Position { get; set; } = new Position(0, 0);

        public Action Feladat { get; set; }

        public bool IsDead { get; set; }

        public int[] Stones { get; set; } = [];

        public string Név { get; private set; }

        private Level CurrentLevel { get; set; } = new Level();

        // CONSTANTS (for karesz interface)
        public const int fekete = (int)Level.Tile.Black;
        public const int piros = (int)Level.Tile.Red;
        public const int zöld = (int)Level.Tile.Green;
        public const int sárga = (int)Level.Tile.Yellow;
        public const int hó = (int)Level.Tile.Snow;
        public const int víz = (int)Level.Tile.Water;

        #endregion

        #region Static Props
        // use concurrent data types for multithreading
        private static readonly ConcurrentDictionary<string, Robot> Robots = new();

        // throws an exception if name is not present in dictionary
        // (instead of nullable handling)
        public static Robot Get(string név) => Robots[név]!;

        #endregion
    }
}
