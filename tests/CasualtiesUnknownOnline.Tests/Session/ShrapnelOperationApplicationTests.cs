using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
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
	public void ShrapnelStateAndTerminal_CarryTheShrapnelKind()
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
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var hostOps = Ops(w.Host);
		hostOps.StateReceived += states.Add;
		hostOps.EndCommittedReceived += ends.Add;

		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });
		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, BreakGrasp = true, GripSlipNonce = 1 });
		g1Ops.SendShrapnelEndRequest(ack.OperationId);

		// The receiving client ROUTES on this field: a state that does not name Shrapnel takes the
		// injection branch, and a shrapnel state carries no AppliedDose, so the authority's own limb
		// write is dropped in silence — the target's own body keeps the values its next 1 Hz report
		// carries straight back over the authority's copy. Every message of the session must name its
		// kind, not just the terminal (which always did, and is why the loss is invisible until then).
		var shrapnelStates = states.Where(m => m.ShrapnelPieces.Count > 0).ToList();
		Assert.NotEmpty(shrapnelStates);
		Assert.All(shrapnelStates, m => Assert.Equal(MedicalOperationKind.Shrapnel, m.Kind));
		Assert.Equal(MedicalOperationKind.Shrapnel, Assert.Single(ends).Kind);
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
	public void BreakGrasp_WritesTheDerivedValuesOntoTheAuthoritativeLimb()
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
		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });

		var before = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		var skinBefore = before.SkinHealth;
		var bleedBefore = before.BleedAmount;
		var painBefore = before.Pain;

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, BreakGrasp = true, GripSlipNonce = 1 });

		// The authority writes the values its OWN derived draw produced for this slip — the same state
		// the operator's screen was put on — and not a fresh roll of its own.
		var expected = FakeShrapnelGripSlipRandom.Script(ShrapnelGripSlipSeed.Of(ack.OperationId, 0, 1), isHead: false);
		var limb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.Equal(skinBefore - expected.Skin, limb.SkinHealth, 3);
		Assert.Equal(bleedBefore + expected.Bleed, limb.BleedAmount, 3);
		Assert.Equal(painBefore + expected.Pain, limb.Pain, 3);
		Assert.Equal([ShrapnelGripSlipSeed.Of(ack.OperationId, 0, 1)], w.HostGripSlips.DrawnSeeds);
	}

	[Fact]
	public void BreakGrasp_WithNoEngineStream_LeavesTheLimbAlone()
	{
		var w = CreateThreeNode();
		w.HostGripSlips.Available = false;
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
		g1Ops.SendShrapnelUpdate(ack!.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, X = 10f, Y = -100f, Grabbed = true, OwnershipChange = true });

		var before = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		var skinBefore = before.SkinHealth;
		var bleedBefore = before.BleedAmount;
		var painBefore = before.Pain;
		var states = new List<MedicalOperationStateMsg>();
		Ops(w.Host).StateReceived += states.Add;

		g1Ops.SendShrapnelUpdate(ack.OperationId, new ShrapnelPieceUpdate { PieceIndex = 0, BreakGrasp = true, GripSlipNonce = 1 });

		// A build with no engine stream applies nothing rather than inventing a value: the slip is
		// refused, and the piece is still released so the session does not wedge on it.
		var limb = characters.GetHostCharacterData()!.Limbs.Single(l => l.Index == 1);
		Assert.Equal(skinBefore, limb.SkinHealth, 3);
		Assert.Equal(bleedBefore, limb.BleedAmount, 3);
		Assert.Equal(painBefore, limb.Pain, 3);
		Assert.Equal(0UL, states.Last(m => m.ShrapnelPieces.Any(p => p.PieceIndex == 0)).ShrapnelPieces.Single(p => p.PieceIndex == 0).OwnerSteamId);
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
		PlayerInteractionTestSession.SeedOwnBody(w.Guest1, guestSnapshot);
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
