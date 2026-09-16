using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S4 scope 5's failure-degradation matrix: a full disk, a directory that cannot be
/// written, and an unusable save root each have a defined, logged, non-crashing
/// behaviour — the previous snapshot stays live, the run keeps playing, and the cut
/// that could not be written is refused by name.
/// </summary>
public class WorldSaveDegradationTests
{
	private const ulong HostId = 1001UL;
	private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void ADiskThatFillsMidTransaction_RefusesTheCutAndKeepsThePreviousSnapshot()
	{
		var now = Noon;
		using var fixture = RunningWorld("degrade-diskfull", () => now);
		now = now.AddMinutes(1);
		var first = Cut(fixture);
		Assert.True(first.Captured);

		// The staging seam stands in for the disk filling between the payload write and
		// the commit: an IOException is exactly what a full volume raises.
		fixture.Repository.Writer.StagedHook = () => throw new IOException("There is not enough space on the disk.");
		now = now.AddMinutes(1);
		var refused = Cut(fixture);
		fixture.Repository.Writer.StagedHook = null;

		Assert.False(refused.Captured);
		Assert.Contains("StageFailed", refused.Summary, StringComparison.Ordinal);

		// The world is exactly where it was, and the session can still write once the
		// cause is gone (the leftover staging folder is reset by the next transaction).
		Assert.True(fixture.Repository.Repository.HasSnapshot(fixture.WorldId));
		now = now.AddMinutes(1);
		Assert.True(Cut(fixture).Captured);
	}

	[Fact]
	public void AReadOnlySaveDirectory_RefusesTheCutAndKeepsThePreviousSnapshot()
	{
		var now = Noon;
		using var fixture = RunningWorld("degrade-readonly", () => now);
		now = now.AddMinutes(1);
		Assert.True(Cut(fixture).Captured);

		fixture.Repository.Writer.StagedHook = () => throw new UnauthorizedAccessException("Access to the path is denied.");
		now = now.AddMinutes(1);
		var refused = Cut(fixture);
		fixture.Repository.Writer.StagedHook = null;

		Assert.False(refused.Captured);
		Assert.Contains("StageFailed", refused.Summary, StringComparison.Ordinal);
		Assert.True(fixture.Repository.Repository.HasSnapshot(fixture.WorldId));
	}

	[Fact]
	public void ASaveRootThatCannotBeADirectory_FailsTheRunWithoutThrowing()
	{
		var repository = UnusableRepository("degrade-root");
		var kernel = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		using var service = new WorldSaveService(
			repository,
			new FakeSessionControl { LocalSteamId = HostId, HostSteamId = HostId },
			new FakeCharacterDataControl(),
			kernel,
			new FakeTransportIdentity { LocalPeerId = HostId, LocalDisplayName = "Host" },
			new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance),
			new FakeWorldFactSource(),
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance,
			utcNow: () => Noon);

		// The run plays; it just cannot be saved, and the Continue entry has nothing to
		// offer instead of throwing out of the picker.
		Assert.False(service.TryBeginRun(isTutorial: false));
		Assert.Equal(string.Empty, service.CurrentWorldId);
		Assert.False(service.HasRestorableWorld);
		Assert.Null(service.ContinueWorldId);
		Assert.False(service.TryRequestCut(WorldCutReason.Command, out var refusal));
		Assert.Contains("world", refusal, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void AWorldFolderThatCannotBeCreated_IsReportedByTheRepository()
	{
		var created = UnusableRepository("degrade-write").CreateWorld("Unwritable");

		Assert.False(created.Success);
		Assert.False(string.IsNullOrEmpty(created.Failure));
	}

	/// <summary>
	/// A repository whose root cannot exist: a file sits where the <c>saves</c> folder has
	/// to be, so every directory creation below it raises IOException — which is what a
	/// read-only or vanished drive looks like to this code, and the same arrival point
	/// scope 5's read-only-directory case uses.
	/// </summary>
	private static WorldRepository UnusableRepository(string label)
	{
		var workspace = SaveTestWorkspace.Create(label);
		Directory.CreateDirectory(workspace.Root);
		var blocker = Path.Combine(workspace.Root, "blocker");
		File.WriteAllBytes(blocker, SaveTestData.Bytes("not a directory"));

		return new WorldRepository(
			Path.Combine(blocker, "saves"),
			NullLogger<WorldRepository>.Instance,
			new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance),
			new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance),
			() => Noon);
	}

	private static WorldSaveFixture RunningWorld(string label, Func<DateTime> now)
	{
		var fixture = WorldSaveFixture.Create(label, utcNow: now, repository: SaveTestRepository.Create(label));
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		return fixture;
	}

	private static WorldCutReport Cut(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal), refusal);
		return Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
	}
}
