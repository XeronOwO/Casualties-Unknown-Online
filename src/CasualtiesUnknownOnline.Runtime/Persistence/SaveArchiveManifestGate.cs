using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The OPEN seam of a save snapshot: the bytes come out of a one-file archive or an
/// unpacked <c>live/</c> folder, the manifest decides whether they are usable, and a
/// damaged manifest is reported as damage rather than guessed around.
///
/// <para>
/// It is the same gate for both shapes — the archive reader and the legacy folder
/// reader hand it their entry list and get back the manifest or the reason it is
/// damaged — because "what the snapshot is" must not depend on where its bytes happen
/// to live. Deciding what to DO with the opened files (salvage, repair mode, fallback
/// order) stays with <see cref="SaveArchiveReader"/>; this class only says whether the
/// bytes are a sound snapshot.
/// </para>
/// </summary>
internal static class SaveArchiveManifestGate
{
	private const int EntryBufferSize = 81920;

	/// <summary>Opens one archive and returns its file entries; directory entries carry no content and are skipped.</summary>
	internal static List<ArchiveFileEntry> ReadArchiveEntries(string archivePath)
	{
		using var stream = File.OpenRead(archivePath);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		var files = new List<ArchiveFileEntry>(archive.Entries.Count);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var entry in archive.Entries)
		{
			if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
			{
				continue;
			}

			// Validated before the bytes are used: a hostile or corrupted entry name
			// must fail here, not at a later join onto a destination path.
			var path = ArchivePathPolicy.ValidateArchiveEntryName(entry.FullName);
			if (!seen.Add(path))
			{
				// Duplicate names are legal ZIP; an archive whose paths are ambiguous
				// cannot say which bytes a file is. That is damage, not a crash.
				throw new InvalidDataException($"the archive carries the entry '{path}' more than once");
			}

			using var entryStream = entry.Open();
			using var buffer = new MemoryStream();
			entryStream.CopyTo(buffer, EntryBufferSize);
			files.Add(new ArchiveFileEntry(path, buffer.ToArray()));
		}

