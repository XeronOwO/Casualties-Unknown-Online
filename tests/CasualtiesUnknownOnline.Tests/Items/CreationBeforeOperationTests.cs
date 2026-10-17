using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
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
	private static CharacterItemMsg Item(ulong instanceId = 0, float condition = 1f) =>
		new() { InstanceId = instanceId, ItemId = "test_item", Condition = condition };

	/// <summary>
	/// The carried-set statement is the creation claim for what a member carries: once
	/// this host has judged it, a use report naming an id it never judged is not a
	/// creation it may honour (the report would materialize any item at all), so the
	/// report is refused and answered instead of accepted-first.
	/// </summary>
	[Fact]
	public void AUseReportForAnUnjudgedItem_AfterTheCarriedSetWasStated_IsRefusedAndNothingIsMaterialized()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G1, Item(42));
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "the carried set must be judged before this case means anything");

		w.Use(w.G1, 900, Item(900, condition: 0.5f));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemUpdateState, verdict.RefusedKind);
		Assert.Null(w.KernelRow(900));
		// NOT the pickup/block-drop rollback: that one destroys the local copy, and this
		// copy is the only one there is — the refusal exists to have it stated, not deleted.
		Assert.Empty(w.Rejects(w.G1));
	}

	/// <summary>
	/// The window the heal exists for is unchanged: before this host has judged the
	/// member's carried set, a use report is the only claim the item has (the
	/// registration was swallowed), so it is still materialized accepted-first.
	/// </summary>
	[Fact]
	public void AUseReportForAnUnjudgedItem_BeforeTheCarriedSetWasStated_IsStillMaterialized()
	{
		using var w = ItemSimWorld.Create();

		w.Use(w.G1, 900, Item(900));
		w.Driver.Tick(33);

		Assert.NotNull(w.KernelRow(900));
		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// And the refusal carries its way back: the member re-states its carried set (the
	/// absolute report is what the refusal asks for), and the item lands on the host
	/// WITH the state the use produced — the two sides agree again in one round trip.
	/// The re-stated condition differs from the one the first statement carried (1f),
	/// so the value is the use's, not one typed twice. What this case injects by hand is
	/// the CAPTURE (the simulation has no body to enumerate); the link it pins is the
	/// one the runtime owns — the refusal makes the next statement due at once.
	/// </summary>
	[Fact]
	public void ARefusedUseReport_ReStatesTheCarriedSet_AndTheItemLandsWithTheUsedState()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G1, Item(42));
		w.Driver.Tick(33);
		Assert.False(w.RegistrationDue(w.G1), "no window is open before the refusal");

		w.Use(w.G1, 900, Item(900, condition: 0.5f));
		w.Driver.Tick(33);
		Assert.Single(w.CommandRejections(w.G1));
		Assert.True(w.RegistrationDue(w.G1), "the refusal makes the next carried-set report due now");

		w.CarriedSet(w.G1, Item(42), Item(900, condition: 0.5f));
		w.Driver.Tick(33);

		var row = Assert.NotNull(w.KernelRow(900));
		Assert.Equal(0.5f, row.Data.Condition);
	}

	/// <summary>
	/// The state-report path's tombstone answer, on BOTH halves: with the carried-set
	/// statement judged (the refusal that names the claim) and without one (the window
	/// where the heal is still the claim). A refused creation must never be resurrected
	/// by either, and the second case is the one a heal-first order would break.
	/// </summary>
	[Fact]
	public void AUseReportForARefusedCreation_StillAnswersItsOwnReason_WithAndWithoutAStatement()
	{
		using var w = ItemSimWorld.Create();

		w.Items.SendItemReject(w.G1.SteamId, 77, ItemRejectMsg.Reason.BlockAlreadyBroken);
		w.Driver.Tick(33);
		var seeded = w.Rejects(w.G1).Count;

		// (a) before any carried-set statement: the heal is the claim, but a refused
		// creation is already dead and the tombstone answers first.
		w.Use(w.G1, 77, Item(77));
		w.Driver.Tick(33);

		var beforeStatement = Assert.Single(w.Rejects(w.G1).Skip(seeded));
		Assert.Equal(ItemRejectMsg.Reason.BlockAlreadyBroken, beforeStatement.Rejection);
		Assert.Null(w.KernelRow(77));

		// (b) after the statement was judged: the same answer, not the carry refusal.
		w.CarriedSet(w.G1, Item(42));
		w.Driver.Tick(33);
		w.Use(w.G1, 77, Item(77));
		w.Driver.Tick(33);

		var afterStatement = Assert.Single(w.Rejects(w.G1).Skip(seeded + 1));
		Assert.Equal(77ul, afterStatement.ItemId);
		Assert.Equal(ItemRejectMsg.Reason.BlockAlreadyBroken, afterStatement.Rejection);
		var verdict = w.CommandRejections(w.G1).Last(); // the seeded tombstone's own frame comes first
		Assert.Equal(RejectionReason.BlockAlreadyBroken, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemUpdateState, verdict.RefusedKind);
		Assert.False(w.RegistrationDue(w.G1), "a tombstoned item is not settled by re-stating the set");
		Assert.Null(w.KernelRow(77));
	}

	/// <summary>
	/// And the statement may only hang contents off ITS OWN member's rows: a registration
	/// that names somebody else's item as a container must not create anything inside it.
	/// The parent-exists check the kernel's contained-spawn arm makes is not enough — this
	/// path calls the kernel directly, so the ownership test is the receiver's own record.
	/// </summary>
	[Fact]
	public void ACarriedStatement_MayNotCreateContentsInsideAnotherMembersItem()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G2, Item(80)); // G2's own carried container
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G2, 80));

		var theirs = Item(80);
		theirs.Contents.Add(Item(81));
		var mine = Item(40);
		mine.Contents.Add(theirs);
		w.CarriedSet(w.G1, mine);
		w.Driver.Tick(33);

		Assert.True(w.TransferredOf(w.G1, 40));
		Assert.Null(w.KernelRow(81));
		var owner = w.KernelRow(80);
		Assert.True(owner.HasValue);
		Assert.Equal(w.G2.SteamId, owner.Value.Location.Owner.Value);
	}

	/// <summary>
	/// The statement's contents are judged with it: a carried container's children are
	/// rows of their own, so a nested item the statement carries is KNOWN to the host
	/// and its use is not refused. Without this the refusal would trap every use of an
	/// item inside a carried container (a re-stated set would never name it).
	/// </summary>
	[Fact]
	public void ACarriedContainersContents_AreRegisteredWithTheStatement_SoTheirUseIsNotRefused()
	{
		using var w = ItemSimWorld.Create();

		var container = Item(50);
		container.Contents.Add(Item(51, condition: 1f));
		w.CarriedSet(w.G1, container);
		w.Driver.Tick(33);

		var nested = w.KernelRow(51);
		Assert.True(nested.HasValue, "the statement's contents are judged with it");
		Assert.Equal(ItemLocationKind.Contained, nested.Value.Location.Kind);
		Assert.Equal(50ul, nested.Value.Location.ParentItemId);
		Assert.Equal(w.G1.SteamId, nested.Value.Location.Owner.Value);

		w.Use(w.G1, 51, Item(51, condition: 0.5f));
		w.Driver.Tick(33);

		Assert.Empty(w.CommandRejections(w.G1));
		var updated = w.KernelRow(51);
		Assert.True(updated.HasValue);
		Assert.Equal(0.5f, updated.Value.Data.Condition);
	}

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
