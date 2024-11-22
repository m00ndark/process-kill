using System;

namespace ProcessKill;

internal struct Parameters
{
	public ListArgument PathPatterns { get; set; }

	public ListArgument ArgumentsPatterns { get; set; }

	public Argument<StopServicesOption> StopServices { get; set; }

	public Argument<TimeSpan> StopTimeout { get; set; }

	public Argument<bool> DryRun { get; set; }

	public Argument<OutputType> OutputType { get; set; }

	public Argument<bool> Verbose { get; set; }
}
