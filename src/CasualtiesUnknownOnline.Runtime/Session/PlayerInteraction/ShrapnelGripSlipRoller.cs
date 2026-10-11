using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The authority's half of one shrapnel grip slip's derived random window: it derives the values the
/// operator's own screen just drew from the same seed, and checks them against the values that screen
/// reported. What it RETURNS is what the authoritative limb is written with — the operator's numbers
/// are evidence, never the verdict.
/// <para>
/// The comparison is the mechanism's own runtime proof. The two machines draw the same values only
/// while their draw sequences stay aligned: the native <c>ShrapnelMinigame.BreakGrasp</c> reads the
/// stream in a fixed order that includes the gore clip pick and the pitch shift inside
/// <c>Sound.Play</c>, and a game update that changes that order would split the two screens again —
/// silently, because both would still look like an ordinary roll. A disagreement is therefore logged
/// with both sets of raw values, so the acceptance run reads "the sequences still agree" instead of
/// assuming it.
/// </para>
/// </summary>
internal sealed class ShrapnelGripSlipRoller(IShrapnelGripSlipRandom random, ILogger log)
{
	/// <summary>
	/// How far the reported and the derived value may differ before the sequences are called misaligned.
	/// The two numbers travel different routes — the authority's is the draw itself, the operator's is
	/// the difference between two float limb fields the draw was applied to, so it carries that
	/// subtraction's rounding. A real misalignment moves a value by a whole draw step, orders of
	/// magnitude above this.
	/// </summary>
	private const float RelativeTolerance = 1e-3f;

	private readonly IShrapnelGripSlipRandom _random = random;
	private readonly ILogger _log = log;

	/// <summary>
	/// Derives this slip's values and records how they compare with the reporting client's. Returns the
	/// derived values, or an all-zero set when this build has no engine stream — in which case the slip
	/// applies no damage and the error line says exactly that.
	/// </summary>
	internal ShrapnelGripSlipValues Roll(ShrapnelOperationSession shrapnel, ulong operatorId, MedicalOperationUpdateMsg msg, bool isHead)
	{
		var seed = ShrapnelGripSlipSeed.Of(shrapnel.OperationId, msg.PieceIndex, msg.GripSlipNonce);
		if (_random.TryDraw(seed, isHead) is not { } derived)
		{
			_log.LogError(
				"[Shrapnel] session {OperationId} grip slip {Nonce} on piece {Piece} by {Operator} applied NO damage: this build registers no engine random stream to derive the window from.",
				shrapnel.OperationId, msg.GripSlipNonce, msg.PieceIndex, operatorId);
			return default;
		}

		_log.LogInformation(
			"[Shrapnel] session {OperationId} grip slip {Nonce} on piece {Piece} by {Operator} (head={Head}, seed {Seed}): derived skin {Skin} bleed {Bleed} pain {Pain} brain {Brain}.",
			shrapnel.OperationId, msg.GripSlipNonce, msg.PieceIndex, operatorId, isHead, seed,
			derived.Skin, derived.Bleed, derived.Pain, derived.BrainLoss);

		Compare(shrapnel, operatorId, msg, derived);
		return derived;
	}

	private void Compare(ShrapnelOperationSession shrapnel, ulong operatorId, MedicalOperationUpdateMsg msg, ShrapnelGripSlipValues derived)
	{
		if (msg.GripSlipNonce == 0)
		{
			// A nonce of 0 is the reporting client saying it held no derived window: its screen drew
			// the native values from the free-running stream, so there is nothing to compare. The
			// packaged build always registers the window, so this line is the missing-registration
			// symptom rather than a routine case.
			_log.LogWarning(
				"[Shrapnel] session {OperationId} grip slip on piece {Piece} by {Operator} arrived with nonce 0: the reporting client held no derived window and drew from the free-running stream.",
				shrapnel.OperationId, msg.PieceIndex, operatorId);
			return;
		}

		var reported = new ShrapnelGripSlipValues(msg.GripSlipSkin, msg.GripSlipBleed, msg.GripSlipPain, msg.GripSlipBrainLoss);
		if (!Differs(reported.Skin, derived.Skin)
			&& !Differs(reported.Bleed, derived.Bleed)
			&& !Differs(reported.Pain, derived.Pain)
			&& !Differs(reported.BrainLoss, derived.BrainLoss))
		{
			_log.LogDebug(
				"[Shrapnel] session {OperationId} grip slip {Nonce} on piece {Piece}: both sides derived the same values.",
				shrapnel.OperationId, msg.GripSlipNonce, msg.PieceIndex);
			return;
		}

		_log.LogWarning(
			"[Shrapnel] session {OperationId} grip slip {Nonce} on piece {Piece} by {Operator}: the draw sequences DISAGREE — the operator's screen reported skin {ReportedSkin} bleed {ReportedBleed} pain {ReportedPain} brain {ReportedBrain}, the authority derived skin {Skin} bleed {Bleed} pain {Pain} brain {Brain}.",
			shrapnel.OperationId, msg.GripSlipNonce, msg.PieceIndex, operatorId,
			reported.Skin, reported.Bleed, reported.Pain, reported.BrainLoss,
			derived.Skin, derived.Bleed, derived.Pain, derived.BrainLoss);
	}

	private static bool Differs(float reported, float derived) =>
		Math.Abs(reported - derived) > RelativeTolerance * Math.Max(1f, Math.Max(Math.Abs(reported), Math.Abs(derived)));
}
