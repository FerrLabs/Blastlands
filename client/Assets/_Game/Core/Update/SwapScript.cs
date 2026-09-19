using System.Text;

namespace Blastlands.Core.Update
{
    public static class SwapScript
    {
        private const int WaitForExitSeconds = 120;

        public static string PowerShell(int gameProcessId, UpdateLayout layout, string executableName)
        {
            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("$install = " + Quote(layout.Install));
            script.AppendLine("$staged = " + Quote(layout.Staged));
            script.AppendLine("$previous = " + Quote(layout.Previous));
            script.AppendLine("$executable = " + Quote(executableName));
            script.AppendLine();
            script.AppendLine("function Move-Retrying([string] $from, [string] $to) {");
            script.AppendLine("    for ($attempt = 1; $attempt -le 20; $attempt++) {");
            script.AppendLine("        try {");
            script.AppendLine("            Move-Item -LiteralPath $from -Destination $to");
            script.AppendLine("            return");
            script.AppendLine("        } catch {");
            script.AppendLine("            Start-Sleep -Milliseconds 500");
            script.AppendLine("        }");
            script.AppendLine("    }");
            script.AppendLine("    throw \"could not move $from to $to\"");
            script.AppendLine("}");
            script.AppendLine();
            script.AppendLine("Wait-Process -Id " + gameProcessId + " -Timeout " + WaitForExitSeconds + " -ErrorAction SilentlyContinue");
            script.AppendLine("if (Get-Process -Id " + gameProcessId + " -ErrorAction SilentlyContinue) { exit 1 }");
            script.AppendLine();
            script.AppendLine("try {");
            script.AppendLine("    if (Test-Path -LiteralPath $previous) {");
            script.AppendLine("        Remove-Item -LiteralPath $previous -Recurse -Force");
            script.AppendLine("    }");
            script.AppendLine("    Move-Retrying $install $previous");
            script.AppendLine("    try {");
            script.AppendLine("        Move-Retrying $staged $install");
            script.AppendLine("    } catch {");
            script.AppendLine("        Move-Retrying $previous $install");
            script.AppendLine("    }");
            script.AppendLine("} finally {");
            script.AppendLine("    Start-Process -FilePath (Join-Path $install $executable) -WorkingDirectory $install");
            script.AppendLine("}");
            return script.ToString();
        }

        public static string Quote(string value)
        {
            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('\'');
            foreach (char c in value)
            {
                quoted.Append(c);
                if (IsSingleQuote(c))
                {
                    quoted.Append(c);
                }
            }

            quoted.Append('\'');
            return quoted.ToString();
        }

        private static bool IsSingleQuote(char c)
        {
            return c == '\'' || c == '\u2018' || c == '\u2019' || c == '\u201A' || c == '\u201B';
        }
    }
}
