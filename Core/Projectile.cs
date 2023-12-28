namespace karesz.Core
{
    public class Projectile(Position initialPosition, Robot owner)
    {
        public readonly Robot Owner = owner;
        public Position CurrentPosition = initialPosition;

        public Func<Position, Position>? MovePredicate { get; set; } = null;

        private void Tick()
        {
            if(MovePredicate != null)
                CurrentPosition = MovePredicate(CurrentPosition);
            else
                CurrentPosition += RelativeDirection.Forward;
        }

        // statics
        public static readonly HashSet<Projectile> Projectiles = [];

        public static void TickAll()
        {
            foreach (var item in Projectiles)
                item.Tick();
        }

        public static void Shoot(Position initialPosition, Robot owner) => 
            Projectiles.Add(new Projectile(initialPosition, owner));
    }
}
