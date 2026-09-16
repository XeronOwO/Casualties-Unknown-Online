using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The world folder's writer lease (S4 scope 5's concurrent-host case): one folder has
/// one writer, a second instance is refused BY NAME, a lease nobody refreshes goes
/// stale and is taken over, and this process's own lease is refreshed rather than
/// refused.
/// </summary>
public class WorldLeaseTests
{
	[Fact]
	public void AFreshForeignLease_RefusesTheWrite()
	{
		var fixture = SaveTestRepository.Create("lease-fresh");
		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now.AddMinutes(-1));

		var result = Write(fixture);

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.LeaseHeld, result.Reason);
		Assert.Contains("OTHER-MACHINE:4242", result.Detail, StringComparison.Ordinal);

		// Nothing was written: the world still holds the snapshot it had (none, here).
		Assert.False(File.Exists(fixture.ManifestPath));
		Assert.False(Directory.Exists(fixture.Workspace.StagingDirectory(fixture.WorldId)));
	}

	[Fact]
	public void ALeaseJustInsideTheWindow_IsStillRefused()
	{
		var fixture = SaveTestRepository.Create("lease-edge");
		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now.AddMinutes(-29));

		Assert.Equal(SaveWriteResult.Failure.LeaseHeld, Write(fixture).Reason);
	}

	[Fact]
	public void AStaleForeignLease_IsTakenOverAndRefreshed()
	{
		var fixture = SaveTestRepository.Create("lease-stale");
		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now.AddMinutes(-31));

		var result = Write(fixture);

		Assert.True(result.Success, result.Detail);
		var lease = WorldLease.Read(fixture.WorldDirectory, NullLogger.Instance);
		Assert.NotNull(lease);
		Assert.Equal(WorldLease.Owner, lease!.Owner);
		Assert.Equal(SaveArchiveFormat.FormatUtc(fixture.Now), lease.HeartbeatUtc);
	}

	[Fact]
	public void ALeaseThatNamesNoWriter_IsTreatedAsStale()
	{
		var fixture = SaveTestRepository.Create("lease-unreadable");
		File.WriteAllBytes(WorldLease.PathOf(fixture.WorldDirectory), SaveTestData.Bytes("{ not a lease"));

		Assert.True(Write(fixture).Success);
	}

	[Fact]
	public void ThisProcessesOwnLease_IsRefreshedRatherThanRefused()
	{
		var fixture = SaveTestRepository.Create("lease-own");

		Assert.True(Write(fixture).Success);
		Assert.True(Write(fixture).Success);
		Assert.Equal(WorldLease.Owner, WorldLease.Read(fixture.WorldDirectory, NullLogger.Instance)!.Owner);
	}

	[Fact]
	public void TryHoldWorld_NamesTheHolderAndTakesOverOnlyAStaleLease()
	{
		var fixture = SaveTestRepository.Create("lease-hold");
		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now);

		Assert.False(fixture.Repository.TryHoldWorld(fixture.WorldId, out var refusal));
		Assert.Contains("OTHER-MACHINE:4242", refusal, StringComparison.Ordinal);

		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now.AddHours(-2));
		Assert.True(fixture.Repository.TryHoldWorld(fixture.WorldId, out var none), none);
	}

	[Fact]
	public void ReleaseWorld_RemovesOurLeaseAndLeavesAForeignOneAlone()
	{
		var fixture = SaveTestRepository.Create("lease-release");
		Assert.True(Write(fixture).Success);

		fixture.Repository.ReleaseWorld(fixture.WorldId);
		Assert.Null(WorldLease.Read(fixture.WorldDirectory, NullLogger.Instance));

		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now);
		fixture.Repository.ReleaseWorld(fixture.WorldId);
		Assert.NotNull(WorldLease.Read(fixture.WorldDirectory, NullLogger.Instance));
	}

	[Fact]
	public void ContinuingAWorldAnotherInstanceIsWriting_IsRefusedBeforeAnythingIsApplied()
	{
		var fixture = SaveTestRepository.Create("lease-continue");
		Assert.True(Write(fixture).Success);

		// Another instance took the folder over and is playing that world.
		WriteLease(fixture, "OTHER-MACHINE:4242", fixture.Now);

		using var service = WorldSaveFixture.Create(
			"lease-continue", repository: fixture, utcNow: () => fixture.Now);

		Assert.False(service.Service.TryContinue(out var outcome));
		Assert.Contains("OTHER-MACHINE:4242", outcome.Summary, StringComparison.Ordinal);
		Assert.False(service.Service.HasArmedCut);
	}

	private static SaveWriteResult Write(SaveTestRepository fixture) =>
		fixture.Repository.WriteSnapshot(fixture.WorldId, fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("lease")));

	private static void WriteLease(SaveTestRepository fixture, string owner, DateTime heartbeatUtc) =>
		SaveArchiveJson.WriteFile(WorldLease.PathOf(fixture.WorldDirectory), new WorldLeaseEntry(owner, SaveArchiveFormat.FormatUtc(heartbeatUtc)));
}
