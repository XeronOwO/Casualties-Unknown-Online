using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The give claim's DATA judgement (decision 265): the real <see cref="TradeGiveJudge"/> over real
/// <see cref="ItemState"/> rows, with the game tables replaced by a fixture (the tests cannot reference
/// them — only the Game Adapter may). What is pinned here is the authority half: whose item the reporter
/// may speak for, which rows are refused by name, that a claim above the host's own number is CLAMPED and
/// answered rather than refused (the row can be a frame behind the giver's live item), and that one
/// instance credits once. The NUMBER itself is <see cref="TradeStockMachine.GiveItemValue"/>, pinned in
/// <see cref="TradeStockMachineTests"/>.
/// </summary>
public class TradeGiveJudgeTests
{
	private const ulong Guest = 2001;
	private const ulong Other = 4242;
	private const ulong TraderItem = 7001;

	private static ItemState Row(ulong instanceId, ItemLocation location, string definition = "bandage", float condition = 1f) =>
		new(new ItemIdentity(instanceId, definition), 1, location)
		{
			Data = new ItemData(condition, false, -1, [], []),
		};

	private static (TradeGiveJudge Judge, GiveItemVerdict Verdict) JudgeGive(
		ItemState row,
		ulong sender = Guest,
		ulong itemInstanceId = TraderItem,
		int reportedValue = 20,
		int hostValue = 20)
	{
		var rows = new Dictionary<ulong, ItemState> { [row.Identity.InstanceId] = row };
		var judge = new TradeGiveJudge();
		var verdict = judge.Judge(sender, itemInstanceId, reportedValue, id => rows.TryGetValue(id, out var found) ? found : null, _ => new GiveItemValue(hostValue, null));
		return (judge, verdict);
	}

