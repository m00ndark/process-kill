using System;
using System.Diagnostics;

namespace ProcessKill;

internal static class Runner
{
	public static bool Execute(string path, string arguments, out string output)
	{
		using (Process process = new Process())
		{
			process.StartInfo = new ProcessStartInfo
				{
					FileName = path,
					Arguments = arguments,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
				};

			string localOutput = string.Empty;

			process.OutputDataReceived += (_, args) => localOutput += args.Data ?? string.Empty;

			process.ErrorDataReceived += (_, args) =>
				{
					if (args.Data != null)
					{
						Console.WriteLine(args.Data);
					}
				};

			//Log($"Executing '{path}' with arguments '{arguments}'");
			process.Start();

			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
			{
				//Log($"{Path.GetFileNameWithoutExtension(path)} timed out");
				process.Kill();
				output = null;
				return false;
			}

			output = localOutput;
			return process.ExitCode == 0;
		}
	}

}
