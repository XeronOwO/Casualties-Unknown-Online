using System;

namespace CasualtiesUnknownOnline.ContractTool.Cli;

/// <summary>
/// A command line the tool refuses to guess at. It is a distinct type so the
/// entry point can answer with the usage text and the documented exit code
/// instead of a stack trace.
/// </summary>
internal sealed class UsageException : Exception
{
	internal UsageException(string message)
		: base(message)
	{
	}
}
