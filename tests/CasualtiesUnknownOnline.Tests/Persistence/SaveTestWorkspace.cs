using System;
using System.IO;

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

	internal string StagingDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), ".staging");

	internal string PreviousDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), ".previous");

	internal string BackupsDirectory(string worldId) => Path.Combine(WorldDirectory(worldId), "backups");

	internal string ManifestPath(string worldId) => Path.Combine(LiveDirectory(worldId), "manifest.json");
}
