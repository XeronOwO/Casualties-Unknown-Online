using System;
using System.IO;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The one-file live shape: a backup is promoted by copying the archive into
/// live.cuoz, never by unpacking a folder. The replaced archive is preserved and
/// the new live archive still reads through the normal repository/reader path.
/// </summary>
public class SaveArchivePromotionTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void ABackupArchive_ReplacesTheLiveArchiveWithoutUnpackingAFolder()
	{
		var workspace = SaveTestWorkspace.Create("promote-archive");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);

		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("first"))).Success);
		var first = Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Single();
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut.AddMinutes(1), SaveTestData.RunPayload("second"))).Success);
		var second = Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Single(path => !string.Equals(path, first, StringComparison.Ordinal));
		File.Copy(first, workspace.LiveArchivePath(worldId), overwrite: true);

		var promoted = WorldBackup.TryParse(workspace.BackupsDirectory(worldId), Path.GetFileName(second));
		Assert.NotNull(promoted);
		var result = WorldBackupPromotion.Promote(directory, promoted!, WorldPromotionTrigger.PlayerChoice, Cut.AddMinutes(2), NullLogger.Instance);

		Assert.True(result.Success, result.Detail);
		Assert.False(Directory.Exists(workspace.LiveDirectory(worldId)), "the one-file shape has no loose live folder");
		Assert.Equal(File.ReadAllBytes(second), File.ReadAllBytes(workspace.LiveArchivePath(worldId)));
		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());
		Assert.True(load.Loaded, load.Summary);
		Assert.Contains("second", Encoding.UTF8.GetString(load.Content!.Files.Single(file => file.Path == SaveTestData.RunFileName).Bytes), StringComparison.Ordinal);
	}
}
