using System.Collections.Generic;
using System.Linq;

namespace ProcessKill;

internal class ListArgument : Argument
{
	public override string Value
	{
		get => Values.FirstOrDefault();
		set => Values.Add(value);
	}

	public List<string> Values { get; } = [];

	public new static ListArgument Optional => new() { IsOptional = true };

	public new static ListArgument Mandatory => new() { IsOptional = false };
}

internal class Argument<T> : Argument
{
	public T ParsedValue { get; set; }

	public override bool IsFlag => typeof(T) == typeof(bool);

	public new static Argument<T> Optional => new() { IsOptional = true };

	public new static Argument<T> Mandatory => new() { IsOptional = false };

	public static implicit operator T(Argument<T> argument) => argument.ParsedValue;
}

internal class Argument
{
	public bool IsOptional { get; protected init; }

	public virtual string Value { get; set; }

	public bool HasValue => Value != null;

	public virtual bool IsFlag => false;

	public static Argument Optional => new() { IsOptional = true };

	public static Argument Mandatory => new() { IsOptional = false };

	public static implicit operator string(Argument argument) => argument.Value;

	public override string ToString()
	{
		return Value;
	}
}
