using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Stage 2 shared shrapnel session tests. The old remote tweezers path was a
/// one-shot full-limb removal; Stage 2 replaces it with one host-owned shared
/// session per target limb, multiple operators, per-piece ownership, authoritative
/// piece state, and correct cancel/disconnect/partial semantics.
/// </summary>
public sealed class MedicalOperationShrapnelSessionTests
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
		Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 50f, MuscleHealth = 50f, Shrapnel = 3 },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f },
		],
	};

	private static IMedicalOperationControl Ops(TestNode node) =>
		node.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	[Fact]
	public void Guest_WireShrapnelUpdate_ReachesHostTransport()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var frames = new List<(ulong From, byte[] Frame)>();
		var states = new List<MedicalOperationStateMsg>();
		w.Host.Transport.MessageReceived += (from, data) => frames.Add((from, data));
		Ops(w.Host).StateReceived += states.Add;
		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });

		Assert.Contains(frames, f => f.From == Guest1Id && f.Frame.Length > 0 && (NetMsg)f.Frame[0] == NetMsg.MedicalOperationUpdate);
		Assert.NotEmpty(states);
		var state = Assert.Single(states);
		Assert.Equal(Guest1Id, state.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}

	[Fact]
	public void Host_DirectShrapnelUpdate_PublishesAuthoritativeState()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;
		Ops(w.Host).HandleUpdate(Guest1Id, new MedicalOperationUpdateMsg
		{
			OperationId = ack!.OperationId,
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});

		var state = Assert.Single(states);
		Assert.Equal(Guest1Id, state.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}

	[Fact]
	public void TwoGuests_JoinSameSharedShrapnelSession_ShareOneOperationId()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? first = null;
		MedicalOperationStartAckMsg? second = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => first = m;
		g2Ops.StartAckReceived += m => second = m;

		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(first);
		Assert.True(first!.Accepted);

		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(second);
		Assert.True(second!.Accepted);
		Assert.Equal(first.OperationId, second.OperationId);
	}

	[Fact]
	public void SamePiece_NonOwnerMove_IsRejectedWhileOwned()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => ack = m;
		g2Ops.StartAckReceived += m => secondAck = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(secondAck);
		Assert.True(secondAck!.Accepted);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});

		var afterFirst = states.LastOrDefault(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.NotNull(afterFirst);
		var firstOwner = afterFirst!.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId;
		Assert.Equal(Guest1Id, firstOwner);

		g2Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 99f,
			Y = -99f,
			Grabbed = true,
			OwnershipChange = true,
		});

		var afterSecond = states.LastOrDefault(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.NotNull(afterSecond);
		var piece = afterSecond!.ShrapnelPieces.Single(p => p.PieceIndex == 0);
		Assert.Equal(Guest1Id, piece.OwnerSteamId);
		Assert.True(Math.Abs(piece.X - 10f) < 0.001f);
	}

	[Fact]
	public void DifferentPieces_TwoOperators_CanMoveConcurrently()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => ack = m;
		g2Ops.StartAckReceived += m => secondAck = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);
		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(secondAck);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		g2Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 1, X = 40f, Y = -120f, Grabbed = true, OwnershipChange = true });

		var last = states.LastOrDefault(m => m.ShrapnelPieces.Count >= 2);
		Assert.NotNull(last);
		Assert.Equal(Guest1Id, last!.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
		Assert.Equal(Guest2Id, last.ShrapnelPieces.Single(p => p.PieceIndex == 1).OwnerSteamId);
	}

	[Fact]
	public void OperatorCancel_ReleasesHeldPiece_KeepsRemovedPieces()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationStartAckMsg? secondAck = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => ack = m;
		g2Ops.StartAckReceived += m => secondAck = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);
		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(secondAck);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		var held = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(Guest1Id, held.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);

		g1Ops.SendCancelRequest(ack.OperationId);
		var released = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(0UL, released.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}

	[Fact]
	public void StaleNonOwnershipMove_AfterRelease_DoesNotReacquirePiece()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});
		Assert.Equal(Guest1Id, states.Last().ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			Released = true,
			OwnershipChange = true,
		});
		Assert.Equal(0UL, states.Last().ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);

		// A stale unreliable held-move that was queued before the release must
		// not re-acquire the piece: only an explicit OwnershipChange grab may.
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 20f,
			Y = -90f,
			Grabbed = true,
			OwnershipChange = false,
		});

		var finalState = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(0UL, finalState.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}

	[Fact]
	public void AllPiecesRemoved_TerminatesWithCompleted()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		var hostEnds = new List<MedicalOperationEndCommittedMsg>();
		var g1Ops = Ops(w.Guest1);
		var hostOps = Ops(w.Host);
		g1Ops.StartAckReceived += m => ack = m;
		hostOps.EndCommittedReceived += hostEnds.Add;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		for (var i = 0; i < 3; i++)
		{
			g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
			{
				PieceIndex = i,
				X = 0f,
				Y = 50f,
				Grabbed = true,
				OwnershipChange = true,
			});
		}

		g1Ops.SendShrapnelEndRequest(ack.OperationId);
		var end = Assert.Single(hostEnds);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);

		var hostLimbs = characters.GetHostCharacterData()!.Limbs;
		Assert.Equal(0, hostLimbs.Single(l => l.Index == 1).Shrapnel);
	}

	[Fact]
	public void OperatorDisconnect_ReleasesOwnership_OtherOperatorKeepsSession()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);
		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;
		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		Assert.Equal(Guest1Id, states.Last().ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);

		((ISessionControl)w.Host.Session).RemoveGuestMember(Guest1Id);
		var after = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(0UL, after.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}

	[Fact]
	public void BreakGrasp_AppliesLimbDamageAndReleasesPiece()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;
		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, BreakGrasp = true });

		var state = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(0UL, state.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
		var limb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.True(limb.Pain > 0f);
		Assert.True(limb.BleedAmount > 0f);
	}

	[Fact]
	public void Tweezers_JoinDrainsOnlyOperatorsOwnItem()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var items = w.Host.Services.GetRequiredService<Runtime.Session.Items.IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var tweezers = new CharacterItemMsg { InstanceId = 42, ItemId = "tweezers", Condition = 1f, SlotIndex = 0 };
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true, tweezers));
		items.AdoptTransferredItem(Guest1Id, 42, tweezers);
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 42, 1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		var saved = characters.GetSavedCharacter(Guest1Id)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.99f) < 0.001f);
		var transferred = items.GetTransferredItems(Guest1Id).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.99f) < 0.001f);
	}

	[Fact]
	public void OperatorJoin_ReceivesOwnTweezersItemAfterInState()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var items = w.Host.Services.GetRequiredService<Runtime.Session.Items.IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var tweezers1 = new CharacterItemMsg { InstanceId = 42, ItemId = "tweezers", Condition = 1f, SlotIndex = 0 };
		var tweezers2 = new CharacterItemMsg { InstanceId = 43, ItemId = "tweezers", Condition = 1f, SlotIndex = 1 };
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true, tweezers1));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true, tweezers2));
		items.AdoptTransferredItem(Guest1Id, 42, tweezers1);
		items.AdoptTransferredItem(Guest2Id, 43, tweezers2);
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? first = null;
		MedicalOperationStartAckMsg? second = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		var g1States = new List<MedicalOperationStateMsg>();
		var g2States = new List<MedicalOperationStateMsg>();
		g1Ops.StartAckReceived += m => first = m;
		g2Ops.StartAckReceived += m => second = m;
		g1Ops.StateReceived += g1States.Add;
		g2Ops.StateReceived += g2States.Add;

		g1Ops.SendShrapnelStartRequest(HostId, 42, 1);
		Assert.NotNull(first);
		Assert.True(first!.Accepted);
		g2Ops.SendShrapnelStartRequest(HostId, 43, 1);
		Assert.NotNull(second);
		Assert.True(second!.Accepted);

		var g1State = g1States.Last(m => m.ShrapnelPieces.Count > 0);
		Assert.Equal(42UL, g1State.ItemInstanceId);
		Assert.NotNull(g1State.ItemAfter);
		Assert.True(Math.Abs(g1State.ItemAfter!.Condition - 0.99f) < 0.001f);

		var g2State = g2States.Last(m => m.ShrapnelPieces.Count > 0);
		Assert.Equal(43UL, g2State.ItemInstanceId);
		Assert.NotNull(g2State.ItemAfter);
		Assert.True(Math.Abs(g2State.ItemAfter!.Condition - 0.99f) < 0.001f);
	}

	[Fact]
	public void TwoOperators_RemoveDifferentPieces_CommitBothAndKeepRemaining()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? first = null;
		MedicalOperationStartAckMsg? second = null;
		var g1Ops = Ops(w.Guest1);
		var g2Ops = Ops(w.Guest2);
		g1Ops.StartAckReceived += m => first = m;
		g2Ops.StartAckReceived += m => second = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(first);
		g2Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(second);

		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(first!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 0f, Y = 50f, Grabbed = true, OwnershipChange = true });
		g2Ops.SendShrapnelUpdate(second!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 1, X = 0f, Y = 50f, Grabbed = true, OwnershipChange = true });

		var state = states.Last(m => m.ShrapnelPieces.Count >= 2);
		Assert.True(state.ShrapnelPieces.Single(p => p.PieceIndex == 0).Removed);
		Assert.True(state.ShrapnelPieces.Single(p => p.PieceIndex == 1).Removed);
		Assert.Equal(1, state.ShrapnelPieces.Count(p => !p.Removed));
		Assert.Equal(1, characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1).Shrapnel);
	}

	[Fact]
	public void ThirdParty_ReceivesAuthoritativeShrapnelState()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		characters.SaveCharacterData(Guest2Id, Snapshot(Guest2Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);
		MarkInWorld(w.Guest2);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var g2States = new List<MedicalOperationStateMsg>();
		Ops(w.Guest2).StateReceived += g2States.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 12f,
			Y = -80f,
			Grabbed = true,
			OwnershipChange = true,
		});

		var state = g2States.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		var piece = state.ShrapnelPieces.Single(p => p.PieceIndex == 0);
		Assert.Equal(Guest1Id, piece.OwnerSteamId);
		Assert.True(Math.Abs(piece.X - 12f) < 0.001f);
		Assert.True(Math.Abs(piece.Y - -80f) < 0.001f);
	}

	[Fact]
	public void ShrapnelSessionActive_BlocksInjectionForSameOperator()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var morphine = new CharacterItemMsg
		{
			InstanceId = 44,
			ItemId = "morphine",
			Condition = 1f,
			SlotIndex = 1,
			Liquids = [new LiquidStackMsg { LiquidId = "morphine", Amount = 100f }],
		};
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true, morphine));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		var g1Ops = Ops(w.Guest1);
		MedicalOperationStartAckMsg? shrapnelAck = null;
		MedicalOperationStartAckMsg? injectionAck = null;
		g1Ops.StartAckReceived += m =>
		{
			if (m.Kind == MedicalOperationKind.Shrapnel)
			{
				shrapnelAck = m;
			}
			else
			{
				injectionAck = m;
			}
		};

		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(shrapnelAck);
		Assert.True(shrapnelAck!.Accepted);

		g1Ops.SendStartRequest(HostId, 44, -1);
		Assert.NotNull(injectionAck);
		Assert.False(injectionAck!.Accepted);
		Assert.Equal("Operator already has an active operation.", injectionAck.RejectReason);
	}

	[Fact]
	public void PartialRemoval_KeepsRemainingPiecesInSession()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(Guest1Id, Snapshot(Guest1Id, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var g1Ops = Ops(w.Guest1);
		g1Ops.StartAckReceived += m => ack = m;
		g1Ops.SendShrapnelStartRequest(HostId, 0, 1);
		Assert.NotNull(ack);

		var ends = new List<MedicalOperationEndCommittedMsg>();
		Ops(w.Host).EndCommittedReceived += ends.Add;
		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 0f, Y = 50f, Grabbed = true, OwnershipChange = true });
		var state = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.True(state.ShrapnelPieces.Single(p => p.PieceIndex == 0).Removed);
		Assert.Equal(2, state.ShrapnelPieces.Count(p => !p.Removed));
		Assert.Empty(ends);

		var hostLimbs = characters.GetHostCharacterData()!.Limbs;
		Assert.Equal(2, hostLimbs.Single(l => l.Index == 1).Shrapnel);
	}

	[Fact]
	public void HostOperator_CanStartAndOperateShrapnelOnGuest()
	{
		var w = CreateThreeNode();
		var characters = w.Host.Services.GetRequiredService<Runtime.Session.CharacterData.ICharacterDataControl>();
		var guestSnapshot = Snapshot(Guest1Id, conscious: true);
		characters.SaveCharacterData(Guest1Id, guestSnapshot);
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		MarkInWorld(w.Host);
		MarkInWorld(w.Guest1);

		MedicalOperationStartAckMsg? ack = null;
		var hostOps = Ops(w.Host);
		hostOps.StartAckReceived += m => ack = m;
		hostOps.SendShrapnelStartRequest(Guest1Id, 0, 1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		var states = new List<MedicalOperationStateMsg>();
		hostOps.StateReceived += states.Add;
		hostOps.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		var state = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		Assert.Equal(HostId, state.ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
	}
}
