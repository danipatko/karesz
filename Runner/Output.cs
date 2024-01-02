namespace karesz.Runner
{
	public class Output
	{
		public static string Content { get; private set; } = string.Empty;

		private static StringWriter Writer = new();

		/// <summary>
		/// Capture everything written to Console.Out (but not Console.Error)
		/// Pair method: `ResetCapture`, use this to reset console output and add captured text to `Content`
		/// </summary>
		public static async Task StartCaptureAsync()
		{
			Writer = new StringWriter();
			await Writer.FlushAsync();
			Console.SetOut(Writer);
		}

		/// <summary>
		/// Add captured text to content and set output to default Console.Out
		/// </summary>
		public static async Task ResetCaptureAsync()
		{
			// add captured content
			var reader = new StringReader(Writer.ToString());
			Content += await reader.ReadToEndAsync();
			// reset stdout
			var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
			Console.SetOut(stdout);
			await Console.Out.FlushAsync();
		}

		public static void Write(object? value) => Content += value;

		public static void WriteLine(object? value) => Content += value + "\n";

		public static void Flush() => Content = string.Empty;
	}
}
