using System.ComponentModel;

namespace karesz.Runner
{
	// https://stackoverflow.com/questions/3708454/is-there-a-textwriter-child-class-that-fires-event-if-text-is-written
	public class StringWriterExt(bool autoFlush = true) : StringWriter
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		public delegate void FlushedEventHandler(object sender, EventArgs args);
		public event FlushedEventHandler? Flushed;

		public virtual bool AutoFlush { get; set; } = autoFlush;

		public override async Task FlushAsync()
		{
			await base.FlushAsync();
			Flushed?.Invoke(this, EventArgs.Empty);
		}

		public override void Flush()
		{
			base.Flush();
			Flushed?.Invoke(this, EventArgs.Empty);
		}

		public override async Task WriteAsync(char value)
		{
			await base.WriteAsync(value);
			if (AutoFlush) await FlushAsync();
		}

		public override async Task WriteAsync(string? value)
		{
			await base.WriteAsync(value);
			if (AutoFlush) await FlushAsync();
		}

		public override async Task WriteAsync(char[] buffer, int index, int count)
		{
			await base.WriteAsync(buffer, index, count);
			if (AutoFlush) await FlushAsync();
		}

		public override void Write(char value)
		{
			base.Write(value);
			if (AutoFlush) Flush();
		}

		public override void Write(string? value)
		{
			base.Write(value);
			if (AutoFlush) Flush();
		}

		public override void Write(char[] buffer, int index, int count)
		{
			base.Write(buffer, index, count);
			if (AutoFlush) Flush();
		}
	}

	public class Output
	{
		private static StringWriterExt Writer = new();
		public static StringWriterExt.FlushedEventHandler? FlushHandler { get; set; }

		/// <summary>
		/// Capture everything written to Console.Out (but not Console.Error)
		/// Pair method: `ResetCapture`, use this to reset console output and add captured text to `Content`
		/// </summary>
		public static async Task StartCaptureAsync()
		{
            await Console.Error.WriteLineAsync(">>> starting capture");
			Writer = new();
			if (FlushHandler != null)
				Writer.Flushed += FlushHandler;
			
			await Writer.FlushAsync();
			Console.SetOut(Writer);
		}

		/// <summary>
		/// Add captured text to content and set output to default Console.Out
		/// </summary>
		public static async Task ResetCaptureAsync()
		{
			await Console.Error.WriteLineAsync("||| ending capture");
			// reset stdout
			var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
			Console.SetOut(stdout);
			await Console.Out.FlushAsync();
		}

		// public static async Task WriteLineAsync(string? value) => await Writer.WriteAsync(value + "\n");

		public static void WriteLine(string? value) => Writer.Write(value + "\n");
	}
}
