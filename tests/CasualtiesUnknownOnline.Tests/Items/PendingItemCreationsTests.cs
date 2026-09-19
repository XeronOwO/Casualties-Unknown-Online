using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The SENDING half of the creation-before-operation invariant: before an
/// operation report leaves a guest, every deferred creation report is settled,
/// so the host judges the creation first and never has to hold or guess. The
/// gate is guest-only — the host is its own authority and never reports an
/// operation to itself — and a composition that registered no source (a unit
/// composition with no game scene) simply defers nothing.
/// </summary>
[Trait("Category", "Integration")]
public class PendingItemCreationsTests
{
	private sealed class RecordingCreationSource : IPendingItemCreationSource
	{
		internal int Settles { get; private set; }

		public void SettlePendingCreations() => Settles++;
	}

	[Fact]
	public void AGuestSettlesItsDeferredCreationsBeforeItReportsAnOperation()
	{
		using var w = ItemSimWorld.Create();
		var source = new RecordingCreationSource();
		var g1 = w.G1.Services.GetRequiredService<IItemControl>();
		g1.RegisterPendingCreationSource(source);

		g1.SendItemPickedUp(42);

		Assert.Equal(1, source.Settles);
	}

	[Fact]
	public void EveryOperationReportSettles_TheWholeOperationFamily()
	{
		using var w = ItemSimWorld.Create();
		var source = new RecordingCreationSource();
		var g1 = w.G1.Services.GetRequiredService<IItemControl>();
		g1.RegisterPendingCreationSource(source);
		var item = new CharacterItemMsg { ItemId = "test_item", Condition = 1f };

		g1.SendItemUse(1, item);
		g1.SendItemSlot(1, 0, item);
		g1.SendItemContainerContent(1, item);
		g1.SendItemDropped(1, item, new NetVector2(1, 1), new NetVector2(0, 0), 0, 0f);
		g1.SendItemDestroyed(1);
		g1.SendItemPickedUp(1);

		Assert.Equal(6, source.Settles);
	}

	[Fact]
	public void TheHostNeverSettlesGuestCreations_ItIsItsOwnAuthority()
	{
		using var w = ItemSimWorld.Create();
		var source = new RecordingCreationSource();
		var host = w.Host.Services.GetRequiredService<IItemControl>();
		host.RegisterPendingCreationSource(source);

		host.SendItemDropped(42, new CharacterItemMsg { ItemId = "test_item" }, new NetVector2(1, 1), new NetVector2(0, 0), 0, 0f);

		Assert.Equal(0, source.Settles);
	}
}
