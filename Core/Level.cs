using System.Security.Claims;

namespace karesz.Core
{
    public class Level
    {
        // possible objects on the map
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

        protected Tile[,] Map = new Tile[0, 0];
        protected int[,] HeatMap = new int[0, 0];

        public bool IsLoaded = false;

        public int Width { get => Map.GetLength(0); }
        public int Height { get => Map.GetLength(1); }

        public Tile this[int x, int y]
        {
            // we do not trust the user with bounds -> return closest tile inside
            get => Map[Vector.Clamp(0, Width, x), Vector.Clamp(0, Height, y)];
            protected set => Map[x, y] = value;
        }
        public Tile this[Vector v]
        {
            get => this[v.x, v.y];
            protected set => this[v.x, v.y] = value;
        }

        // --- METHODS

        // checks if vector is inside the map
        public bool InBounds(Vector v) => Vector.InBounds(v, Width, Height);

        public void Set(Tile tile, Vector at)
        {
            if(InBounds(at)) this[at] = tile;
        }

        // get the neighbor tile coordinates of a position
        private IEnumerable<Vector> NeighborsOf(Vector position)
        {
            if (position.x >= 0) yield return position + Direction.Left;
            if (position.x <= Width) yield return position + Direction.Right;
            if (position.y >= 0) yield return position + Direction.Down;
            if (position.y <= Height) yield return position + Direction.Up;
        }

        public int GetHeat(Vector v) => InBounds(v) ? HeatMap[v.x, v.y] : -1;

    }
}
