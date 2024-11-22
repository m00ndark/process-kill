using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace ProcessKill;

internal static class Killer
{
	private static readonly string[] _regexSpecialChars = ["\\", ".", "(", ")", "[", "]", "^", "$"];

	public static void Execute(Parameters parameters)
	{
		bool outputProgress = parameters.OutputType == OutputType.Progress;
		string stopping = parameters.DryRun ? "Would stop" : "Stopping";
		string killing = parameters.DryRun ? "Would kill" : "Killing";

		(ServiceController Service, string ExecutablePath)[] services = parameters.StopServices.HasValue
			? ServiceController
				.GetServices()
				.Where(service => service.Status == ServiceControllerStatus.Running)
				.WithMetadata()
			: null;

		ServiceController GetServiceOrDefault(string processPath)
		{
			return services?
				.Where(service => service.ExecutablePath.Contains(processPath))
				.Select(service => service.Service)
				.FirstOrDefault();
		}

		foreach ((Process Process, string Path, string CommandLine, string Arguments, ServiceController Service) processInfo in GetProcesses()
			.Where(x => x.Process.Id != Environment.ProcessId)
			.Where(x => !string.IsNullOrEmpty(x.CommandLine))
			.Select(x => (x.Process, x.Path, x.CommandLine, Arguments: x.CommandLine.GetArguments(x.Path)))
			.Where(x => parameters.PathPatterns.Match(x.Path))
			.Where(x => parameters.ArgumentsPatterns.Match(x.Arguments))
			.Select(x => (x.Process, x.Path, x.CommandLine, x.Arguments, Service: GetServiceOrDefault(x.Path)))
			.OrderBy(x => Path.GetDirectoryName(x.Path))
			.ThenBy(x => x.Service == null)
			.ThenBy(x => Path.GetFileName(x.Path)))
		{
			bool failed = false;
			bool isService = false;
			bool stoppedService = false;
			bool killedProcess = false;

			try
			{
				PrintProgress(outputProgress, $"PID: {processInfo.Process.Id} - {processInfo.Path}");

				if (parameters.Verbose)
				{
					PrintProgress(outputProgress, $"CMD: {processInfo.CommandLine}");
				}

				if (processInfo.Service != null)
				{
					isService = true;
					bool shouldStopService = false;

					if (parameters.StopServices == StopServicesOption.All)
					{
						PrintProgress(outputProgress, $"{stopping} service '{processInfo.Service.ServiceName}'...");
						shouldStopService = true;
					}
					else if (parameters.StopServices == StopServicesOption.Recovery && processInfo.Service.HasRestartRecovery())
					{
						PrintProgress(outputProgress, $"{stopping} service '{processInfo.Service.ServiceName}' because it has recovery options...");
						shouldStopService = true;
					}

					if (!parameters.DryRun && shouldStopService)
					{
						if (processInfo.Service.StopAndWait(processInfo.Process, parameters.StopTimeout))
						{
							stoppedService = true;
							continue;
						}
						else
						{
							PrintProgress(outputProgress, "Failed to stop service.");
						}
					}
				}

				PrintProgress(outputProgress, $"{killing} process '{Path.GetFileName(processInfo.Path)}'...");
				if (!parameters.DryRun && !processInfo.Process.HasExited)
				{
					processInfo.Process.Kill(true);
					killedProcess = true;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				failed = true;
			}
			finally
			{
				string action = failed
					? " FAILED"
					: parameters.DryRun
						? " DRYRUN"
						: stoppedService
							? "STOPPED"
							: killedProcess
								? " KILLED"
								: " EXITED";

				string type = isService
					? "Service"
					: "Process";

				PrintResult(outputProgress, $"{action}: {type} PID {processInfo.Process.Id} - {processInfo.Path}");
				PrintProgress(outputProgress);
			}
		}
	}

	private static bool Match(this ListArgument patternArgument, string input)
	{
		return !patternArgument.HasValue || patternArgument.Values.Any(pattern => Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
	}

	private static (ServiceController Service, string ExecutablePath)[] WithMetadata(this IEnumerable<ServiceController> services)
	{
		using (ManagementObjectSearcher managementObjectSearcher = new("SELECT Name, PathName FROM Win32_Service"))
		{
			using (ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get())
			{
				ManagementObject[] managementObjects = managementObjectCollection
					.Cast<ManagementObject>()
					.ToArray();

				return services
					.Select(service => (
						Service: service,
						ExecutablePath: managementObjects
							.Where(obj => (string)obj["Name"] == service.ServiceName)
							.Select(obj => (string)obj["PathName"])
							.FirstOrDefault()))
					.ToArray();
			}
		}
	}

	private static bool HasRestartRecovery(this ServiceController service)
	{
		return Runner.Execute("sc.exe", $"qFailure \"{service.ServiceName}\"", out string output)
			&& Regex.IsMatch(output, @"FAILURE_ACTIONS\s+:\s+RESTART");
	}

	private static (Process Process, string Path, string CommandLine)[] GetProcesses()
	{
		using (ManagementObjectSearcher managementObjectSearcher = new("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process"))
		{
			using (ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get())
			{
				return Process
					.GetProcesses()
					.Join(
						managementObjectCollection.Cast<ManagementObject>(),
						process => process.Id,
						managementObject => (int)(uint)managementObject["ProcessId"],
						(process, managementObject) => (Process: process, Path: (string)managementObject["ExecutablePath"], CommandLine: (string)managementObject["CommandLine"]))
					.ToArray();
			}
		}
	}

	private static string GetArguments(this string commandLine, string processPath)
	{
		string escapedProcessPath = _regexSpecialChars
			.Aggregate(processPath, (path, ch) => path.Replace(ch, $"\\{ch}"));

		string pattern = @$"^""?(?:(?:\\\?\?\\)?{escapedProcessPath}|{escapedProcessPath.Replace(@"\\", "/")}|(?:\.\\|\./)?{Path.GetFileName(processPath)}|{Path.GetFileNameWithoutExtension(processPath)})""?(?:\s+|$)";

		return Regex.Replace(
			commandLine,
			pattern,
			string.Empty,
			RegexOptions.IgnoreCase);
	}

	private static bool StopAndWait(this ServiceController service, Process process, TimeSpan timeout)
	{
		try
		{
			service.Stop();

			if (process.WaitForExit(timeout))
			{
				return true;
			}
		}
		catch
		{
			// nothing
		}

		return false;
	}

	private static void PrintProgress(bool outputProgress, string message = "")
	{
		if (outputProgress)
		{
			Console.WriteLine(message);
		}
	}

	private static void PrintResult(bool outputProgress, string message)
	{
		if (!outputProgress)
		{
			Console.WriteLine(message);
		}
	}
}
