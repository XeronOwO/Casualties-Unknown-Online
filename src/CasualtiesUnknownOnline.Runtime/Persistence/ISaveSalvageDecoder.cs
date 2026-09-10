using System.Text.Json;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The per-entry salvage contract (§6, decision 163): a domain decoder receives
/// ONE entry at a time from a readable domain file and reports each entry's
/// disposition. A single untranslatable entry — an unknown content id, a prefab
/// or template a mod update removed, an unmappable id — is skipped there and
/// then; the remaining entries of the same domain still apply, and the skip is
/// recorded with enough identity for the in-game report to name it.
/// </summary>
public interface ISaveSalvageDecoder
{
	/// <summary>The snapshot path currently being decoded; the same for every entry of one file.</summary>
	string CurrentPath { get; }

	/// <summary>The schema version this decode pass understands; an entry declaring a newer one is skipped, never guessed (§6.1).</summary>
	int ReaderSchemaVersion { get; }

	/// <summary>True = stop decoding this file; the entries already applied stay applied.</summary>
	bool ShouldStop { get; }

	/// <summary>Records that one entry cannot be materialized. <paramref name="id"/> names the affected content id.</summary>
	void Skip(string id, string reason, string detail);

	/// <summary>Reads an entry's own schema version, or 0 when the entry does not carry one.</summary>
	int ReadEntrySchemaVersion(JsonElement entry);
}
