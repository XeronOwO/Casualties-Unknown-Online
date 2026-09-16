using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The salvage pass of §6: it offers every entry of every file to the caller's decoder,
/// ONE entry at a time, and accumulates what was skipped. Split out of
/// <see cref="SaveArchiveReader"/>, which owns OPENING a snapshot (the manifest gate, the
/// live folder, the backup fallback): what to do with the entries is a separate question,
/// and the reader reached the architecture line limit once the recovery's per-backup open
/// joined it.
///
/// Per entry, never per domain (decision 163): a decoder that throws loses that entry and
/// nothing else, an entry whose <c>schemaVersion</c> is newer than this reader understands
/// is skipped BY NAME, and a decoder that asks to stop ends the pass with the entries
/// already applied staying applied.
/// </summary>
internal sealed class SaveSalvagePass(ILogger log)
{
	private readonly ILogger _log = log;

	/// <summary>
	/// Runs the decoder over the snapshot's files. The content is returned unchanged — this
	/// pass reports, it does not transform — beside the account of everything it skipped.
	/// </summary>
	internal (WorldSnapshotContent Content, SalvageResult Salvage) Run(
		WorldSnapshotContent content,
		Action<JsonElement, SalvageSession> decode,
		WorldLoadOptions options)
	{
		var damage = new List<DamageReport.Entry>();
		foreach (var file in content.Files)
		{
			if (!SalvageFile(file, decode, options, damage))
			{
				_log.LogWarning("Salvage stopped after {Path}: the decoder asked to stop; the remaining files are not decoded.", file.Path);
				break;
			}
		}

		var salvage = new SalvageResult(new DamageReport(damage));
		_log.LogInformation("Salvage pass over {FileCount} file(s) of world {WorldId}: {Damage}.",
			content.Files.Count, content.Manifest.WorldId, salvage.Report.Describe());
		return (content, salvage);
	}

	/// <summary>Runs the decoder over one file's entries. False = the decoder asked to stop after this file.</summary>
	private bool SalvageFile(SnapshotFile file, Action<JsonElement, SalvageSession> decode, WorldLoadOptions options, List<DamageReport.Entry> damage)
	{
		var session = SalvageDecode.Create(options.ReaderSchemaVersion, (id, reason, detail) =>
			_log.LogWarning("Salvage in {Path}: entry {Id} was skipped — {Reason} ({Detail}).", file.Path, id, reason, detail));
		session.BeginFile(file.Path);

		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(file.Bytes);
		}
		catch (JsonException ex)
		{
			_log.LogWarning(ex, "Salvage cannot parse {Path} as JSON; the file is skipped.", file.Path);
			damage.Add(FileEntry(DamageReport.EntryReason.FileInvalidJson, file.Path, ex.Message));
			return true;
		}

		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Array)
			{
				// A domain file is an entry list by contract. A non-array payload is a
				// whole-file problem: a decoder that guessed at its shape would be
				// exactly the guessing §6.1 forbids.
				_log.LogWarning("Salvage skipped {Path}: the payload is {Kind}, not an entry array.", file.Path, document.RootElement.ValueKind);
				damage.Add(FileEntry(DamageReport.EntryReason.FileDecodeFailed, file.Path,
					$"the payload is {document.RootElement.ValueKind}, not an entry array"));
				return true;
			}

			var index = 0;
			foreach (var entry in document.RootElement.EnumerateArray())
			{
				if (session.ShouldStop)
				{
					_log.LogInformation("Salvage of {Path} stopped at the decoder's request; the entries already applied stay applied.", file.Path);
					break;
				}

				InvokeDecoder(entry, index, decode, session);
				index++;
			}

			damage.AddRange(session.EndFile());
			return !session.ShouldStop;
		}
	}

	private void InvokeDecoder(JsonElement entry, int index, Action<JsonElement, SalvageSession> decode, SalvageSession session)
	{
		var id = SalvageSession.IdOf(entry, index);
		var schemaVersion = session.ReadEntrySchemaVersion(entry);
		if (schemaVersion > session.ReaderSchemaVersion)
		{
			session.RecordSchemaNewer(id, schemaVersion);
			_log.LogWarning("Salvage in {Path}: entry {Id} declares schemaVersion {Schema} > {Reader}; skipped.",
				session.CurrentPath, id, schemaVersion, session.ReaderSchemaVersion);
			return;
		}

		try
		{
			decode(entry, session);
		}
		catch (Exception ex)
		{
			// One entry's decoder failure must not take the domain with it (§6): the
			// next entry is offered to the decoder as usual.
			session.RecordRejected(id, $"{ex.GetType().Name}: {ex.Message}");
			_log.LogWarning(ex, "Salvage in {Path}: the decoder threw for entry {Id}; the entry is skipped.", session.CurrentPath, id);
		}
	}

	private static DamageReport.Entry FileEntry(DamageReport.EntryReason reason, string path, string detail) =>
		new(DamageReport.EntryScope.File, reason, path, string.Empty, detail);
}