	[Fact]
	public void ClaimInsideTheHostsOwnValue_IsCreditedAsItStands()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Guest))), reportedValue: 15, hostValue: 20);

		Assert.Null(verdict.Refusal);
		Assert.Equal(15, verdict.Credit);
		Assert.False(verdict.ClaimExceeded);
	}

	[Fact]
	public void ClaimAboveTheHostsOwnValue_IsClampedAndAnswered()
	{
		// The host's row can be a frame behind the giver's live item (condition decay, latency), so the
		// answer is a CLAMP that both sides converge on — never a refusal that would destroy the item.
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Guest))), reportedValue: 60, hostValue: 20);

		Assert.Null(verdict.Refusal);
		Assert.Equal(20, verdict.Credit);
		Assert.True(verdict.ClaimExceeded);
		Assert.Contains("worth 20", verdict.Detail, StringComparison.Ordinal);
		Assert.Contains("claimed 60", verdict.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void NoInstanceNamed_IsRefused()
	{
		var judge = new TradeGiveJudge();

		var verdict = judge.Judge(Guest, itemInstanceId: 0, reportedValue: 60, _ => null, _ => new GiveItemValue(20, null));

		Assert.NotNull(verdict.Refusal);
		Assert.Equal(0, verdict.Credit);
	}

	[Fact]
	public void UnjudgedInstance_IsRefused()
	{
		var judge = new TradeGiveJudge();

		var verdict = judge.Judge(Guest, 999, reportedValue: 20, _ => null, _ => new GiveItemValue(20, null));

		Assert.NotNull(verdict.Refusal);
		Assert.Contains("never judged", verdict.Refusal, StringComparison.Ordinal);
		Assert.Equal(0, verdict.Credit);
	}

	[Fact]
	public void AnotherMembersCarriedItem_IsRefused()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Other))));

		Assert.NotNull(verdict.Refusal);
		Assert.Contains("neither the reporter's own nor world content", verdict.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void AnotherMembersContainedItem_IsRefused()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Contained(new ActorId(Other), 55)));

		Assert.NotNull(verdict.Refusal);
	}

	/// <summary>
	/// The row's OWN owner field is not the holder fact for a contained item: the kernel stamps it at
	/// creation and does not re-stamp it when the container changes hands (or when a generation-time
	/// container is picked up), so a child of the sender's carried bag can still name an older owner. The
	/// CHAIN is what says who holds the tree — the same reason a report about such a child must not be
	/// refused.
	/// </summary>
	[Fact]
	public void ContainedInTheSendersCarriedBag_WithAStaleChildOwner_IsAccepted()
	{
		var bag = Row(800, ItemLocation.Carried(new ActorId(Guest)), "trashbag");
		var child = Row(TraderItem, ItemLocation.Contained(new ActorId(Other), 800));
		var rows = new Dictionary<ulong, ItemState> { [bag.Identity.InstanceId] = bag, [child.Identity.InstanceId] = child };
		var judge = new TradeGiveJudge();

		var verdict = judge.Judge(Guest, TraderItem, reportedValue: 20, id => rows.TryGetValue(id, out var found) ? found : null, _ => new GiveItemValue(20, null));

		Assert.Null(verdict.Refusal);
		Assert.Equal(20, verdict.Credit);
	}

	[Fact]
	public void ResetSessionState_ClearsTheReceipts()
	{
		var judge = new TradeGiveJudge();
		var row = Row(TraderItem, ItemLocation.Carried(new ActorId(Guest)));

		judge.RecordGiven(TraderItem);
		Assert.NotNull(judge.Judge(Guest, TraderItem, reportedValue: 20, _ => row, _ => new GiveItemValue(20, null)).Refusal);

		judge.ResetSessionState();
		Assert.Null(judge.Judge(Guest, TraderItem, reportedValue: 20, _ => row, _ => new GiveItemValue(20, null)).Refusal);
	}

	[Fact]
	public void TerminalRow_IsRefused()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Terminal()));

		Assert.NotNull(verdict.Refusal);
		Assert.Contains("already gone", verdict.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void InstalledLimbItem_IsRefused()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Applied(new ActorId(Guest), 2)));

		Assert.NotNull(verdict.Refusal);
		Assert.Contains("installed on a limb", verdict.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void WorldContent_IsAccepted()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.World(10f, 20f)), reportedValue: 20, hostValue: 20);

		Assert.Null(verdict.Refusal);
		Assert.Equal(20, verdict.Credit);
	}

	[Fact]
	public void ContainedUnderAWorldContainer_IsWorldContent_AndAccepted()
	{
		// The native gesture drags an item out of a container that lies on the ground; its row names the
		// member that reported the container, so ownership alone would refuse a legitimate hand-in.
		var parent = Row(900, ItemLocation.World(10f, 20f), "trashbag");
		var child = Row(TraderItem, ItemLocation.Contained(new ActorId(Other), 900));
		var rows = new Dictionary<ulong, ItemState> { [parent.Identity.InstanceId] = parent, [child.Identity.InstanceId] = child };
		var judge = new TradeGiveJudge();

		var verdict = judge.Judge(Guest, TraderItem, reportedValue: 20, id => rows.TryGetValue(id, out var found) ? found : null, _ => new GiveItemValue(20, null));

		Assert.Null(verdict.Refusal);
		Assert.Equal(20, verdict.Credit);
	}

	[Fact]
	public void SecondGiveOfTheSameInstance_IsRefused()
	{
		var (judge, first) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Guest))));
		Assert.Null(first.Refusal);
		judge.RecordGiven(TraderItem);

		var second = judge.Judge(Guest, TraderItem, reportedValue: 20, _ => Row(TraderItem, ItemLocation.Carried(new ActorId(Guest))), _ => new GiveItemValue(20, null));

		Assert.NotNull(second.Refusal);
		Assert.Contains("already given", second.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void OnlyACreditedGiveIsRecorded_SoARefusedOneStaysGiveable()
	{
		var (judge, refused) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Other))));
		Assert.NotNull(refused.Refusal);

		var again = judge.Judge(Guest, TraderItem, reportedValue: 20, _ => Row(TraderItem, ItemLocation.Carried(new ActorId(Guest))), _ => new GiveItemValue(20, null));

		Assert.Null(again.Refusal);
	}

	[Fact]
	public void AValueTheHostsTablesCannotCompute_IsRefusedByName()
	{
		var judge = new TradeGiveJudge();
		var row = Row(TraderItem, ItemLocation.Carried(new ActorId(Guest)));

		var verdict = judge.Judge(Guest, TraderItem, reportedValue: 20, _ => row, _ => new GiveItemValue(0, "liquid 'unobtainium' is not in this host's table"));

		Assert.NotNull(verdict.Refusal);
		Assert.Contains("unobtainium", verdict.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void ZeroWorthItem_CreditsNothing()
	{
		var (_, verdict) = JudgeGive(Row(TraderItem, ItemLocation.Carried(new ActorId(Guest)), condition: 0f), reportedValue: 20, hostValue: 0);

		Assert.Null(verdict.Refusal);
		Assert.Equal(0, verdict.Credit);
	}
}
