namespace Karesz
{
    using karesz.Core;

    public class Form
    {
        // this method is the entry point
        public virtual void DIÁK_ROBOTJAI() { }

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

        // TODO: add plugins and stuff here
        public Form()
        {
            Console.WriteLine("Creating default karesz...");
            var karesz = Robot.Create("Karesz");
            karesz.Teleport(10, 10);
        }
    };

    public partial class Form1 : Form { }
}
