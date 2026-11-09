using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The player's delete verbs at the repository: one save point, or the whole run. The two
/// refusals that matter are the newest archive (the restore's own fallback) and a name that is
/// not one of the world's (a caller-supplied path must never be resolved).
/// </summary>
public sealed class SaveArchiveDeleteTests
{
	private const string FirstSavePoint = "mid-run-20261108-070314.cuoz";
	private const string SecondSavePoint = "mid-run-20261108-070315.cuoz";

	[Fact]
	public void DeleteBackup_AnUnknownName_IsRefused()
	{
		var fixture = SaveTestRepository.Create("delete-unknown");
		Directory.CreateDirectory(fixture.Workspace.BackupsDirectory(fixture.WorldId));
		File.WriteAllBytes(Path.Combine(fixture.Workspace.BackupsDirectory(fixture.WorldId), FirstSavePoint), []);

		var deleted = fixture.Repository.DeleteBackup(fixture.WorldId, "not-a-save-point.cuoz", out var refusal);

		Assert.False(deleted);
		Assert.False(string.IsNullOrEmpty(refusal));
		Assert.True(File.Exists(Path.Combine(fixture.Workspace.BackupsDirectory(fixture.WorldId), FirstSavePoint)));
	}

	[Fact]
	public void DeleteBackup_TheLastSavePoint_IsRefused()
	{
		var fixture = SaveTestRepository.Create("delete-last");
		var directory = fixture.Workspace.BackupsDirectory(fixture.WorldId);
		Directory.CreateDirectory(directory);
		File.WriteAllBytes(Path.Combine(directory, FirstSavePoint), []);

		var deleted = fixture.Repository.DeleteBackup(fixture.WorldId, FirstSavePoint, out var refusal);

		Assert.False(deleted);
		Assert.Contains("last save point", refusal);
		Assert.True(File.Exists(Path.Combine(directory, FirstSavePoint)));
	}

	[Fact]
	public void DeleteBackup_RemovesTheNamedArchiveAndKeepsTheRest()
	{
		var fixture = SaveTestRepository.Create("delete-one");
		var directory = fixture.Workspace.BackupsDirectory(fixture.WorldId);
		Directory.CreateDirectory(directory);
		File.WriteAllBytes(Path.Combine(directory, FirstSavePoint), []);
		File.WriteAllBytes(Path.Combine(directory, SecondSavePoint), []);

		var deleted = fixture.Repository.DeleteBackup(fixture.WorldId, SecondSavePoint, out var refusal);

		Assert.True(deleted, refusal);
		var remaining = fixture.Repository.ListBackups(fixture.WorldId);
		Assert.Single(remaining);
		Assert.Equal(FirstSavePoint, remaining[0].FileName);
	}

	[Fact]
	public void DeleteWorld_RemovesTheFolderAndEverythingUnderIt()
	{
		var fixture = SaveTestRepository.Create("delete-world");
		var worldDirectory = fixture.Workspace.WorldDirectory(fixture.WorldId);
		var backupDirectory = fixture.Workspace.BackupsDirectory(fixture.WorldId);
		Directory.CreateDirectory(backupDirectory);
		File.WriteAllBytes(Path.Combine(backupDirectory, FirstSavePoint), []);

		var deleted = fixture.Repository.DeleteWorld(fixture.WorldId, out var refusal);

		Assert.True(deleted, refusal);
		Assert.False(Directory.Exists(worldDirectory));
		Assert.Empty(fixture.Repository.ListWorlds());
	}

	[Fact]
	public void DeleteWorld_AnInvalidId_IsRefused()
	{
		var fixture = SaveTestRepository.Create("delete-bad-id");

		var deleted = fixture.Repository.DeleteWorld("../../etc", out var refusal);

		Assert.False(deleted);
		Assert.False(string.IsNullOrEmpty(refusal));
		Assert.True(Directory.Exists(fixture.Workspace.WorldDirectory(fixture.WorldId)));
	}
}
