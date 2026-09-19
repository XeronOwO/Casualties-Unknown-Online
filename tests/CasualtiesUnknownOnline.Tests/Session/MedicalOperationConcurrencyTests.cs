using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The medical-operation concurrency contract: several operators work one victim,
/// and one limb carries several independent units of work at the same time.
/// <para>
/// A unit is (target, limb, operation kind) — plus, where the native minigame has
/// discrete pieces, the piece itself (the shrapnel family's per-piece ownership,
/// which the shared session already arbitrates). Different units on one limb
/// coexist; the outcome of a unit that resolves once does not: both operators may
/// start, the first completion settles the unit, and the other open operations on
/// that unit are answered precisely and stopped there instead of resolving it a
/// second time. The victim's own client still answers for its own body, so every
/// start here goes through the real host → target → host verdict path.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class MedicalOperationConcurrencyTests
{
	private const ulong HostId = 1001;
	private const ulong Guest1Id = 2001;
	private const ulong Guest2Id = 2002;
	private const ulong LobbyId = 9001;

	private sealed record World(TestNode Host, TestNode Guest1, TestNode Guest2);

	private static World CreateThreeNode()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(Guest1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		var g2Steam = new FakeSteamService(Guest2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var g1 = TestNode.Create(Guest1Id, network, g1Steam, clock, pumpFirstFrame: true);
		var g2 = TestNode.Create(Guest2Id, network, g2Steam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, Guest1Id, Guest2Id];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		host.Update();
		g1.Update();
		g2.Update();
		return new World(host, g1, g2);
	}

	/// <summary>
	/// The three-node world with Guest1 as the victim: the host's picture of the
	/// victim AND the victim's own body, which is the client that answers the
	/// target-body check. The two are seeded from the same snapshot so a refusal
	/// in these tests can only come from the concurrency rule under test.
	/// </summary>
	private static (World World, ICharacterDataControl Characters, CharacterDataMsg VictimBody) CreateVictimWorld(params CharacterLimbMsg[] limbs)
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, true));
		var victimBody = SnapshotWithLimbs(Guest1Id, true, limbs);
		characters.SaveCharacterData(Guest1Id, victimBody);
		PlayerInteractionTestSession.SeedOwnBody(w.Guest1, victimBody);
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);
		return (w, characters, victimBody);
	}

	private static CharacterItemMsg Item(ulong instanceId, string itemId, float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		Condition = condition,
		SlotIndex = 0,
	};

	private static CharacterLimbMsg Limb(
		int index,
		bool dislocated = false,
		bool dismembered = false,
		IEnumerable<ComponentStateMsg>? components = null,
		int distance = 0,
		IEnumerable<int>? connected = null) => new()
		{
			Index = index,
			SkinHealth = 50f,
			MuscleHealth = 50f,
			Dislocated = dislocated,
			Dismembered = dismembered,
			DistanceToHeart = distance,
			ConnectedLimbIndices = connected is null ? [] : [.. connected],
			Components = components is null ? [] : [.. components],
		};

	private static ComponentStateMsg SplintComponent(float condition = 0.75f) => new()
	{
		TypeName = "SplintLimb",
		Fields =
		[
			new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = condition },
			new ComponentFieldMsg { Name = "item", Kind = SaveableFieldKind.String, StringValue = "splint" },
		],
	};

	private static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = conscious,
			BrainHealth = conscious ? 80f : 5f,
		},
	};

	private static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, params CharacterLimbMsg[] limbs)
	{
		var data = Snapshot(owner, conscious);
		data.Limbs = [.. limbs];
		return data;
	}

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	private static IMedicalOperationControl Ops(TestNode node) =>
		node.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;

	private static CharacterLimbMsg VictimLimb(ICharacterDataControl characters, int index) =>
		characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == index);

	[Fact]
	public void SameLimb_DifferentKinds_BothRunAndBothEffectsApply()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1, dislocated: true), Limb(2));
		var items = w.Host.Services.GetRequiredService<IItemControl>();
		var bandage = Item(77, "bandage");
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, true, bandage));
		items.AdoptTransferredItem(Guest2Id, 77, bandage);

		MedicalOperationStartAckMsg? dislocationAck = null;
		MedicalOperationStartAckMsg? bandageAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => dislocationAck = m;
		secondOps.StartAckReceived += m => bandageAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);
		secondOps.SendOtherStartRequest(Guest1Id, 77, 1, MedicalOperationKind.Bandage);

		Assert.True(dislocationAck!.Accepted, dislocationAck.RejectReason);
		Assert.True(bandageAck!.Accepted, bandageAck.RejectReason);

		hostOps.SendOtherUpdate(dislocationAck.OperationId, MedicalOperationUpdateAction.Hit, flag1: true);
		secondOps.SendOtherUpdate(bandageAck.OperationId, MedicalOperationUpdateAction.Wrap);

		var limb = VictimLimb(characters, 1);
		Assert.True(limb.Pain > 0f, "the dislocation hit must land on the shared limb");
		Assert.True(limb.SkinHealAmount > 0f, "the bandage wrap must land on the same limb");
	}

	[Fact]
	public void SameUniqueUnit_BothMayStart_TheFirstCompletionStopsTheOther()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1, dislocated: true), Limb(2));

		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => firstAck = m;
		secondOps.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);
		secondOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);

		Assert.True(firstAck!.Accepted, firstAck.RejectReason);
		Assert.True(secondAck!.Accepted, secondAck.RejectReason);

		// Both operators work the same unit while it is open: each hit reaches the shared
		// limb and neither cancels the other.
		hostOps.SendOtherUpdate(firstAck.OperationId, MedicalOperationUpdateAction.Hit, flag1: true);
		var painAfterFirstHit = VictimLimb(characters, 1).Pain;
		secondOps.SendOtherUpdate(secondAck.OperationId, MedicalOperationUpdateAction.Hit, flag1: true);
		Assert.True(painAfterFirstHit > 0f, "the first operator's hit must land on the shared unit");
		Assert.True(VictimLimb(characters, 1).Pain > painAfterFirstHit, "the second operator's hit lands on the same unit");

		var secondEnds = new List<MedicalOperationEndCommittedMsg>();
		secondOps.EndCommittedReceived += secondEnds.Add;

		hostOps.SendOtherEndRequest(firstAck.OperationId, 1f);

		var stopped = Assert.Single(secondEnds.Where(e => e.OperationId == secondAck.OperationId));
		Assert.Equal(MedicalOperationTerminalReason.AlreadyHandled, stopped.TerminalReason);

		var limb = VictimLimb(characters, 1);
		Assert.False(limb.Dislocated, "the first completion must relocate the joint exactly once");
		Assert.False(
			stopped.TargetLimbs.Single(l => l.Index == 1).Dislocated,
			"every peer agrees on the limb's terminal state");

		secondOps.SendOtherEndRequest(secondAck.OperationId, 1f);
		Assert.Single(secondEnds.Where(e => e.OperationId == secondAck.OperationId));
	}

	[Fact]
	public void SameRepeatableEffect_EachCompletedOperationApplies()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1), Limb(2));
		var items = w.Host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(76, "bandage")));
		var secondBandage = Item(77, "bandage");
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, true, secondBandage));
		items.AdoptTransferredItem(Guest2Id, 77, secondBandage);

		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => firstAck = m;
		secondOps.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 76, 1, MedicalOperationKind.Bandage);
		secondOps.SendOtherStartRequest(Guest1Id, 77, 1, MedicalOperationKind.Bandage);

		Assert.True(firstAck!.Accepted, firstAck.RejectReason);
		Assert.True(secondAck!.Accepted, secondAck.RejectReason);

		hostOps.SendOtherUpdate(firstAck.OperationId, MedicalOperationUpdateAction.Wrap);
		var afterFirst = VictimLimb(characters, 1).SkinHealAmount;
		Assert.True(afterFirst > 0f, "the first operator's wrap must apply");

		secondOps.SendOtherUpdate(secondAck.OperationId, MedicalOperationUpdateAction.Wrap);
		Assert.True(
			VictimLimb(characters, 1).SkinHealAmount > afterFirst,
			"a repeatable effect applies per completed operation, so the second operator's wrap lands on top");
	}

	[Fact]
	public void SameLimb_SameItem_TheSecondOperatorCannotShareTheItem()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1), Limb(2));
		// The same instance id is deliberately seeded on both operators (the
		// reservation id is the conflict key), so this pins the ITEM claim rather
		// than the limb: two operators on one limb are allowed, one item is not.
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(42, "bandage")));
		var shared = Item(42, "bandage");
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, true, shared));
		w.Host.Services.GetRequiredService<IItemControl>().AdoptTransferredItem(Guest2Id, 42, shared);

		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => firstAck = m;
		secondOps.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 42, 1, MedicalOperationKind.Bandage);
		secondOps.SendOtherStartRequest(Guest1Id, 42, 1, MedicalOperationKind.Bandage);

		Assert.True(firstAck!.Accepted, firstAck.RejectReason);
		Assert.False(secondAck!.Accepted);
		Assert.Equal("Item is already reserved.", secondAck.RejectReason);
	}

	[Fact]
	public void SplintRemoval_TwoOperators_TheItemIsAwardedOnce()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1, components: [SplintComponent(0.75f)]), Limb(2));

		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => firstAck = m;
		secondOps.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.SplintRemoval);
		secondOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.SplintRemoval);

		Assert.True(firstAck!.Accepted, firstAck.RejectReason);
		Assert.True(secondAck!.Accepted, secondAck.RejectReason);

		var secondEnds = new List<MedicalOperationEndCommittedMsg>();
		secondOps.EndCommittedReceived += secondEnds.Add;

		hostOps.SendOtherEndRequest(firstAck.OperationId, 0f);

		var stopped = Assert.Single(secondEnds.Where(e => e.OperationId == secondAck.OperationId));
		Assert.Equal(MedicalOperationTerminalReason.AlreadyHandled, stopped.TerminalReason);
		Assert.Null(stopped.AwardedItem);

		var limb = VictimLimb(characters, 1);
		Assert.False(limb.Splinted);
		Assert.DoesNotContain(limb.Components, c => c.TypeName == "SplintLimb");
		Assert.Single(characters.GetHostCharacterData()!.Items, i => i.ItemId == "splint");
		Assert.DoesNotContain(characters.GetSavedCharacter(Guest2Id)!.Items, i => i.ItemId == "splint");
	}

	[Fact]
	public void OperatorDisconnect_TheOtherOperationOnTheUnitContinues()
	{
		var (w, characters, _) = CreateVictimWorld(Limb(0), Limb(1, dislocated: true), Limb(2));

		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var secondOps = Ops(w.Guest2);
		hostOps.StartAckReceived += m => firstAck = m;
		secondOps.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);
		secondOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);

		Assert.True(firstAck!.Accepted, firstAck.RejectReason);
		Assert.True(secondAck!.Accepted, secondAck.RejectReason);

		var hostEnds = new List<MedicalOperationEndCommittedMsg>();
		hostOps.EndCommittedReceived += hostEnds.Add;

		((ISessionControl)w.Host.Session).RemoveGuestMember(Guest2Id);

		hostOps.SendOtherEndRequest(firstAck.OperationId, 1f);
		var end = Assert.Single(hostEnds.Where(e => e.OperationId == firstAck.OperationId));
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		Assert.False(VictimLimb(characters, 1).Dislocated, "the operator that stayed finishes the unit");
	}

	[Fact]
	public void UnitRules_SettleOnlyTheUniqueKinds()
	{
		Assert.True(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Dislocation));
		Assert.True(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Amputation));
		Assert.True(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.SplintRemoval));
		Assert.True(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.TourniquetRemoval));
		Assert.False(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Bandage));
		Assert.False(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Injection));
		Assert.False(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Aed));
		Assert.False(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.ManualDefib));
		Assert.False(MedicalOperationUnitRules.ResolvesOnce(MedicalOperationKind.Shrapnel));

		foreach (var kind in Enum.GetValues(typeof(MedicalOperationKind)).Cast<MedicalOperationKind>())
		{
			if (MedicalOperationUnitRules.ResolvesOnce(kind))
			{
				Assert.NotEmpty(MedicalOperationUnitRules.HandledReason(kind));
			}
		}
	}
}
