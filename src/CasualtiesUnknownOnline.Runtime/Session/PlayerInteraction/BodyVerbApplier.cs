using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The body verb family's per-target APPLICATION: the only part of the family that touches the body seams
/// (<see cref="ILocalBodyReset"/>, <see cref="ILocalBodyKill"/>) and the only part that sends an instruction to a
/// member's own client.
///
/// <para>
/// It decides nothing about permission or the target roster — the control plane in
/// <see cref="PlayerHealResetService"/> hands it one target that was already admitted and asks for the operation to be
/// carried out. It owns no outstanding-instruction table either: it reports whether the transport accepted an
/// instruction, and the control plane records the ticket an applied result must echo. That keeps the judgement (may this
/// request land?) in one place and the application (make this body change) in another.
/// </para>
///
/// <para>
/// The judgement-ownership rule decides the routing: the host's own body is applied HERE, on the host's client, and a
/// member's body is only ever an INSTRUCTION to the client that owns it. Both routes reach the same adapter seam, which
/// mirrors the game's own console verb (<c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c>: <c>heal</c> at
/// line 530, <c>kill</c> at line 620).
/// </para>
/// </summary>
internal sealed class BodyVerbApplier(
	PacketSender sender,
	PlayerCharacterAccess access,
	ILocalBodyReset localReset,
	ILocalBodyKill localKill,
	BodyVerbAnswers answers,
	ILogger log)
{
	/// <summary>One target's application attempt, in the facts the control plane aggregates and the console prints.</summary>
	internal readonly record struct Applied(bool Ok, string Line, IReadOnlyList<ConsoleAnswerPartMsg> Parts);

	/// <summary>The host's own body: apply the operation here, at once, and answer with what it did.</summary>
	internal Applied ApplyLocally(ulong actor, ulong target, BodyOperation operation)
	{
		if (operation == BodyOperation.Kill)
		{
			var killed = localKill.Kill();
			log.LogInformation("[Kill] {Actor} killed the host's own body (brainBefore={Before:0.#}, applied={Applied}).",
				actor, killed.BrainHealthBefore, killed.Applied);
			return new Applied(
				killed.Applied,
				BodyVerbAnswers.Killed(target, killed),
				[killed.Applied
					? answers.AnswerPart("kill.answer.applied", BodyVerbAnswers.Argument(target))
					: answers.AnswerPart("kill.answer.refused_for", BodyVerbAnswers.Argument(target))]);
		}

		var outcome = localReset.Reset(null);
		log.LogInformation("[HealReset] {Actor} reset the host's own body (wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}, applied={Applied}).",
			actor, outcome.WasDead, outcome.RestoredLimbs, outcome.HollowCleared, outcome.Applied);
		var parts = new List<ConsoleAnswerPartMsg>(4);
		if (outcome.Applied)
		{
			parts.Add(answers.AnswerPart("heal.answer.reset_applied", BodyVerbAnswers.Argument(target)));
			answers.AddResetClauses(parts, outcome);
		}
		else
		{
			parts.Add(answers.AnswerPart("heal.answer.refused_for", BodyVerbAnswers.Argument(target)));
			parts.Add(answers.AnswerPart("heal.answer.reason_body_refused"));
		}

		return new Applied(outcome.Applied, BodyVerbAnswers.Applied(target, outcome), parts);
	}

	/// <summary>
	/// A member's body: send the instruction that client applies to its own body. The id is the host's ticket for the
	/// applied result and is allocated by the control plane, so this method only reports whether the frame left.
	/// </summary>
	internal Applied InstructMember(ulong actor, ulong target, BodyOperation operation, ulong instructionId)
	{
		var wasDead = access.GetCharacterData(target)?.Health is { Alive: false };
		if (!sender.TrySend(target, NetMsg.HealResetInstruction, new HealResetInstructionMsg
		{
			OperatorSteamId = actor,
			InstructionId = instructionId,
			Operation = operation,
		}))
		{
			// The transport refused the frame locally: nothing was sent, so no result can answer this
			// instruction and the operator must not read "sent".
			log.LogWarning("[HealReset] {Actor}'s instruction to {Target} (instruction {InstructionId}) was not accepted by the transport; no reset is pending.",
				actor, target, instructionId);
			return new Applied(
				false,
				BodyVerbAnswers.RefusedFor(target, "the transport did not accept the reset instruction, so nothing was sent."),
				[
					answers.AnswerPart("heal.answer.refused_for", BodyVerbAnswers.Argument(target)),
					answers.AnswerPart("heal.answer.reason_transport_refused"),
				]);
		}

		log.LogInformation("[HealReset] {Actor} instructed {Target} to reset its own body (instruction {InstructionId}, wasDead={WasDead}).",
			actor, target, instructionId, wasDead);
		// The answer names the operation that was actually sent: a kill instruction must not read as a reset instruction.
		return operation == BodyOperation.Kill
			? new Applied(true, BodyVerbAnswers.SentKillToMember(target), [answers.AnswerPart("kill.answer.sent", BodyVerbAnswers.Argument(target))])
			: new Applied(
				true,
				BodyVerbAnswers.SentToMember(target, wasDead),
				[answers.AnswerPart(wasDead ? "heal.answer.sent_dead" : "heal.answer.sent", BodyVerbAnswers.Argument(target))]);
	}

	/// <summary>
	/// THIS client's own body, by the host's instruction. A reset answers with the notice the target's console prints;
	/// a kill writes the body and reports through the ordinary character snapshot, so it produces no notice.
	/// </summary>
	internal HealResetNotice? ApplyInstruction(HealResetInstructionMsg msg)
	{
		if (msg.Operation == BodyOperation.Kill)
		{
			var killed = localKill.Kill();
			if (killed.Applied)
			{
				log.LogInformation("[Kill] the host's instruction from {Operator} (instruction {InstructionId}) killed this body: brain health {Before:0.#} -> 0.",
					msg.OperatorSteamId, msg.InstructionId, killed.BrainHealthBefore);
			}
			else
			{
				log.LogWarning("[Kill] the host's instruction from {Operator} (instruction {InstructionId}) could not be applied here: {Reason}",
					msg.OperatorSteamId, msg.InstructionId, killed.Refusal);
			}

			return null;
		}

		var outcome = localReset.Reset(msg.InstructionId);
		var text = BodyVerbAnswers.Notice(msg.OperatorSteamId, outcome);
		if (outcome.Applied)
		{
			log.LogInformation("[HealReset] the host's instruction from {Operator} (instruction {InstructionId}) reset this body: wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}.",
				msg.OperatorSteamId, msg.InstructionId, outcome.WasDead, outcome.RestoredLimbs, outcome.HollowCleared);
		}
		else
		{
			log.LogWarning("[HealReset] the host's instruction from {Operator} (instruction {InstructionId}) could not be applied here: {Reason}",
				msg.OperatorSteamId, msg.InstructionId, outcome.Refusal);
		}

		return new HealResetNotice(msg.OperatorSteamId, outcome, text);
	}
}
