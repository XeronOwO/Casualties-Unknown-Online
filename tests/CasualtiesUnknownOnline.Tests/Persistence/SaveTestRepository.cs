using System;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The production composition of the persistence layer on a temp root: the
/// repository plus the writer and reader wired the way the plugin will wire them
/// in S2. Keeping one factory means a test never hand-rolls a half-configured
/// repository.
/// </summary>
internal sealed record SaveTestRepository(WorldRepository Repository, SaveTestWorkspace Workspace, string WorldId, DateTime Now, WorldMetadata Seed, SaveArchiveWriter Writer)
{
	internal static SaveTestRepository Create(string label, string displayName = "Index World")
	{
		var workspace = SaveTestWorkspace.Create(label);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var now = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
		var repository = new WorldRepository(workspace.Root, NullLogger<WorldRepository>.Instance, writer, reader, () => now);
		var created = repository.CreateWorld(displayName);
		if (!created.Success || created.Metadata is null)
		{
			throw new InvalidOperationException($"the test repository could not create a world: {created.Failure}");
		}

		return new SaveTestRepository(repository, workspace, created.WorldId, now, created.Metadata, writer);
	}

	internal string WorldDirectory => Workspace.WorldDirectory(WorldId);

	internal string LiveDirectory => Workspace.LiveDirectory(WorldId);

	internal string ManifestPath => Workspace.ManifestPath(WorldId);

	internal SaveWorldRequest Request(WorldCutKind kind, DateTime savedAtUtc, params SavePayloadFile[] payload) =>
		SaveTestData.Request(WorldId, kind, savedAtUtc, payload);
}
