namespace karesz.Core
{
    public static class Events
    {
        public static event EventHandler? OnRender;

        public static void RaiseRender() => OnRender?.Invoke(null, EventArgs.Empty);
    }

    public class RenderEvent(IEnumerable<Robot> robots) : EventArgs
    {
        public IEnumerable<Robot> Robots { get; set; } = robots;

		public static event EventHandler<RenderEvent>? OnRender;
		
        public static void Raise(IEnumerable<Robot> robots) => OnRender?.Invoke(null, new RenderEvent(robots));
    }
}
