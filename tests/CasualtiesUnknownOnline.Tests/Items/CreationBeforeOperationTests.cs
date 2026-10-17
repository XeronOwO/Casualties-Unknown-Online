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

	/// <summary>
	/// The container sync is the SAME creation channel the state report was, one wire kind over: a
	/// carried container's facts reach the kernel through it, and its missing-parent arm materializes
	/// whatever the report names. Once this host has judged the member's carried-set statement — which
	/// carries a carried container AND its contents recursively — an id the statement did not carry is
	/// not a creation it may honour, so the report is refused and answered instead of accepted-first.
	/// </summary>
	[Fact]
	public void AContainerSyncForAnUnjudgedParent_AfterTheCarriedSetWasStated_IsRefusedAndNothingIsMaterialized()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G1, Item(42));
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "the carried set must be judged before this case means anything");

		w.ContainerSync(w.G1, 900, Item(900));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		Assert.Null(w.KernelRow(900));
		// The same rule the state report obeys: the settlement is a re-stated set, never the
		// pickup/block-drop rollback that would destroy the only copy there is.
		Assert.Empty(w.Rejects(w.G1));
	}

	/// <summary>
	/// And the window the heal exists for is unchanged: before the statement is judged, the container
	/// report IS the claim (the registration was swallowed), so the parent is still materialized as the
	/// reporting member's own carried item.
	/// </summary>
	[Fact]
	public void AContainerSyncForAnUnjudgedParent_BeforeTheCarriedSetWasStated_IsStillMaterialized()
	{
		using var w = ItemSimWorld.Create();

		w.ContainerSync(w.G1, 900, Item(900));
		w.Driver.Tick(33);

		var row = Assert.NotNull(w.KernelRow(900));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
		Assert.Equal(w.G1.SteamId, row.Location.Owner.Value);
		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// The child arm of the same channel, and the refusal must apply NOTHING: the sync also destroys the
	/// descendants a report leaves out, so an applied-then-refused shape would take the known sibling down
	/// with the forged child.
	/// </summary>
	[Fact]
	public void AContainerSyncForAnUnjudgedChild_AfterTheCarriedSetWasStated_IsRefused()
	{
		using var w = ItemSimWorld.Create();

		var container = Item(50);
		container.Contents.Add(Item(51));
		w.CarriedSet(w.G1, container);
		w.Driver.Tick(33);
		Assert.True(w.KernelRow(51).HasValue, "the statement's contents are judged with it");

		var forged = Item(50);
		forged.Contents.Add(Item(52));
		w.ContainerSync(w.G1, 50, forged);
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		Assert.Null(w.KernelRow(52));
		var sibling = Assert.NotNull(w.KernelRow(51));
		Assert.Equal(ItemLocationKind.Contained, sibling.Location.Kind);
		Assert.Empty(w.Rejects(w.G1));
	}

	/// <summary>
	/// And a child this host never judged may only be created inside a row it records as THAT member's
	/// own — the rule that holds even in the heal window, where a member's carried-set statement has not
	/// been judged yet and its reports are still accepted-first. The kernel's contained-spawn arm checks
	/// that the named parent exists, never whose it is, and this path reaches the kernel directly (the
	/// admission seam only judges rows that already exist, so it can never see a creation).
	/// </summary>
	[Fact]
	public void AContainerSync_MayNotCreateAnUnjudgedChildInsideAnotherMembersContainer()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G2, Item(80));
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G2, 80), "the other member's container must be judged first");
		// G1 has stated nothing: this case is the heal window, where the report is the claim.

		w.ForgeContainerChildParent(w.G1, 40, "test_item", 81, "test_item", 80);
		w.Driver.Tick(33);

		Assert.Null(w.KernelRow(81));
		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		// The refusal applies NOTHING — not even the parent row the same report names.
		Assert.Null(w.KernelRow(40));
	}

	/// <summary>
	/// The container sync's way back is the same statement the state report's is — the refusal makes the
	/// next carried-set report due at once, and the re-stated set carries the container WITH the content it
	/// gained, so the child lands on the host inside the parent the member owns.
	/// </summary>
	[Fact]
	public void ARefusedContainerSync_ReStatesTheCarriedSet_AndTheContentsLand()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G1, Item(50));
		w.Driver.Tick(33);
		Assert.False(w.RegistrationDue(w.G1), "no window is open before the refusal");

		var changed = Item(50);
		changed.Contents.Add(Item(52, condition: 0.5f));
		w.ContainerSync(w.G1, 50, changed);
		w.Driver.Tick(33);
		Assert.Single(w.CommandRejections(w.G1));
		Assert.True(w.RegistrationDue(w.G1), "the refusal makes the next carried-set report due now");
		Assert.Null(w.KernelRow(52));

		w.CarriedSet(w.G1, changed);
		w.Driver.Tick(33);

		var landed = Assert.NotNull(w.KernelRow(52));
		Assert.Equal(ItemLocationKind.Contained, landed.Location.Kind);
		Assert.Equal(50ul, landed.Location.ParentItemId);
		Assert.Equal(w.G1.SteamId, landed.Location.Owner.Value);
		Assert.Equal(0.5f, landed.Data.Condition);
	}

	/// <summary>
	/// The heal window's nested shape must NOT be refused by the ownership arm: a report may create a
	/// parent and hang a child under THAT parent in the same report, so a named parent this host does not
	/// know yet is not a fact the arm can judge. Without this the swallowed-registration window would
	/// refuse every nested container report it exists for.
	/// </summary>
	[Fact]
	public void AContainerSync_BeforeTheCarriedSetWasStated_StillCreatesANestedTree()
	{
		using var w = ItemSimWorld.Create();

		var outer = Item(900);
		var inner = Item(901);
		inner.Contents.Add(Item(902));
		outer.Contents.Add(inner);
		w.ContainerSync(w.G1, 900, outer);
		w.Driver.Tick(33);

		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
		var nested = Assert.NotNull(w.KernelRow(902));
		Assert.Equal(ItemLocationKind.Contained, nested.Location.Kind);
		Assert.Equal(901ul, nested.Location.ParentItemId);
		Assert.Equal(w.G1.SteamId, nested.Location.Owner.Value);
	}

	/// <summary>
	/// The record arm on the parent, alone (no children listed, so no other arm can refuse this shape):
	/// the sync REWRITES the parent's own facts, so a parent that is not this member's row is not a
	/// report this host applies. A world container is the shape with no production sender — a load that
	/// lands in a world container reports a drop — and naming one used to rewrite it.
	/// </summary>
	[Fact]
	public void AContainerSyncMayNotRewriteAWorldContainer()
	{
		using var w = ItemSimWorld.Create();

		w.Spawn(w.G1, 70, Item(70));
		w.Driver.Tick(33);
		Assert.True(w.HostTable(70), "the world row must exist before this case means anything");

		w.ContainerSync(w.G1, 70, Item(70, condition: 0.5f));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		var parent = Assert.NotNull(w.KernelRow(70));
		Assert.Equal(ItemLocationKind.World, parent.Location.Kind);
		Assert.Equal(1f, parent.Data.Condition);
	}

	/// <summary>
	/// The same arm's DESTRUCTIVE half. This one needs the contents to be CONTAINED rows, and a world
	/// container's real contents are WORLD rows carrying a parent id (`ItemLocation.World(x, y,
	/// parentItemId)`) — the kernel's destroy walk only descends CONTAINED rows, so one report against a
	/// world container cannot reach them. The destructive shape is therefore reachable only in two
	/// reports (the first converting contents into contained rows, which these same arms now refuse), so
	/// this case states that converted record by hand and pins that the destroy walk cannot reach it
	/// through a parent the sender does not own: defence in depth, declared as staged.
	/// </summary>
	[Fact]
	public void AContainerSyncMayNotDestroyAWorldContainersContents()
	{
		using var w = ItemSimWorld.Create();

		w.Spawn(w.G1, 70, Item(70));
		w.Driver.Tick(33);
		Assert.True(w.HostTable(70), "the world container must exist before this case means anything");
		Assert.True(
			w.StageKernelRow(w.G1.SteamId, 71, "test_item", ItemLocation.Contained(new ActorId(w.G1.SteamId), 70), Item(71)),
			"the converted record this case is about is stated by hand, not produced by a report");

		w.ContainerSync(w.G1, 70, Item(70)); // no children listed: the stale-descendant arm would destroy 71
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		Assert.True(w.KernelRow(71).HasValue, "the world container's content must survive the report");
	}

	/// <summary>
	/// The kernel writes each listed child's destination verbatim (<c>ItemLocation.Contained(command.Actor,
	/// child.ParentItemId)</c>), so the destination has to be this member's own row: otherwise a report
	/// hangs its item inside another member's container.
	/// </summary>
	[Fact]
	public void AContainerSyncMayNotHangAChildUnderAnotherMembersRow()
	{
		using var w = ItemSimWorld.Create();

		w.CarriedSet(w.G2, Item(80));
		w.Driver.Tick(33);
		w.CarriedSet(w.G1, Item(40), Item(42));
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42));

		w.ForgeContainerChildParent(w.G1, 40, "test_item", 42, "test_item", 80);
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		var moved = Assert.NotNull(w.KernelRow(42));
		Assert.Equal(ItemLocationKind.Carried, moved.Location.Kind);
		Assert.Equal(w.G1.SteamId, moved.Location.Owner.Value);
	}

	/// <summary>
	/// And a child the record holds inside ANOTHER member's container is not this sender's to move,
	/// whatever destination the report names: the container-changed-hands allowance is about a child
	/// whose container is the SENDER's row (the picked-up container carries children whose recorded
	/// owner is still the previous one), never about a peer's backpack.
	/// </summary>
	[Fact]
	public void AContainerSyncMayNotMoveAnotherMembersContainedItem()
	{
		using var w = ItemSimWorld.Create();

		var theirs = Item(80);
		theirs.Contents.Add(Item(81));
		w.CarriedSet(w.G2, theirs);
		w.Driver.Tick(33);
		w.CarriedSet(w.G1, Item(40));
		w.Driver.Tick(33);

		w.ForgeContainerChildParent(w.G1, 40, "test_item", 81, "test_item", 40);
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(WireCommandKind.ItemContainerSync, verdict.RefusedKind);
		var stayed = Assert.NotNull(w.KernelRow(81));
		Assert.Equal(ItemLocationKind.Contained, stayed.Location.Kind);
		Assert.Equal(80ul, stayed.Location.ParentItemId);
		Assert.Equal(w.G2.SteamId, stayed.Location.Owner.Value);
	}

	/// <summary>
	/// And the heal window's NESTED shape must not be refused: a member's own nested container is
	/// recorded as CONTAINED with that member's owner (never as carried), and a later report hanging a
	/// new item under it is ordinary play in the window where the registration was swallowed.
	/// </summary>
	[Fact]
	public void AContainerSync_BeforeTheCarriedSetWasStated_StillAddsAChildToTheMembersOwnNestedContainer()
	{
		using var w = ItemSimWorld.Create();

		var outer = Item(900);
		var inner = Item(901);
		outer.Contents.Add(inner);
		w.ContainerSync(w.G1, 900, outer);
		w.Driver.Tick(33);
		var nested = Assert.NotNull(w.KernelRow(901));
		Assert.Equal(ItemLocationKind.Contained, nested.Location.Kind);

		var changed = Item(900);
		var grown = Item(901);
		grown.Contents.Add(Item(902));
		changed.Contents.Add(grown);
		w.ContainerSync(w.G1, 900, changed);
		w.Driver.Tick(33);

		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
		var landed = Assert.NotNull(w.KernelRow(902));
		Assert.Equal(901ul, landed.Location.ParentItemId);
	}

	/// <summary>
	/// The container-changed-hands shape the arms must NOT refuse: the container's row is the sender's
	/// own, while the item inside it still records the PREVIOUS holder — that is what the game's own
	/// drop + pickup leaves behind, and it is why the arms test the ROW's owner and never the child's.
	/// A future tightening to the child's own owner (the field that produced this cycle's blocker) would
	/// fail only here.
	/// </summary>
	[Fact]
	public void AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents()
	{
		using var w = ItemSimWorld.Create();

		// The record a drop + pickup produces, stated: the container is G1's row now, its child 81 still
		// carries G2 (the previous holder) in its own location.
		Assert.True(w.StageKernelRow(w.G1.SteamId, 80, "test_item", ItemLocation.Carried(new ActorId(w.G1.SteamId)), Item(80)));
		Assert.True(w.StageKernelRow(w.G2.SteamId, 81, "test_item", ItemLocation.Contained(new ActorId(w.G2.SteamId), 80), Item(81)));
		w.CarriedSet(w.G1, Item(40));
		w.Driver.Tick(33);

		var container = Item(80);
		container.Contents.Add(Item(81));
		w.ContainerSync(w.G1, 80, container);
		w.Driver.Tick(33);

		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
		var child = Assert.NotNull(w.KernelRow(81));
		Assert.Equal(80ul, child.Location.ParentItemId);
		Assert.Equal(w.G2.SteamId, child.Location.Owner.Value);
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
