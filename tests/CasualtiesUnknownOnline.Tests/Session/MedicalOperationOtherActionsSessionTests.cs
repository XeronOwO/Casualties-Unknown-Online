using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Stage 3 medical operation tests: the remaining native minigames/actions
/// (bandage, splint/tourniquet removal, dislocation, AED, manual defib,
/// amputation) all reuse the generic medical session envelope and host
/// reservation sets.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MedicalOperationOtherActionsSessionTests
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

	private static (TestNode Host, TestNode Guest) CreatePair()
	{
		var w = CreateThreeNode();
		return (w.Host, w.Guest1);
	}

	private static CharacterItemMsg Item(ulong instanceId, string itemId, float condition = 1f)
	{
		var item = new CharacterItemMsg
		{
			InstanceId = instanceId,
			ItemId = itemId,
			Condition = condition,
			SlotIndex = 0,
		};
		return item;
	}

	private static CharacterLimbMsg Limb(
		int index,
		float skin = 50f,
		float muscle = 50f,
		bool dislocated = false,
		bool dismembered = false,
		bool blocked = false,
		float infection = 0f,
		int distance = 0,
		IEnumerable<int>? connected = null,
		IEnumerable<ComponentStateMsg>? components = null)
	{
		return new CharacterLimbMsg
		{
			Index = index,
			SkinHealth = skin,
			MuscleHealth = muscle,
			Dislocated = dislocated,
			Dismembered = dismembered,
			BlockedBleeding = blocked,
			InfectionAmount = infection,
			DistanceToHeart = distance,
			ConnectedLimbIndices = connected is null ? [] : [.. connected],
			Components = components is null ? [] : [.. components],
		};
	}

	private static ComponentStateMsg SplintComponent(float condition = 0.75f, string itemId = "splint") => new()
	{
		TypeName = "SplintLimb",
		Fields =
		[
			new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = condition },
			new ComponentFieldMsg { Name = "conditionLossMinute", Kind = SaveableFieldKind.Float, FloatValue = 0.015f },
			new ComponentFieldMsg { Name = "item", Kind = SaveableFieldKind.String, StringValue = itemId },
		],
	};

	private static ComponentStateMsg TourniquetComponent(float condition = 0.8f) => new()
	{
		TypeName = "TourniquetScript",
		Fields =
		[
			new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = condition },
			new ComponentFieldMsg { Name = "timeApplied", Kind = SaveableFieldKind.Float, FloatValue = 0f },
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

	[Fact]
	public void Bandage_WrapsApplyProgressivelyAndConsumeItem()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true, Limb(0), Limb(1), Limb(2)));
		var bandage = Item(77, "bandage");
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true, bandage));
		items.AdoptTransferredItem(Guest1Id, 77, bandage);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var states = new List<MedicalOperationStateMsg>();
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.StateReceived += states.Add;
		gOps.EndCommittedReceived += ends.Add;

		gOps.SendOtherStartRequest(HostId, 77, 1, MedicalOperationKind.Bandage);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		for (var i = 0; i < 5; i++)
		{
			gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Wrap);
		}

		var state = states.Last(m => m.ActionProgress > 0f);
		Assert.True(state.ActionProgress > 0f);
		Assert.True(state.ItemAfter!.Condition < 1f);

		gOps.SendOtherEndRequest(ack.OperationId);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		var hostLimb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.True(hostLimb.SkinHealAmount > 0f);
		var savedGuest = characters.GetSavedCharacter(Guest1Id)!.Items.Single(i => i.InstanceId == 77);
		Assert.True(savedGuest.Condition < 1f);
	}

	[Fact]
	public void Bandage_CancelKeepsCommittedWraps()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true, Limb(0), Limb(1), Limb(2)));
		var bandage = Item(78, "bandage");
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true, bandage));
		items.AdoptTransferredItem(Guest1Id, 78, bandage);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.EndCommittedReceived += ends.Add;

		gOps.SendOtherStartRequest(HostId, 78, 1, MedicalOperationKind.Bandage);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Wrap);

		gOps.SendCancelRequest(ack.OperationId);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Cancelled, end.TerminalReason);
		Assert.True(end.ItemAfter!.Condition < 1f);
	}

	[Fact]
	public void SplintRemoval_GivesItemToOperatorAndClearsLimb()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true, Limb(0), Limb(1, components: [SplintComponent(0.75f)], connected: [2], distance: 2), Limb(2, distance: 4)));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true));
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationEndCommittedMsg? end = null;
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.EndCommittedReceived += m => end = m;

		gOps.SendOtherStartRequest(HostId, 0, 1, MedicalOperationKind.SplintRemoval);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		gOps.SendOtherEndRequest(ack.OperationId, 0f);
		Assert.NotNull(end);

		var hostLimb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.False(hostLimb.Splinted);
		Assert.DoesNotContain(hostLimb.Components, c => c.TypeName == "SplintLimb");
		Assert.NotNull(end!.AwardedItem);
		Assert.Equal("splint", end.AwardedItem!.ItemId);
		Assert.True(Math.Abs(end.AwardedItem.Condition - 0.75f) < 0.001f);
		Assert.Contains(characters.GetSavedCharacter(Guest1Id)!.Items, i => i.ItemId == "splint");
	}

	[Fact]
	public void SplintRemoval_HostOnGuest_AwardsItemToHostOperator()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true));
		characters.SaveCharacterData(Guest1Id, SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1, components: [SplintComponent(0.6f)]), Limb(2)));
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var hostOps = Ops(host);
		hostOps.StartAckReceived += m => ack = m;
		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.SplintRemoval);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		var ends = new List<MedicalOperationEndCommittedMsg>();
		hostOps.EndCommittedReceived += ends.Add;
		hostOps.SendOtherEndRequest(ack.OperationId, 0f);
		var end = Assert.Single(ends);
		Assert.NotNull(end.AwardedItem);
		Assert.False(characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 1).Splinted);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.ItemId == "splint");
	}

	[Fact]
	public void TourniquetRemoval_GivesItemAndClearsAffectedLimbs()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true,
			Limb(0),
			Limb(1, blocked: true, components: [TourniquetComponent(0.8f)], connected: [2], distance: 2),
			Limb(2, blocked: true, distance: 4, connected: [1])));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true));
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationEndCommittedMsg? end = null;
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.EndCommittedReceived += m => end = m;

		gOps.SendOtherStartRequest(HostId, 0, 1, MedicalOperationKind.TourniquetRemoval);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		gOps.SendOtherEndRequest(ack.OperationId, 0f);
		Assert.NotNull(end);

		var hostLimbs = characters.GetHostCharacterData()!.Limbs;
		Assert.False(hostLimbs.Single(l => l.Index == 1).BlockedBleeding);
		Assert.False(hostLimbs.Single(l => l.Index == 2).BlockedBleeding);
		Assert.DoesNotContain(hostLimbs.Single(l => l.Index == 1).Components, c => c.TypeName == "TourniquetScript");
		Assert.Equal("tourniquet", end!.AwardedItem!.ItemId);
	}

	[Fact]
	public void Dislocation_ExclusiveLimbLeaseThenSuccess()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true));
		characters.SaveCharacterData(Guest1Id, SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1, dislocated: true), Limb(2)));
		characters.SaveCharacterData(Guest2Id, SnapshotWithLimbs(Guest2Id, true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? hostAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var hostOps = Ops(w.Host);
		var g2Ops = Ops(w.Guest2);
		hostOps.StartAckReceived += m => hostAck = m;
		g2Ops.StartAckReceived += m => secondAck = m;

		hostOps.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);
		Assert.NotNull(hostAck);
		Assert.True(hostAck!.Accepted);
		g2Ops.SendOtherStartRequest(Guest1Id, 0, 1, MedicalOperationKind.Dislocation);
		Assert.NotNull(secondAck);
		Assert.False(secondAck!.Accepted);
		Assert.Equal("Target limb is already reserved.", secondAck.RejectReason);

		var states = new List<MedicalOperationStateMsg>();
		hostOps.StateReceived += states.Add;
		hostOps.SendOtherUpdate(hostAck.OperationId, MedicalOperationUpdateAction.Hit, flag1: true);
		Assert.NotEmpty(states);

		var ends = new List<MedicalOperationEndCommittedMsg>();
		hostOps.EndCommittedReceived += ends.Add;
		hostOps.SendOtherEndRequest(hostAck.OperationId, 1f);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		var limb = characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 1);
		Assert.False(limb.Dislocated);
		Assert.True(limb.Pain > 0f);
	}

	[Fact]
	public void Aed_StageBatteryDrainAndShockClearsFibrillation()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(90, "aed")));
		var targetData = SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1), Limb(2));
		targetData.Health!.FibrillationProgress = 50f;
		characters.SaveCharacterData(Guest1Id, targetData);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(host);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(Guest1Id, 90, 1, MedicalOperationKind.Aed);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		gOps.SendOtherUpdate(ack!.OperationId, MedicalOperationUpdateAction.Stage, value1: 0f);
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Stage, value1: 1f);
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Shock);

		var ends = new List<MedicalOperationEndCommittedMsg>();
		gOps.EndCommittedReceived += ends.Add;
		gOps.SendOtherEndRequest(ack.OperationId);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		var hostItem = characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 90);
		Assert.True(hostItem.Condition < 0.84f);
		Assert.True(Math.Abs(characters.GetSavedCharacter(Guest1Id)!.Health!.FibrillationProgress - 0f) < 0.001f);
	}

	[Fact]
	public void ManualDefib_ShockCommitsChargeAndResult()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(91, "manualdefibrillator")));
		var targetData = SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1), Limb(2));
		targetData.Health!.FibrillationProgress = 25f;
		characters.SaveCharacterData(Guest1Id, targetData);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(host);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(Guest1Id, 91, 1, MedicalOperationKind.ManualDefib);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		gOps.SendOtherUpdate(ack!.OperationId, MedicalOperationUpdateAction.Shock, value1: 50f);

		var ends = new List<MedicalOperationEndCommittedMsg>();
		gOps.EndCommittedReceived += ends.Add;
		gOps.SendOtherEndRequest(ack.OperationId);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		var hostItem = characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 91);
		Assert.True(hostItem.Condition < 1f);
		Assert.True(characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 1).Pain > 0f);
	}

	[Fact]
	public void Amputation_PartialCutKeepsLimbAndFinalCutDismembers()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(92, "machete")));
		characters.SaveCharacterData(Guest1Id, SnapshotWithLimbs(Guest1Id, true,
			Limb(0),
			Limb(1, infection: 80f),
			Limb(2),
			Limb(3, infection: 80f)));
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(host);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(Guest1Id, 92, 3, MedicalOperationKind.Amputation);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		gOps.SendOtherUpdate(ack!.OperationId, MedicalOperationUpdateAction.Cut, value1: 0.4f);
		Assert.False(characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 3).Dismembered);

		gOps.SendOtherEndRequest(ack.OperationId, 0.4f);
		var partialLimb = characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 3);
		Assert.False(partialLimb.Dismembered);
		Assert.True(partialLimb.SkinHealth < 50f);

		// A fresh session can now complete the amputation.
		MedicalOperationStartAckMsg? secondAck = null;
		gOps.StartAckReceived += m => secondAck = m;
		gOps.SendOtherStartRequest(Guest1Id, 92, 3, MedicalOperationKind.Amputation);
		Assert.NotNull(secondAck);
		Assert.True(secondAck!.Accepted);
		gOps.SendOtherUpdate(secondAck.OperationId, MedicalOperationUpdateAction.Cut, value1: 1f);
		var ends = new List<MedicalOperationEndCommittedMsg>();
		gOps.EndCommittedReceived += ends.Add;
		gOps.SendOtherEndRequest(secondAck.OperationId, 1f);
		Assert.Single(ends);
		Assert.True(characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 3).Dismembered);
	}

	[Fact]
	public void Guest1_BandageOnGuest2_HostRelaysStateAndEnd()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var items = w.Host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true, Item(94, "bandage")));
		characters.SaveCharacterData(Guest2Id, SnapshotWithLimbs(Guest2Id, true, Limb(0), Limb(1), Limb(2)));
		items.AdoptTransferredItem(Guest1Id, 94, Item(94, "bandage"));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		var g2States = new List<MedicalOperationStateMsg>();
		var g2Ends = new List<MedicalOperationEndCommittedMsg>();
		g1Ops.StartAckReceived += m => ack = m;
		Ops(w.Guest2).StateReceived += g2States.Add;
		Ops(w.Guest2).EndCommittedReceived += g2Ends.Add;

		g1Ops.SendOtherStartRequest(Guest2Id, 94, 1, MedicalOperationKind.Bandage);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		g1Ops.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Wrap);
		Assert.Contains(g2States, s => s.OperationId == ack.OperationId);

		g1Ops.SendOtherEndRequest(ack.OperationId);
		var end = Assert.Single(g2Ends);
		Assert.Equal(Guest2Id, end.TargetSteamId);
		Assert.Equal(MedicalOperationKind.Bandage, end.Kind);
		var guest2Limb = characters.GetSavedCharacter(Guest2Id)!.Limbs.Single(l => l.Index == 1);
		Assert.True(guest2Limb.SkinHealAmount > 0f);
	}

	[Fact]
	public void ThirdParty_ReceivesStage3StateAndTerminal()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, true, Limb(0), Limb(1), Limb(2)));
		var bandage = Item(93, "bandage");
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, true, bandage));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		var g2States = new List<MedicalOperationStateMsg>();
		var g2Ends = new List<MedicalOperationEndCommittedMsg>();
		g1Ops.StartAckReceived += m => ack = m;
		Ops(w.Guest2).StateReceived += g2States.Add;
		Ops(w.Guest2).EndCommittedReceived += g2Ends.Add;

		g1Ops.SendOtherStartRequest(HostId, 93, 1, MedicalOperationKind.Bandage);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		g1Ops.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Wrap);
		Assert.NotEmpty(g2States);

		g1Ops.SendOtherEndRequest(ack.OperationId);
		var end = Assert.Single(g2Ends);
		Assert.Equal(MedicalOperationKind.Bandage, end.Kind);
	}
}
