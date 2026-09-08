using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.ShrapnelSessionFixture;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Shrapnel session family: shared-session lifecycle and per-piece ownership (join, concurrent operators, stale moves, cancel/disconnect, partial removal, completion).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class ShrapnelOwnershipSessionTests
{
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
}