		return files;
	}

	/// <summary>Lists a live snapshot's files as raw bytes; the manifest gate runs on the result.</summary>
	internal static List<ArchiveFileEntry> ReadLiveArchiveEntries(string liveDirectory) =>
		[.. ArchivePathPolicy.EnumerateFiles(liveDirectory)
			.Select(relative => new ArchiveFileEntry(relative, File.ReadAllBytes(ArchivePathPolicy.CombineWithin(liveDirectory, relative))))];

	/// <summary>
	/// Reads the manifest that is the gate (§3.2, §6.1): the JSON must carry every
	/// required field, the format marker and schema must match this build, and every
	/// listed path must be a safe, unique snapshot path. A manifest that fails any
	/// of these is *damaged* — the caller falls back to a backup rather than
	/// trusting a payload the manifest does not really describe.
	/// </summary>
	internal static bool TryReadManifest(IReadOnlyList<ArchiveFileEntry> files, out SaveManifest manifest, out string reason)
	{
		manifest = null!;
		var entry = files.FirstOrDefault(file => file.Path == SaveArchiveFormat.ManifestFileName);
		if (entry is null)
		{
			reason = "there is no manifest.json";
			return false;
		}

		try
		{
			if (!SaveArchiveJson.HasProperties(entry.Bytes, SaveManifest.RequiredProperties))
			{
				reason = "the manifest does not carry every required field";
				return false;
			}

			var parsed = SaveArchiveJson.Deserialize<SaveManifest>(entry.Bytes);
			if (parsed is null)
			{
				reason = "the manifest deserialized to null";
				return false;
			}

			if (!string.Equals(parsed.Format, SaveManifest.FormatMarker, StringComparison.Ordinal))
			{
				reason = $"the 'format' marker is '{parsed.Format}', not '{SaveManifest.FormatMarker}'";
				return false;
			}

			if (parsed.SchemaVersion != SaveManifest.CurrentSchemaVersion)
			{
				// Newer: the payload must not be guessed. Older: the reader has to map
				// it explicitly and S1 has one schema, so there is nothing to map yet.
				reason = parsed.SchemaVersion > SaveManifest.CurrentSchemaVersion
					? $"the manifest schemaVersion {parsed.SchemaVersion} is newer than this build's {SaveManifest.CurrentSchemaVersion}"
					: $"the manifest schemaVersion {parsed.SchemaVersion} is older than this build's {SaveManifest.CurrentSchemaVersion}";
				return false;
			}

			reason = DescribeFileListProblem(parsed);
			if (reason.Length > 0)
			{
				return false;
			}

			manifest = parsed;
			reason = string.Empty;
			return true;
		}
		catch (JsonException ex)
		{
			reason = $"the manifest is not valid JSON: {ex.Message}";
			return false;
		}
		catch (SaveArchivePathException ex)
		{
			reason = ex.Message;
			return false;
		}
	}

	/// <summary>The reason the manifest's file list is unusable, or "" when it is sound.</summary>
	private static string DescribeFileListProblem(SaveManifest manifest)
	{
		if (manifest.Files is null)
		{
			return "the manifest's 'files' list is null";
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in manifest.Files)
		{
			if (file is null)
			{
				return "the manifest's 'files' list carries a null entry";
			}

			var reason = ArchivePathPolicy.DescribeUnsafePath(file.Path);
			if (reason is not null)
			{
				return $"the manifest lists the unsafe path '{file.Path}': {reason}";
			}

			if (!seen.Add(file.Path))
			{
				return $"the manifest lists '{file.Path}' more than once";
			}
		}

		return string.Empty;
	}

	/// <summary>Reads every file the manifest lists out of the unpacked folder, reporting (and optionally verifying) each one.</summary>
	internal static List<SnapshotFile> ReadManifestFiles(string liveDirectory, SaveManifest manifest, WorldLoadOptions options, List<DamageReport.Entry> damage, ILogger log)
	{
		var files = new List<SnapshotFile>(manifest.Files.Count);
		foreach (var file in manifest.Files)
		{
			var path = ArchivePathPolicy.CombineWithin(liveDirectory, file.Path);
			if (!File.Exists(path))
			{
				damage.Add(FileEntry(DamageReport.EntryReason.FileUnreadable, file.Path, "the manifest lists it but the file is missing"));
				continue;
			}

			var bytes = File.ReadAllBytes(path);
			if (ShouldVerify(manifest, options))
			{
				VerifyBytes(file, bytes, damage, log);
			}

			files.Add(new SnapshotFile(file.Path, bytes, file));
		}

		return files;
	}

	/// <summary>True = this load's manifest requires its digests to be enforced (§3.2).</summary>
	internal static bool ShouldVerify(SaveManifest manifest, WorldLoadOptions options) =>
		options.VerifyChecksums && manifest.ChecksumPolicy == ChecksumPolicy.Required;

	internal static void VerifyBytes(SaveManifest.SaveManifestFile file, byte[] bytes, List<DamageReport.Entry> damage, ILogger log)
	{
		if (bytes.Length != file.Bytes)
		{
			damage.Add(FileEntry(DamageReport.EntryReason.ChecksumMismatch, file.Path,
				$"the file is {bytes.Length} bytes but the manifest says {file.Bytes}"));
			log.LogError("Checksum gate: {Path} is {Actual} bytes, manifest says {Expected}.", file.Path, bytes.Length, file.Bytes);
			return;
		}

		var digest = SaveArchiveChecksum.OfBytes(bytes);
		if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
		{
			damage.Add(FileEntry(DamageReport.EntryReason.ChecksumMismatch, file.Path,
				$"sha256 {digest} does not match the manifest's {file.Sha256}"));
			log.LogError("Checksum gate: {Path} hashes to {Actual}, manifest says {Expected}.", file.Path, digest, file.Sha256);
		}
	}

	/// <summary>The failures a load absorbs: file/archive damage and access problems, never a bug.</summary>
	internal static bool IsRecoverableReadFailure(Exception ex) =>
		ex is IOException
			or UnauthorizedAccessException
			or InvalidDataException
			or SaveArchivePathException
			or ArgumentException
			or JsonException;

	private static DamageReport.Entry FileEntry(DamageReport.EntryReason reason, string path, string detail) =>
		new(DamageReport.EntryScope.File, reason, path, string.Empty, detail);
}
