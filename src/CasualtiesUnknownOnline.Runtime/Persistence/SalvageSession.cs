using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Accumulates one decode pass. The reader owns the callback's file scope; this
/// type owns the bookkeeping, so a decoder only has to say what it could not
/// apply. A retry pass reuses the same session, so nothing a decoder reported is
/// ever dropped from the account.
/// </summary>
public sealed class SalvageSession : ISaveSalvageDecoder
{
	private readonly List<DamageReport.Entry> _entries = [];
	private readonly int _readerSchemaVersion;
	private readonly Action<string, string, string> _report;

	internal SalvageSession(int readerSchemaVersion, Action<string, string, string> report)
	{
		_readerSchemaVersion = readerSchemaVersion;
		_report = report;
	}

	/// <inheritdoc />
	public string CurrentPath { get; private set; } = string.Empty;

	/// <inheritdoc />
	public int ReaderSchemaVersion => _readerSchemaVersion;

	/// <inheritdoc />
	public bool ShouldStop { get; private set; }

	/// <inheritdoc />
	public void Skip(string id, string reason, string detail)
	{
		_report(id, reason, detail);
		_entries.Add(new DamageReport.Entry(
			DamageReport.EntryScope.Entry,
			DamageReport.EntryReason.ContentMissing,
			CurrentPath,
			id,
			$"{reason} ({detail})"));
	}

	/// <summary>Stops this decode pass without failing the snapshot.</summary>
	public void Stop() => ShouldStop = true;

	/// <summary>Reads an entry's own <c>schemaVersion</c>, or 0 when the entry has none.</summary>
	public int ReadEntrySchemaVersion(JsonElement entry) => ReadEntrySchemaVersionOf(entry);

	/// <summary>Records that a decoder rejected an entry as a whole (not tied to a named content id).</summary>
	internal void RecordRejected(string id, string detail) => Add(DamageReport.EntryReason.DecoderRejected, id, detail);

	/// <summary>Records that an entry declares a schema newer than this reader understands (§6.1).</summary>
	internal void RecordSchemaNewer(string id, int schemaVersion) =>
		Add(DamageReport.EntryReason.EntrySchemaNewer, id,
			$"the entry's schemaVersion {schemaVersion} is newer than the reader's {ReaderSchemaVersion}; it is skipped, not guessed");

	/// <summary>Binds the session to the file whose entries it is about to receive.</summary>
	internal void BeginFile(string path)
	{
		CurrentPath = path;
		ShouldStop = false;
		_entries.Clear();
	}

	/// <summary>Every recorded item of the current file; clears the buffer for the next one.</summary>
	internal IReadOnlyList<DamageReport.Entry> EndFile()
	{
		var entries = new List<DamageReport.Entry>(_entries);
		_entries.Clear();
		CurrentPath = string.Empty;
		ShouldStop = false;
		return entries;
	}

	/// <summary>A name for an entry in the report: its content id when it has one, its position otherwise.</summary>
	internal static string IdOf(JsonElement entry, int index) =>
		entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Null
			? id.ToString()
			: $"<entry {index}>";

	private static int ReadEntrySchemaVersionOf(JsonElement entry) =>
		entry.ValueKind == JsonValueKind.Object
			&& entry.TryGetProperty("schemaVersion", out var schema)
			&& schema.TryGetInt32(out var version)
				? version
				: 0;

	private void Add(DamageReport.EntryReason reason, string id, string detail) =>
		_entries.Add(new DamageReport.Entry(DamageReport.EntryScope.Entry, reason, CurrentPath, id, detail));
}
