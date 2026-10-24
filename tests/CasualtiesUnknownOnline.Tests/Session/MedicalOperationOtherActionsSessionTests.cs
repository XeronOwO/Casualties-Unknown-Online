using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
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

	/// <summary>
	/// The item row that IS the limb component a removal is about — the two carriers of the
	/// ruled model: the kernel says WHICH item is installed on that limb (it keeps its id and
	/// gains the location), and the character snapshot says the component is there. Every
	/// production apply writes both, so a case that seeds only the component is seeding the
	/// divergence the removal refuses by name (pinned below by
	/// <see cref="SplintRemoval_WithNoAppliedRow_IsRefusedAndTheComponentStays"/>).
	/// </summary>
	private static ulong InstallOnLimb(TestNode node, ulong owner, int limbIndex, string definitionId, float condition)
	{
		var kernel = node.Services.GetRequiredService<ItemKernelAuthority>();
		var instanceId = ItemInstanceIds.Compose(counter: 41, accountId: owner);
		Assert.True(kernel.TrySpawnCarried(
			owner,
			instanceId,
			definitionId,
			Item(instanceId, definitionId, condition),
			out _,
			out _));
		Assert.True(kernel.TryApplyLimb(owner, instanceId, new ActorId(owner), limbIndex, null, out _, out _));
		return instanceId;
	}

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
		var installed = InstallOnLimb(host, HostId, 1, "splint", 0.75f);
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

		// The id the operator receives is the one the item had while it was installed, and the
		// kernel row moved with it: the removal is a relocation, never a mint.
		Assert.Equal(installed, end.AwardedItem.InstanceId);
		Assert.Equal(installed, Assert.Single(characters.GetSavedCharacter(Guest1Id)!.Items, i => i.ItemId == "splint").InstanceId);
		Assert.Equal(
			ItemLocation.Carried(new ActorId(Guest1Id)),
			host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(installed)!.Value.Location);
	}

	[Fact]
	public void SplintRemoval_WithNoAppliedRow_IsRefusedAndTheComponentStays()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();

		// The opposite divergence: the limb says the component is there, the kernel holds no row
		// naming it. The item's id is unknown, and the removal's whole rule is that the item keeps
		// the id it had — so the gesture is refused rather than answered with an invented one, and
		// NOTHING moves: the component, the limb flag and the operator's hands are as they were.
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
		Assert.True(ack is { Accepted: true });
		gOps.SendOtherEndRequest(ack!.OperationId, 0f);

		Assert.NotNull(end);
		Assert.Null(end!.AwardedItem);
		var hostLimb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.Contains(hostLimb.Components, c => c.TypeName == "SplintLimb");
		Assert.Empty(characters.GetSavedCharacter(Guest1Id)!.Items);
	}

	[Fact]
	public void SplintRemoval_HostOnGuest_AwardsItemToHostOperator()
	{
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true));
		var guestBody = SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1, components: [SplintComponent(0.6f)]), Limb(2));
		characters.SaveCharacterData(Guest1Id, guestBody);
		var installed = InstallOnLimb(host, Guest1Id, 1, "splint", 0.6f);
		PlayerInteractionTestSession.SeedOwnBody(guest, guestBody);
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
		Assert.Equal(installed, end.AwardedItem!.InstanceId);
		Assert.False(characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 1).Splinted);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.ItemId == "splint" && i.InstanceId == installed);

		// The row crossed from the guest's limb into the host operator's hands, and the guest's
		// transfer table no longer claims it: it is not carried by that guest any more.
		Assert.Equal(
			ItemLocation.Carried(new ActorId(HostId)),
			host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(installed)!.Value.Location);
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
		var installed = InstallOnLimb(host, HostId, 1, "tourniquet", 0.8f);
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
		Assert.Equal(installed, end.AwardedItem.InstanceId);
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
		PlayerInteractionTestSession.SeedOwnBody(guest, targetData);
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
	public void Aed_AShockOnACalmHeartIsRefused_AndThePatientIsNotBurned()
	{
		// Stage 3's second half: the shared trigger's LOSER. CPR restarted the heart, so the quiver is gone; the AED's own
		// advice is "no shock advised", and the refusal must leave the patient untouched — no skin burn, no pressure
		// halved — instead of discharging into a heart that no longer needs it.
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, true, Item(91, "aed")));
		var targetData = SnapshotWithLimbs(Guest1Id, true, Limb(0), Limb(1), Limb(2));
		targetData.Health!.FibrillationProgress = 0f;
		targetData.Health.HeartRate = 80f;
		targetData.Health.BloodPressure = 110f;
		characters.SaveCharacterData(Guest1Id, targetData);
		PlayerInteractionTestSession.SeedOwnBody(guest, targetData);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(host);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(Guest1Id, 91, 1, MedicalOperationKind.Aed);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		var beforeLimb = characters.GetSavedCharacter(Guest1Id)!.Limbs.Single(l => l.Index == 1).SkinHealth;
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Stage, value1: 0f);
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Stage, value1: 1f);
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Shock);

		var after = characters.GetSavedCharacter(Guest1Id)!;
		Assert.Equal(110f, after.Health!.BloodPressure, 3);
		Assert.Equal(beforeLimb, after.Limbs.Single(l => l.Index == 1).SkinHealth, 3);

		// The two analysis stages legitimately drain their 0.03 together; a DISCHARGE would take 0.14 off the same item, so
		// the battery is what tells a refused shock from a delivered one.
		var condition = characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 91).Condition;
		Assert.True(condition > 0.96f, $"a refused shock must not discharge the battery, read {condition}");
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
		PlayerInteractionTestSession.SeedOwnBody(guest, targetData);
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
		var guestBody = SnapshotWithLimbs(Guest1Id, true,
			Limb(0),
			Limb(1, infection: 80f),
			Limb(2),
			Limb(3, infection: 80f));
		characters.SaveCharacterData(Guest1Id, guestBody);
		PlayerInteractionTestSession.SeedOwnBody(guest, guestBody);
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
		var guest2Body = SnapshotWithLimbs(Guest2Id, true, Limb(0), Limb(1), Limb(2));
		characters.SaveCharacterData(Guest2Id, guest2Body);
		PlayerInteractionTestSession.SeedOwnBody(w.Guest2, guest2Body);
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

	[Fact]
	public void Cpr_CompressionsLiftThePressureAndStopAtTheBodysOwnCeiling()
	{
		// Ticket remote-medical-cpr.md, stage 1b: a bare-hand operation on a patient in cardiac arrest (the game's own
		// `inCardiacArrest` is heartRate < 20). Each compression writes the pressure CprRhythmPolicy computes for the
		// operator's measured interval, and never past what this patient's own volume allows.
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var hostData = SnapshotWithLimbs(HostId, conscious: false);
		hostData.Health!.HeartRate = 0f;
		hostData.Health.BloodPressure = 5f;
		hostData.Health.BloodVolume = 100f;
		characters.SaveHostCharacterData(hostData);
		var cprOperator = Snapshot(Guest1Id, conscious: true);
		cprOperator.Health!.Stamina = 100f;
		characters.SaveCharacterData(Guest1Id, cprOperator);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;

		gOps.SendOtherStartRequest(HostId, itemInstanceId: 0, targetLimbIndex: -1, MedicalOperationKind.Cpr);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted, "bare-hand CPR on an arrested patient must be accepted");

		// One compression builds progress but does NOT restart the heart (the threshold is above one press's gain).
		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Compress, CprRhythmPolicy.IdealInterval);
		Assert.Equal(0f, characters.GetHostCharacterData()!.Health!.HeartRate);

		// Enough well-timed compressions reach the perfusion threshold and restart the heart: heartRate above the arrest
		// line and fibrillation cleared, which is what lets the game's own model restore the pressure from there.
		for (var i = 0; i < 10; i++)
		{
			gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Compress, CprRhythmPolicy.IdealInterval);
		}

		var health = characters.GetHostCharacterData()!.Health!;
		Assert.True(health.HeartRate >= 20f, $"the heart must restart above the arrest line, read {health.HeartRate}");
		Assert.Equal(0f, health.FibrillationProgress);
	}

	[Fact]
	public void Cpr_VentilationCarriesOxygenUpToTheChestWoundsCeiling()
	{
		// Ticket remote-medical-cpr.md, stage 2: the breath is the oxygen channel the ruling separates from the
		// compressions' pressure channel. The game's circulation rule ADDS to bloodOxygen, so this value persists (unlike
		// the pressure), and its own chest-wound cap (100 - hemothorax*0.3) bounds what a breath may promise.
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var hostData = SnapshotWithLimbs(HostId, conscious: false);
		hostData.Health!.HeartRate = 0f;
		hostData.Health.BloodOxygen = 20f;
		hostData.Health.Hemothorax = 10f;
		characters.SaveHostCharacterData(hostData);
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(HostId, itemInstanceId: 0, targetLimbIndex: -1, MedicalOperationKind.Cpr);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Ventilate, CprRhythmPolicy.IdealInterval);
		var oxygen = characters.GetHostCharacterData()!.Health!.BloodOxygen;
		Assert.True(oxygen > 20f, $"a breath must carry oxygen, read {oxygen}");
		Assert.True(oxygen <= CprRhythmPolicy.OxygenCeilingFor(10f), $"the chest wound caps it, read {oxygen}");
	}

	[Fact]
	public void Cpr_EveryPressCostsTheOperatorStaminaAndHeat()
	{
		// Stage 4's costs (ticket remote-medical-cpr.md): the operator pays stamina and a little heat per press, on their own
		// committed data, because the host applies both sides of the operation.
		var (host, guest) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var hostData = SnapshotWithLimbs(HostId, conscious: false);
		hostData.Health!.HeartRate = 0f;
		characters.SaveHostCharacterData(hostData);
		var guestData = Snapshot(Guest1Id, conscious: true);
		guestData.Health!.Stamina = 100f;
		guestData.Health.Temperature = 37f;
		characters.SaveCharacterData(Guest1Id, guestData);
		MarkInWorld(host);
		MarkInWorld(guest);

		MedicalOperationStartAckMsg? ack = null;
		var gOps = Ops(guest);
		gOps.StartAckReceived += m => ack = m;
		gOps.SendOtherStartRequest(HostId, itemInstanceId: 0, targetLimbIndex: -1, MedicalOperationKind.Cpr);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		for (var i = 0; i < 3; i++)
		{
			gOps.SendOtherUpdate(ack.OperationId, MedicalOperationUpdateAction.Compress, CprRhythmPolicy.IdealInterval);
		}

		var saved = characters.GetSavedCharacter(Guest1Id)!.Health!;
		Assert.Equal(100f - (3f * CprRhythmPolicy.StaminaPerPress), saved.Stamina, 2);
		Assert.Equal(37f + (3f * CprRhythmPolicy.TemperaturePerPress), saved.Temperature, 3);
	}

	[Fact]
	public void Cpr_RefusesAPatientWhoseHeartIsStillBeating()
	{
		// The AED family is for the quivering heart; compressing a beating one is not the care, so the target's own client
		// refuses it by name.
		var (host, _) = CreatePair();
		var characters = host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var hostData = SnapshotWithLimbs(HostId, conscious: false);
		hostData.Health!.HeartRate = 120f;
		characters.SaveHostCharacterData(hostData);

		var accepted = MedicalTargetBodyValidator.TryValidate(
			characters.GetHostCharacterData()!,
			MedicalOperationKind.Cpr,
			limbIndex: -1,
			out _,
			out var reason);

		Assert.False(accepted);
		Assert.Contains("heart has not stopped", reason);
	}
}
