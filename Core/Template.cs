namespace Karesz
{
    using karesz.Core;

    public class Form
    {
        public const int fekete = (int)Level.Tile.Black;
        public const int piros = (int)Level.Tile.Red;
        public const int zöld = (int)Level.Tile.Green;
        public const int sárga = (int)Level.Tile.Yellow;
        public const int hó = (int)Level.Tile.Snow;
        public const int víz = (int)Level.Tile.Water;

        public const int jobbra = 1;
        public const int balra = -1;

        public const int észak = 0;
        public const int kelet = 1;
        public const int dél = 2;
        public const int nyugat = 3;

        public Form()
        {
            //Console.WriteLine("Creating default karesz...");
            //_ = Robot.Create("Karesz", startX: 0, startY: 0, startRotation: Direction.Up);
        }
    };

    public partial class Form1 : Form { }
}
