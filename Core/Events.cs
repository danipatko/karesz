namespace karesz.Core
{
    public static class Events
    {
        public static event EventHandler? OnRender;

        public static void RaiseRender() => OnRender?.Invoke(null, EventArgs.Empty);
    }

    //public class RenderEvent : EventArgs
    //{
    //    public static void Raise() => OnRender?.Invoke(null, EventArgs.Empty);
    //}
}
