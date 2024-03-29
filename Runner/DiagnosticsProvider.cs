﻿using Microsoft.CodeAnalysis;

namespace karesz.Runner
{
    public partial class DiagnosticsProvider
    {
        public class Issue
        {
            public string Message { get; set; }
			public int Severity { get; set; } // monaco.MarkerSeverity.Error,
            public int StartLineNumber { get; set; } 
			public int StartColumn { get; set; }
            public int EndLineNumber { get; set; }
            public int EndColumn { get; set; }
            public override string ToString()
            {
                return $"Line {StartLineNumber}, Column {StartColumn}: {Message}";
            }
        }

        /// <summary>
        /// Starts a compilation and returns an array of issues. Issue is json serializable and compatible with monaco's API.
        /// </summary>
        public static async Task<IEnumerable<Issue>> GetDiagnosticsAsync(string code)
        {
            var results = await CompilerSerivce.CompileAsync(code);
            if (results.Success) return [];

            var issues = new Issue[results.Diagnostics.Length];

            for (int i = 0; i < results.Diagnostics.Length; i++)
            {
                var linespan = results.Diagnostics[i].Location.GetLineSpan();
                issues[i] = new Issue
                {
                    Message = FmtMessage(results.Diagnostics[i]),
                    Severity = (int)results.Diagnostics[i].Severity,
                    StartLineNumber = linespan.StartLinePosition.Line + 1, // offset needed for whatever reason
                    EndLineNumber = linespan.EndLinePosition.Line + 1,
                    StartColumn = linespan.StartLinePosition.Character + 1,
                    EndColumn = linespan.EndLinePosition.Character + 1,
                };
            }

            return issues;
        }

        public static string FmtMessage(Diagnostic diagnostic) => $"[{diagnostic.Severity}] {diagnostic.GetMessage()} ({diagnostic.Id})";

	}
}

