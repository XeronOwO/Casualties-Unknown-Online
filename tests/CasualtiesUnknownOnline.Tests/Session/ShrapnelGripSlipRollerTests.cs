using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The authority's half of the derived shrapnel grip-slip window, in isolation: which state a slip
/// derives from, what the authority does when this build has no engine stream, and how the values the
/// operator's screen reported are compared against the ones the authority derived.
/// </summary>
public class ShrapnelGripSlipRollerTests
{
	private const ulong OperatorId = 2001;

	private static ShrapnelOperationSession Session() => new() { OperationId = 4242, Target = 1001, LimbIndex = 1 };

	/// <summary>A break-grasp report as the operator's client sends it, with the values it read off its own screen.</summary>
	private static MedicalOperationUpdateMsg Report(int pieceIndex, int nonce, ShrapnelGripSlipValues? reported = null) => new()
	{
		PieceIndex = pieceIndex,
		BreakGrasp = true,
		GripSlipNonce = nonce,
		GripSlipSkin = reported?.Skin ?? 0f,
		GripSlipBleed = reported?.Bleed ?? 0f,
		GripSlipPain = reported?.Pain ?? 0f,
		GripSlipBrainLoss = reported?.BrainLoss ?? 0f,
	};

	[Fact]
	public void Roll_DrawsFromTheSlipsOwnIdentity()
	{
		var random = new FakeShrapnelGripSlipRandom();
		var log = new RecordingLogger<ShrapnelGripSlipRoller>();
		var roller = new ShrapnelGripSlipRoller(random, log);

		var values = roller.Roll(Session(), OperatorId, Report(pieceIndex: 2, nonce: 3), isHead: false);

		var expectedSeed = ShrapnelGripSlipSeed.Of(4242, 2, 3);
		Assert.Equal([expectedSeed], random.DrawnSeeds);
		Assert.Equal(FakeShrapnelGripSlipRandom.Script(expectedSeed, false), values);
	}

	[Fact]
	public void Roll_GivesEverySlipOfTheSamePieceItsOwnState()
	{
		var random = new FakeShrapnelGripSlipRandom();
		var roller = new ShrapnelGripSlipRoller(random, new RecordingLogger<ShrapnelGripSlipRoller>());
		var session = Session();

		roller.Roll(session, OperatorId, Report(pieceIndex: 2, nonce: 1), isHead: false);
		roller.Roll(session, OperatorId, Report(pieceIndex: 2, nonce: 2), isHead: false);
		roller.Roll(session, OperatorId, Report(pieceIndex: 3, nonce: 2), isHead: false);

		Assert.Equal(3, random.DrawnSeeds.Count);
		Assert.Equal(3, new HashSet<int>(random.DrawnSeeds).Count);
	}

	[Fact]
	public void Roll_PassesTheHeadFlagToTheDraw()
	{
		var random = new FakeShrapnelGripSlipRandom();
		var roller = new ShrapnelGripSlipRoller(random, new RecordingLogger<ShrapnelGripSlipRoller>());
		var session = Session();

		var head = roller.Roll(session, OperatorId, Report(0, 1), isHead: true);
		var body = roller.Roll(session, OperatorId, Report(0, 1), isHead: false);

		// The flag the ROLLER passed on, asserted on its own: the returned values alone cannot pin it,
		// because the fake's script yields the same four numbers for a head limb whose brain gate misses
		// — a hardcoded `false` would pass whenever that gate happened to miss.
		Assert.Equal([true, false], random.DrawnHeadFlags);
		Assert.Equal(FakeShrapnelGripSlipRandom.Script(ShrapnelGripSlipSeed.Of(4242, 0, 1), true), head);
		Assert.Equal(FakeShrapnelGripSlipRandom.Script(ShrapnelGripSlipSeed.Of(4242, 0, 1), false), body);
	}

	[Fact]
	public void Roll_WithNoEngineStream_AppliesNothingAndSaysSo()
	{
		var random = new FakeShrapnelGripSlipRandom { Available = false };
		var log = new RecordingLogger<ShrapnelGripSlipRoller>();
		var roller = new ShrapnelGripSlipRoller(random, log);

		var values = roller.Roll(Session(), OperatorId, Report(0, 1), isHead: false);

		Assert.Equal(default, values);
		Assert.True(log.HasError("applied NO damage"));
	}

	[Fact]
	public void Roll_WhenBothSidesDrewTheSameValues_StaysQuiet()
	{
		var drawn = new ShrapnelGripSlipValues(5f, 0.5f, 10f, 0f);
		var random = new FakeShrapnelGripSlipRandom { NextDraw = drawn };
		var log = new RecordingLogger<ShrapnelGripSlipRoller>();
		var roller = new ShrapnelGripSlipRoller(random, log);

		var values = roller.Roll(Session(), OperatorId, Report(0, 1, drawn), isHead: false);

		Assert.Equal(drawn, values);
		Assert.False(log.HasWarning("DISAGREE"));
	}

	[Fact]
	public void Roll_WhenTheReportedValuesDiffer_WarnsWithBothSets()
	{
		var random = new FakeShrapnelGripSlipRandom { NextDraw = new ShrapnelGripSlipValues(5f, 0.5f, 10f, 0f) };
		var log = new RecordingLogger<ShrapnelGripSlipRoller>();
		var roller = new ShrapnelGripSlipRoller(random, log);

		// The authority keeps judging: what it returns is its OWN draw, disagreement or not.
		var values = roller.Roll(Session(), OperatorId, Report(0, 1, new ShrapnelGripSlipValues(5f, 0.5f, 10.5f, 0f)), isHead: false);

		Assert.Equal(new ShrapnelGripSlipValues(5f, 0.5f, 10f, 0f), values);
		Assert.True(log.HasWarning("DISAGREE"));
	}

	[Fact]
	public void Roll_WithANonceOfZero_SaysTheOperatorHeldNoWindow()
	{
		var random = new FakeShrapnelGripSlipRandom { NextDraw = new ShrapnelGripSlipValues(5f, 0.5f, 10f, 0f) };
		var log = new RecordingLogger<ShrapnelGripSlipRoller>();
		var roller = new ShrapnelGripSlipRoller(random, log);

		var values = roller.Roll(Session(), OperatorId, Report(0, nonce: 0), isHead: false);

		Assert.Equal(new ShrapnelGripSlipValues(5f, 0.5f, 10f, 0f), values);
		Assert.True(log.HasWarning("nonce 0"));
		Assert.False(log.HasWarning("DISAGREE"));
	}
}
