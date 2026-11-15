using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// A throwaway repository root under the machine's temp directory, built at
/// runtime so no absolute machine path ever enters the repository (AGENTS.md
/// red line) and two parallel test classes never share a folder.
/// </summary>
internal sealed class SaveTestWorkspace
{
	private SaveTestWorkspace(string root)
	{
		Root = root;
	}

	/// <summary>The repository root (a <c>cuo/saves</c> shape folder of its own).</summary>
	internal string Root { get; }

	internal static SaveTestWorkspace Create(string label) =>
		new(Path.Combine(Path.GetTempPath(), "cuo-save-tests", label, Guid.NewGuid().ToString("N")));

	/// <summary>A world id this workspace owns, created on disk with its metadata.</summary>
	internal string NewWorldId() => "w-20260910-" + Guid.NewGuid().ToString("N").Substring(0, 4);

	internal string WorldDirectory(string worldId) => Path.Combine(Root, worldId);

	internal string LiveDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), "live");

	internal string LiveArchivePath(string worldId) => Path.Combine(WorldDirectory(worldId), SaveArchiveFormat.LiveArchiveFileName);

	/// <summary>
	/// Extracts the committed one-file snapshot back to the legacy live folder and
	/// removes the archive, so a test that mutates a live payload exercises the
	/// compatibility read path instead of the archive path.
	/// </summary>
	internal void MaterializeLegacyLive(string worldId)
	{
		var archive = LiveArchivePath(worldId);
		var live = LiveDirectory(worldId);
		if (!File.Exists(archive))
		{
			// A test fixture that writes more than one cut has already expanded the
			// previous archive; the live folder is the current readable copy.
			Assert.True(Directory.Exists(live), "the writer must commit a live archive");
			return;
		}

		if (Directory.Exists(live))
		{
			Directory.Delete(live, recursive: true);
		}

		Directory.CreateDirectory(live);
		ZipFile.ExtractToDirectory(archive, live);
		File.Delete(archive);
	}

	/// <summary>Reads a live payload entry from the one-file archive, or from a legacy live folder.</summary>
	internal byte[] ReadLiveFile(string worldId, string path)
	{
		var legacy = Path.Combine(LiveDirectory(worldId), path.Replace('/', Path.DirectorySeparatorChar));
		if (File.Exists(legacy))
		{
			return File.ReadAllBytes(legacy);
		}

		using var archive = ZipFile.OpenRead(LiveArchivePath(worldId));
		var entry = archive.GetEntry(path) ?? throw new FileNotFoundException($"the live archive has no '{path}' entry", path);
		using var stream = entry.Open();
		using var copy = new MemoryStream();
		stream.CopyTo(copy);
		return copy.ToArray();
	}

	/// <summary>The live manifest, read from the archive or from a legacy live folder.</summary>
	internal JsonElement ReadLiveManifest(string worldId)
	{
		using var document = JsonDocument.Parse(ReadLiveFile(worldId, SaveArchiveFormat.ManifestFileName));
		return document.RootElement.Clone();
	}

	/// <summary>
	/// Test-only readable copy of the committed archive. The archive stays
	/// authoritative; the save-domain suites read the unpacked shape by path.
	/// </summary>
	internal void ExpandLiveCopy(string worldId)
	{
		var archive = LiveArchivePath(worldId);
		if (!File.Exists(archive))
		{
			return;
		}

		var live = LiveDirectory(worldId);
		if (Directory.Exists(live))
		{
			Directory.Delete(live, recursive: true);
		}

		Directory.CreateDirectory(live);
		ZipFile.ExtractToDirectory(archive, live);
	}

	internal string StagingDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), ".staging");

	internal string PreviousDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), ".previous");

	internal string BackupsDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), "backups");

	internal string ManifestPath(string worldId) => Path.Combine(LiveDirectory(worldId), "manifest.json");
}
