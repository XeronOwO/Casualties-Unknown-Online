using System;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Creates the per-file decode sessions the reader hands to a domain decoder.
/// Only the reader calls this: the session's file scope and its damage accounting
/// must stay in one place, so a decoder can never attribute a skip to the wrong
/// file or lose one.
/// </summary>
public static class SalvageDecode
{
	/// <summary>A session for one decode pass over one file.</summary>
	public static SalvageSession Create(int readerSchemaVersion, Action<string, string, string> report) =>
		new(readerSchemaVersion, report);
}
