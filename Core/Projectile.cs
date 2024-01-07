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

            if(!Robot.CurrentLevel.InBounds(CurrentPosition.Vector))
                Projectiles.Remove(this);
        }

        // statics
        private static readonly HashSet<Projectile> Projectiles = [];

		public static bool IsHit(Position position) => Projectiles.Any(x => x.CurrentPosition.Vector == position.Vector);

		public static void TickAll()
        {
            foreach (var item in Projectiles)
                item.Tick();
        }

        public static void Shoot(Position initialPosition, Robot owner) => Projectiles.Add(new Projectile(initialPosition, owner));

		public static void Clear() => Projectiles.Clear();

        public static Position[] Shots { get => Projectiles.Select(x => x.CurrentPosition).ToArray(); }
	}
}
