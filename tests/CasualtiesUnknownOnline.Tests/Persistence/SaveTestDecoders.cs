using System;
using System.IO;
using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The shape a real domain decoder has: the archive layer hands it ONE entry of
/// one file at a time, and it never has to know how the file was opened (live
/// folder or backup archive). A harness that tried to parse <c>manifest.json</c>
/// as an entry list would be testing a call pattern nobody uses.
/// </summary>
internal static class SaveTestDecoders
{
	/// <summary>
	/// Decodes the entries of one named domain file into their content ids, in
	/// order; an id is required, exactly as the real domain files declare one.
	/// </summary>
	internal static Action<JsonElement, SalvageSession> Ids(string fileName, Action<string, SalvageSession> apply) =>
		(entry, session) =>
		{
			if (!Matches(session.CurrentPath, fileName))
			{
				return;
			}

			apply(entry.GetProperty("id").GetString()!, session);
		};

	/// <summary>True = the snapshot path is the named domain file (compared by file name, then by full path).</summary>
	internal static bool Matches(string path, string fileName) =>
		string.Equals(path, fileName, StringComparison.Ordinal)
		|| string.Equals(Path.GetFileName(path), fileName, StringComparison.Ordinal);
}
