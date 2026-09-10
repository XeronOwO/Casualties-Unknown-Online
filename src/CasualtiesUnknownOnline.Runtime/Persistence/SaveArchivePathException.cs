using System;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Raised when a name would escape its archive root: a payload path handed to
/// the writer, a player key used as a file name, or a ZIP entry read from a
/// backup. The archive layer never rewrites such a name into a safe one — it
/// refuses the whole operation and reports the offending text, because a
/// silent rename would turn an attack or a corruption into a mystery.
/// </summary>
public sealed class SaveArchivePathException : Exception
{
	public SaveArchivePathException()
		: this(string.Empty, "the path is not a safe archive-relative path")
	{
	}

	public SaveArchivePathException(string message)
		: base(message)
	{
		OffendingPath = string.Empty;
		Reason = message;
	}

	public SaveArchivePathException(string message, Exception innerException)
		: base(message, innerException)
	{
		OffendingPath = string.Empty;
		Reason = message;
	}

	public SaveArchivePathException(string offendingPath, string reason)
		: base($"Unsafe archive path '{offendingPath}': {reason}.")
	{
		OffendingPath = offendingPath;
		Reason = reason;
	}

	/// <summary>The exact text that was rejected.</summary>
	public string OffendingPath { get; }

	/// <summary>Why it was rejected (stable, log- and report-friendly).</summary>
	public string Reason { get; }
}
