namespace Karesz
{
    using karesz.Core;
    
    public class Form
    {
        // this method is the entry point
        // should be overridden
        public virtual void DIÁK_ROBOTJAI() { }

        public const int fekete = (int)Level.Tile.Black;
        public const int piros = (int)Level.Tile.Red;
        public const int zöld = (int)Level.Tile.Green;
        public const int sárga = (int)Level.Tile.Yellow;
        public const int hó = (int)Level.Tile.Snow;
        public const int víz = (int)Level.Tile.Water;

        public const int jobbra = 1;
        public const int balra = -1;
    };

    public partial class Form1 : Form
    {
        // TODO: add plugins and stuff here
        public Form1()
        {
            Robot.Create("Karesz");
        }
    }
}
