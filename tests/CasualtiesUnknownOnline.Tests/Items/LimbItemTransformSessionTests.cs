using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The two limb-item reports a CLIENT sends about its own body, through the production
/// composition root: the local limb action installing a carried item as the limb's
/// component, and that component coming off as an item again. Both are RELOCATIONS that
/// keep the instance id, and a guest's report is judged by the host like any other member
/// command — the seam that decides whether a member may submit it is
/// <c>KernelCommandGateway</c>, and this file is the end-to-end half of that rule.
/// </summary>
[Trait("Category", "Integration")]
public class LimbItemTransformSessionTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	[Fact]
	public void TheHostInstallingItsOwnItemOnItsLimb_RelocatesTheRowWithoutMintingAnId()
	{
		using var w = ItemSimWorld.Create();
		var item = Carried(w, HostId, 41, "splint");

		w.Host.Services.GetRequiredService<IItemControl>().SendItemAppliedToLimb(item, 3);
		w.Driver.Tick(20);

		Assert.Equal(ItemLocation.Applied(new ActorId(HostId), 3), Row(w, item).Location);

		// ...and back off again, the way the component's own TakeOff reports it.
		w.Host.Services.GetRequiredService<IItemControl>().SendItemRemovedFromLimb(item, 3);
		w.Driver.Tick(20);

		Assert.Equal(ItemLocation.Carried(new ActorId(HostId)), Row(w, item).Location);
	}

	[Fact]
	public void AGuestsReportOfItsOwnLimbInstall_ReachesTheHostsKernelAndKeepsTheId()
	{
		using var w = ItemSimWorld.Create();
		var item = Carried(w, GuestId, 42, "splint");

		// The guest applies it to its own limb: the wire command is the report, and the HOST
		// judges it (actor binding, the sender's own carried item, the sender's own limb).
		w.G1.Services.GetRequiredService<IItemControl>().SendItemAppliedToLimb(item, 2);
		w.Driver.Tick(50);

		var hostRow = Row(w, item);
		Assert.Equal(ItemLocation.Applied(new ActorId(GuestId), 2), hostRow.Location);
		Assert.Equal("splint", hostRow.Identity.DefinitionId);

		// Then it comes off, into the same body's hands — the same id again.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemRemovedFromLimb(item, 2);
		w.Driver.Tick(50);

		Assert.Equal(ItemLocation.Carried(new ActorId(GuestId)), Row(w, item).Location);
		Assert.Equal("splint", Row(w, item).Identity.DefinitionId);
	}

	[Fact]
	public void AGuestsReportAboutSomebodyElsesItem_IsIgnoredAndTheRowStaysWhereItWas()
	{
		// The eligibility rule's negative half, end to end: the report names the HOST's carried
		// item, and a member may not install another member's item on anything. The row is
		// untouched, and nobody is answered for it (the destroy report's own precedent).
		using var w = ItemSimWorld.Create();
		var hostsItem = Carried(w, HostId, 44, "splint");

		w.G1.Services.GetRequiredService<IItemControl>().SendItemAppliedToLimb(hostsItem, 2);
		w.Driver.Tick(50);

		Assert.Equal(ItemLocation.Carried(new ActorId(HostId)), Row(w, hostsItem).Location);
	}

	[Fact]
	public void TheHostsOwnRemoval_CarriesTheRevivedItemsSlotAndCondition()
	{
		// The relocation's data is the REVIVED object's, not the applied row's: the row's slot is
		// where the item was before the apply and its condition is the last component report's, so
		// a removal that passes nothing leaves a mod-visible row with a stale placement (batch
		// 20261018-v's review). The seam captures the object after the game's own AutoPickUpItem
		// put it where the player will find it.
		using var w = ItemSimWorld.Create();
		var item = Carried(w, HostId, 43, "splint");
		var items = w.Host.Services.GetRequiredService<IItemControl>();
		items.SendItemAppliedToLimb(item, 3);
		w.Driver.Tick(20);

		items.SendItemRemovedFromLimb(item, 3, new CharacterItemMsg
		{
			InstanceId = item,
			ItemId = "splint",
			Condition = 0.4f,
			SlotIndex = 4,
		});
		w.Driver.Tick(20);

		var row = Row(w, item);
		Assert.Equal(ItemLocation.Carried(new ActorId(HostId)), row.Location);
		Assert.Equal(4, row.Data.SlotIndex);
		Assert.Equal(0.4f, row.Data.Condition, 3);
	}

	[Fact]
	public void AGuestsRemovalReport_CarriesTheRevivedItemsSlotAndConditionToTheHost()
	{
		// The same fact over the member's own wire: the command's payload carries the object's
		// state, and the host's kernel row is where the imprecision used to land.
		using var w = ItemSimWorld.Create();
		var item = Carried(w, GuestId, 45, "splint");
		var seam = w.G1.Services.GetRequiredService<IItemControl>();
		seam.SendItemAppliedToLimb(item, 2);
		w.Driver.Tick(50);

		seam.SendItemRemovedFromLimb(item, 2, new CharacterItemMsg
		{
			InstanceId = item,
			ItemId = "splint",
			Condition = 0.35f,
			SlotIndex = 6,
		});
		w.Driver.Tick(50);

		var row = Row(w, item);
		Assert.Equal(ItemLocation.Carried(new ActorId(GuestId)), row.Location);
		Assert.Equal(6, row.Data.SlotIndex);
		Assert.Equal(0.35f, row.Data.Condition, 3);
	}

	/// <summary>One carried item of that owner, in the kernel and in the owner's own snapshot, so the report has a row to relocate.</summary>
	private static ulong Carried(ItemSimWorld world, ulong owner, ulong instanceId, string definitionId)
	{
		var item = new CharacterItemMsg { InstanceId = instanceId, ItemId = definitionId, Condition = 0.75f, SlotIndex = 0 };
		if (owner == HostId)
		{
			Assert.True(Kernel(world).TrySpawnCarried(owner, instanceId, definitionId, item, out _, out _));
		}
		else
		{
			world.Host.Services.GetRequiredService<IItemControl>().AdoptTransferredItem(owner, instanceId, item);
		}

		world.Driver.Tick(20);
		return instanceId;
	}

	private static ItemState Row(ItemSimWorld world, ulong instanceId)
	{
		var row = Kernel(world).FindItem(instanceId);
		Assert.NotNull(row);
		return row!.Value;
	}

	private static ItemKernelAuthority Kernel(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<ItemKernelAuthority>();
}
