namespace karesz.Core
{
    // absolute direction
    public enum Direction : int
    {
        Up = 0, Down = 2, Left = 3, Right = 1,
    }

    // relative direction
    public enum RelativeDirection : int
    {
        Forward = 0, Right = 1, Left = -1
    }

    public struct Vector(int x, int y)
    {
        public int x = x;
        public int y = y;

        // operator overrides
        public static Vector operator +(Vector a, Vector b) => new(a.x + b.x, a.y + b.y);

        public static bool operator ==(Vector a, Vector b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector a, Vector b) => a.x != b.x || a.y != b.y;

        public static Vector Normalize(Direction direction) => direction switch
        {
            Direction.Up => new(0, -1),
            Direction.Right => new(1, 0),
            Direction.Down => new(0, 1),
            Direction.Left => new(-1, 0),
            _ => throw new Exception("Undefined direction.") // should never happen
        };

        // Moves current position towards absolute direction
        public static Vector operator +(Vector p, Direction direction) => p + Normalize(direction);

        // clamp an integer between two values
        public static int Clamp(int min, int max, int n) => Math.Max(Math.Min(n, max), min);

        // clamps a vector between specified coordinates
        public readonly Vector Clamp(int maxX, int maxY, int minX = 0, int minY = 0) => 
            new(Clamp(minX, maxX, x), Clamp(minY, maxY, y));

        public static bool InBounds(Vector v, int maxX, int maxY, int minX = 0, int minY = 0) => InBounds(v.x, v.y, maxX, maxY, minX, minY);

        public static bool InBounds(int x, int y, int maxX, int maxY, int minX = 0, int minY = 0) =>
            x >= minX && y >= minY && x < maxX && y < maxY;

        public readonly static Vector Null = new(0, 0);

        #region Overrides

        public override readonly string ToString() => $"({x}, {y})";

        public readonly override int GetHashCode() => (x << 2) ^ y;

        public readonly override bool Equals(object? obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                return false;

            var p = (Vector)obj;
            return (x == p.x) && (y == p.y);
        }

        #endregion
    }

    public struct Position
    {
        public Vector Vector;
        public Direction Rotation;

        public Position(int x = 0, int y = 0, Direction direction = Direction.Up)
        {
            Vector = new(x, y);
            Rotation = direction;
        }

        public Position(Vector initialVector, Direction rotation)
        {
            Vector = initialVector;
            Rotation = rotation;
        }

        // % operator for negative numbers aswell
        private static int Mod(int a, int b) => ((a % b) + b) % b;

        // Rotates absolute direction by relative direction (forward takes no effect)
        private static Direction Rotate(Direction direction, RelativeDirection rotateBy) => (Direction)Mod((int)direction + (int)rotateBy, 4);

        // Moves current position towards absolute direction (keeps rotation)
        public static Position operator +(Position p, Direction direction) => new(p.Vector + Vector.Normalize(direction), p.Rotation);

        // Moves forward or turns left/right
        public static Position operator +(Position p, RelativeDirection relativeDirection) => relativeDirection switch
        {
            // forward -> adjust position only
            RelativeDirection.Forward => p + p.Rotation,
            // turn left or right -> adjust rotation only
            _ => new(p.Vector, Rotate(p.Rotation, relativeDirection))
        };

        public readonly override string ToString() => $"position:{Vector} rotation:{Rotation}";

        public static string DisplayDirection(Direction direction) => direction switch
        {
            Direction.Up => "észak",
            Direction.Right => "kelet",
            Direction.Down => "dél",
            Direction.Left => "nyugat",
            _ => throw new Exception("Invalid direction enum")
        };
    }
}
