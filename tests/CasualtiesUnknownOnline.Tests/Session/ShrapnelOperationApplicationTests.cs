using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.ShrapnelSessionFixture;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Shrapnel session family: the applied operation surface (authoritative state publication, break-grasp damage, tweezers item drain, third-party view, injection blocking, host operator).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class ShrapnelOperationApplicationTests
{
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
