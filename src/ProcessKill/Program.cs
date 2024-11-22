using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessKill;

internal class Program
{
	private const string ARG_PATH = "path";
	private const string ARG_ARGS = "args";
	private const string ARG_STOPSERVICES = "stopservices";
	private const string ARG_STOPTIMEOUT = "stoptimeout";
	private const string ARG_DRY = "dry";
	private const string ARG_OUTPUT = "output";
	private const string ARG_VERBOSE = "verbose";

	private static readonly Dictionary<string, Argument> _suppliedArgs = new()
		{
			{ ARG_PATH, ListArgument.Optional },
			{ ARG_ARGS, ListArgument.Optional },
			{ ARG_STOPSERVICES, Argument<StopServicesOption>.Optional },
			{ ARG_STOPTIMEOUT, Argument<TimeSpan>.Optional },
			{ ARG_DRY, Argument<bool>.Optional },
			{ ARG_OUTPUT, Argument<OutputType>.Optional },
			{ ARG_VERBOSE, Argument<bool>.Optional },
		};

	public static void Main(string[] args)
	{
		Console.WriteLine();

		if (!TryParseArgs(args, out Parameters parameters))
		{
			return;
		}

		Killer.Execute(parameters);
	}

	private static bool TryParseArgs(string[] args, out Parameters parameters)
	{
		parameters = new Parameters();

		for (int i = 0; i < args.Length; i++)
		{
			if (!args[i].StartsWith("--"))
			{
				PrintUsage($"Bad argument '{args[i]}'");
				return false;
			}

			string arg = args[i][2..];

			if (!_suppliedArgs.TryGetValue(arg, out Argument argument))
			{
				PrintUsage($"Invalid argument '{args[i]}'");
				return false;
			}

			if (argument.IsFlag)
			{
				Argument<bool> flagArg = (Argument<bool>)argument;
				if (args.Length > i + 1 && bool.TryParse(args[i + 1], out bool flag))
				{
					i++;
					flagArg.ParsedValue = flag;
				}
				else
				{
					flagArg.ParsedValue = true;
				}
			}
			else
			{
				if (args.Length <= i + 1 || args[i + 1].StartsWith("--"))
				{
					PrintUsage($"Argument '{args[i]}' is missing value");
					return false;
				}

				argument.Value = args[++i].Trim('\'', '"');
			}
		}

		if (_suppliedArgs.Values.Any(arg => !arg.IsOptional && !arg.HasValue))
		{
			PrintUsage("Arguments missing");
			return false;
		}

		parameters = new Parameters
			{
				PathPatterns = (ListArgument)_suppliedArgs[ARG_PATH],
				ArgumentsPatterns = (ListArgument)_suppliedArgs[ARG_ARGS],
				StopServices = (Argument<StopServicesOption>)_suppliedArgs[ARG_STOPSERVICES],
				StopTimeout = (Argument<TimeSpan>)_suppliedArgs[ARG_STOPTIMEOUT],
				DryRun = (Argument<bool>)_suppliedArgs[ARG_DRY],
				OutputType = (Argument<OutputType>)_suppliedArgs[ARG_OUTPUT],
				Verbose = (Argument<bool>)_suppliedArgs[ARG_VERBOSE],
			};

		if (parameters.StopServices.HasValue)
		{
			if (!Enum.TryParse(parameters.StopServices, ignoreCase: true, out StopServicesOption stopServicesOption))
			{
				PrintUsage("The specified stop services value is not a valid option");
				return false;
			}

			parameters.StopServices.ParsedValue = stopServicesOption;
		}

		if (parameters.StopTimeout.HasValue)
		{
			if (!int.TryParse(parameters.StopTimeout, out int timeoutSeconds))
			{
				PrintUsage("The specified stop timeout value is not a valid number");
				return false;
			}

			parameters.StopTimeout.ParsedValue = TimeSpan.FromSeconds(timeoutSeconds);
		}
		else
		{
			parameters.StopTimeout.ParsedValue = TimeSpan.FromSeconds(10);
		}

		if (parameters.OutputType.HasValue)
		{
			if (!Enum.TryParse(parameters.OutputType, ignoreCase: true, out OutputType outputType))
			{
				PrintUsage("The specified output value is not a valid type");
				return false;
			}

			parameters.OutputType.ParsedValue = outputType;
		}
		else
		{
			parameters.OutputType.ParsedValue = OutputType.Result;
		}

		if (!parameters.PathPatterns.Values.Any() && !parameters.ArgumentsPatterns.Values.Any())
		{
			PrintUsage("Either --path or --args must be provided");
			return false;
		}

		return true;
	}

	private static void PrintUsage(string error)
	{
		Console.WriteLine($"{error}.");
		Console.WriteLine();
		Console.WriteLine("Usage:");
		Console.WriteLine("  ProcessKill [arguments]");
		Console.WriteLine();
		Console.WriteLine("Arguments:");
		Console.WriteLine("  --path <path-regex>        Regex matching the paths of processes to kill. Multiple occurrences allowed.");
		Console.WriteLine("  --args <args-regex>        Regex matching the command line arguments of processes to kill. Multiple");
		Console.WriteLine("                             occurrences allowed.");
		Console.WriteLine("  --stopservices <option>    Indicates that for a process that is identified as a service, an attempt to");
		Console.WriteLine("                             stop the service will be made before killing it; either if <option> is 'all'");
		Console.WriteLine("                             or if <option> is 'recovery' and the service has configured recovery options.");
		Console.WriteLine("  --stoptimeout <timeout>    The max amount of time to wait for a service to stop. Default value is 10 sec.");
		Console.WriteLine("  --dry                      Indicates that this should be a dry run. No processes will be killed.");
		Console.WriteLine("  --output <type>            Describes how this application should provide output; <type> can be either");
		Console.WriteLine("                             'progress' or 'result' (default).");
		Console.WriteLine("  --verbose                  Writes additional output information.");
		Console.WriteLine();
	}
}
