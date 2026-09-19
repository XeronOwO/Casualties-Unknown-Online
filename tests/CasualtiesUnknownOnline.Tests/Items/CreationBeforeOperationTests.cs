using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The JUDGING half of the creation-before-operation invariant: an operation on
/// an item whose creation this host has not judged is answered IN THE SAME FRAME
/// — never parked in a hold window, never guessed about. Two answers exist and
/// they must stay apart: a creation the host REFUSED leaves a tombstone, so a
/// later operation gets the reason the creation died, while an id with no judged
/// creation at all is the protocol violation the sender's half exists to
/// prevent (answered generically and logged loudly).
/// </summary>
[Trait("Category", "Integration")]
public class CreationBeforeOperationTests
{
	private static CharacterItemMsg Item() => new() { ItemId = "test_item", Condition = 1f };

	[Fact]
	public void AnOperationOnAnUnjudgedItem_IsRefusedInTheVeryFrame_NotHeld()
	{
		using var w = ItemSimWorld.Create();

		w.Pickup(w.G1, 42, Item());

		// The refusal does not wait for a frame, let alone the deleted 500 ms hold:
		// the answer is already on its way back when the report is handed over.
		var reject = Assert.Single(w.Rejects(w.G1));
		Assert.Equal(42ul, reject.ItemId);
		Assert.Equal(ItemRejectMsg.Reason.UnknownItem, reject.Rejection);
		w.Driver.Tick(33); // and it stays exactly one answer — nothing was parked to expire later
		Assert.Single(w.Rejects(w.G1));
		Assert.False(w.HostTable(42), "a refused operation must not register anything");
	}

	[Fact]
	public void TheLateCreationStillRegisters_TheUnjudgedRefusalIsNoTombstone()
	{
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.Single(w.Rejects(w.G1));

		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);

		Assert.True(w.HostTable(42), "a creation reported after the refusal still registers");
	}

	[Fact]
	public void AnOperationOnARefusedCreation_IsAnsweredWithThePreciseReason()
	{
		using var w = ItemSimWorld.Create();

		// The host refuses the creation itself (a break that lost first-writer-wins
		// destroys its drops): that refusal is the tombstone.
		w.Items.SendItemReject(w.G1.SteamId, 77, ItemRejectMsg.Reason.BlockAlreadyBroken);
		w.Driver.Tick(33);

		// A report for the same id that was already in flight when the refusal
		// landed must get THAT reason, at once — not a generic unknown-item answer.
		w.Pickup(w.G1, 77, Item());
		w.Driver.Tick(33);

		var rejects = w.Rejects(w.G1);
		Assert.Equal(2, rejects.Count);
		Assert.All(rejects, r => Assert.Equal(ItemRejectMsg.Reason.BlockAlreadyBroken, r.Rejection));
		Assert.All(rejects, r => Assert.Equal(77ul, r.ItemId));
		Assert.False(w.HostTable(77));
	}
}
