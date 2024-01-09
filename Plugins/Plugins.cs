using System.Reflection;
using static karesz.Core.Level;

namespace karesz.Core
{
	// https://stackoverflow.com/questions/5411694/get-all-inherited-classes-of-an-abstract-class
	static class ReflectiveEnumerator
	{
		public static IEnumerable<T> GetEnumerableOfType<T>(params object[] constructorArgs) where T : class
		{
			List<T> objects = [];
			foreach (Type type in Assembly.GetAssembly(typeof(T))!.GetTypes()
				.Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
			{
				objects.Add((T)Activator.CreateInstance(type, constructorArgs)!);
			}
			return objects;
		}
	}

	public abstract class Plugin
	{
		/// <summary>
		/// Specify the level where this plugin should be loaded
		/// </summary>
		public abstract string LevelName { get; }

		/// <summary>
		/// Called after DIAK_ROBOTJAI invocation
		/// !!IMPORTANT!! Any defined robot feladat MUST BE ASYNC!
		/// This is as simple as placing an await before - and appending an 'Async' suffix on karesz functions.
		/// </summary>
		public abstract void TANAR_ROBOTJAI();

		/// <summary>
		/// Called when some other level is about to be loaded
		/// Use this to remove robots previously created by the plugin
		/// </summary>
		public abstract void Cleanup();

		#region Plugin management

		public static Plugin[] PluginList { get; private set; } = [];

		/// <summary>
		/// All classes that implement Plugin
		/// </summary>
		public static void LoadPlugins()
		{
			PluginList = ReflectiveEnumerator.GetEnumerableOfType<Plugin>().ToArray();
            Console.WriteLine("Plugins loaded:\n{0}", string.Join("\n", PluginList.Select(x => $"{x.GetType().Name} (for {x.LevelName})")));
		}

		public static Plugin? Get(string levelName) => PluginList.FirstOrDefault(x => x.LevelName == levelName);

		#endregion
	}
}
