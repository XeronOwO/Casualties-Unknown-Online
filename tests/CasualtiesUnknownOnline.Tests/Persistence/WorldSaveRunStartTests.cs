using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// A new run owns the next generation: the adapter's native handover for a
/// restore the world never consumed must be cancelled (B2). Left pending, the new
/// run's FIRST generation would be mistaken for the generation the cut was
/// restored for and would receive the old world's keypad codes, geyser liquid
/// types and partial block damage.
/// </summary>
public sealed class WorldSaveRunStartTests
{
	[Fact]
	public void TryBeginRun_CancelsTheAdaptersPendingNativeRestore()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(1, 2, 3f);
		native.ApplyBlockDamages(native.Damages);
		Assert.True(native.HasPendingRestore);
		using var fixture = WorldSaveFixture.Create("save-run-start-cancel", nativeWorldFacts: native);

		Assert.True(fixture.Service.TryBeginRun());

		Assert.False(native.HasPendingRestore);
		Assert.Contains("cancel-pending", native.Calls);
	}

	[Fact]
	public void TryBeginRun_WithoutPendingNativeFacts_RecordsNoCancel()
	{
		var native = new FakeNativeWorldFacts();
		using var fixture = WorldSaveFixture.Create("save-run-start-clean", nativeWorldFacts: native);

		Assert.True(fixture.Service.TryBeginRun());

		// The cancel is a real transition, not a call every start logs.
		Assert.DoesNotContain("cancel-pending", native.Calls);
	}

	[Fact]
	public void AbandonRestore_ReleasesEveryHandoverTheClickArmed()
	{
		// A continue whose run baseline cannot be published never reaches the
		// world-entry seam, so this is the last moment the hands can be released. Left
		// armed, the next generation would be mistaken for the restored layer's: the
		// seam skips its layer-boundary reset for a pending restore and writes the dead
		// attempt's facts into it.
		var entities = new FakeRestoredWorldEntitySource { Armed = true };
		using var fixture = WorldSaveFixture.Create("save-abandon-restore", worldEntities: entities);
		fixture.WorldFacts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], radiationLine: null, restoreSequence: 1);
		Assert.True(fixture.WorldFacts.HasPendingLiveReplay);

		fixture.Service.AbandonRestore("the restore published no run baseline, so no world generation will consume it");

		Assert.False(fixture.WorldFacts.HasPendingLiveReplay);
		Assert.False(entities.Armed);
		Assert.Single(entities.Cancels);
		Assert.Contains("no run baseline", Assert.Single(entities.Cancels), StringComparison.Ordinal);
	}
}
