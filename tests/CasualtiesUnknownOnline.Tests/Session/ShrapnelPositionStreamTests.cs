using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.ShrapnelSessionFixture;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Shrapnel session family: the per-piece position stream (wire delivery, ordinary-position coalescing, release/end/cancel flushes).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class ShrapnelPositionStreamTests
{
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
	public void Guest_OrdinaryShrapnelPositions_CoalesceLatestAfterInterval()
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

		var updates = new List<MedicalOperationUpdateMsg>();
		w.Host.Transport.MessageReceived += (_, data) =>
		{
			if (data.Length > 0 && (NetMsg)data[0] == NetMsg.MedicalOperationUpdate)
			{
				updates.Add(NetPacket.DecodePayload<MedicalOperationUpdateMsg>(data));
			}
		};

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});
		Assert.Single(updates);

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 11f,
			Y = -99f,
			Grabbed = true,
		});
		Assert.Equal(2, updates.Count);

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 20f,
			Y = -80f,
			Grabbed = true,
		});
		Assert.Equal(2, updates.Count);

		w.Guest1.Clock.Advance(50);
		w.Guest1.Update();

		Assert.Equal(3, updates.Count);
		Assert.True(Math.Abs(updates[2].X - 20f) < 0.001f);
		Assert.True(Math.Abs(updates[2].Y - -80f) < 0.001f);
	}

	[Fact]
	public void Guest_Release_PreservesLastAuthoritativePosition()
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
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 20f,
			Y = -80f,
			Grabbed = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 24f,
			Y = -76f,
			Grabbed = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			Released = true,
			OwnershipChange = true,
		});

		var final = states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0));
		var piece = final.ShrapnelPieces.Single(p => p.PieceIndex == 0);
		Assert.Equal(0UL, piece.OwnerSteamId);
		Assert.True(Math.Abs(piece.X - 24f) < 0.001f);
		Assert.True(Math.Abs(piece.Y - -76f) < 0.001f);
	}

	[Fact]
	public void Guest_End_FlushesBufferedPositionBeforeTerminal()
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

		var updates = new List<MedicalOperationUpdateMsg>();
		w.Host.Transport.MessageReceived += (_, data) =>
		{
			if (data.Length > 0 && (NetMsg)data[0] == NetMsg.MedicalOperationUpdate)
			{
				updates.Add(NetPacket.DecodePayload<MedicalOperationUpdateMsg>(data));
			}
		};

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 15f,
			Y = -90f,
			Grabbed = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 25f,
			Y = -70f,
			Grabbed = true,
		});
		Assert.Equal(2, updates.Count);

		g1Ops.SendShrapnelEndRequest(ack.OperationId);

		Assert.Equal(3, updates.Count);
		Assert.True(Math.Abs(updates[2].X - 25f) < 0.001f);
		Assert.True(Math.Abs(updates[2].Y - -70f) < 0.001f);
	}

	[Fact]
	public void Guest_Cancel_FlushesBufferedPositionBeforeCancel()
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

		var updates = new List<MedicalOperationUpdateMsg>();
		w.Host.Transport.MessageReceived += (_, data) =>
		{
			if (data.Length > 0 && (NetMsg)data[0] == NetMsg.MedicalOperationUpdate)
			{
				updates.Add(NetPacket.DecodePayload<MedicalOperationUpdateMsg>(data));
			}
		};

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 10f,
			Y = -100f,
			Grabbed = true,
			OwnershipChange = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 18f,
			Y = -85f,
			Grabbed = true,
		});
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate
		{
			PieceIndex = 0,
			X = 30f,
			Y = -60f,
			Grabbed = true,
		});
		Assert.Equal(2, updates.Count);

		g1Ops.SendCancelRequest(ack.OperationId);

		Assert.Equal(3, updates.Count);
		Assert.True(Math.Abs(updates[2].X - 30f) < 0.001f);
		Assert.True(Math.Abs(updates[2].Y - -60f) < 0.001f);
	}
}
