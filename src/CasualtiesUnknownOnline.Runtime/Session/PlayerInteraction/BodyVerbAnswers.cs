using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The body verb family's answers: what ONE target's application looked like, rendered twice — as the English sentence
/// the operator's own log and a client without the catalogue entry read, and as the catalogue key plus its values that
/// the reader's console renders in the reader's own language.
///
/// <para>
/// This is the only part of the family that differs per operation, and it is pure formatting: it owns no session, no
/// roster and no body seam, so the same target fact always produces the same sentence and the same key. The control
/// plane in <see cref="PlayerHealResetService"/> decides WHICH facts are answered; this type decides how they read.
/// </para>
/// </summary>
internal sealed class BodyVerbAnswers(ILocalizationService localization, ILogger log)
{
	/// <summary>
	/// One answer part and the text the same key renders to here: the part is what the member's console RENDERS in the member's
	/// language, the text is what the host's log and a client without the entry fall back to. The Debug line is that rendering,
	/// which is also what makes the census count the literal as a READ of the catalogue rather than an orphan.
	/// </summary>
	internal (ConsoleAnswerPartMsg Part, string Line) AnswerLine(string labelKey, params string[] args)
	{
		var line = localization.Format(labelKey, args);
		log.LogDebug("[HealReset] answer line: {Line}", line);
		var key = labelKey;
		return (new ConsoleAnswerPartMsg { Key = key, Args = [.. args] }, line);
	}

	/// <summary>One keyed answer part; the same Debug rendering keeps the catalogue read visible to the census.</summary>
	internal ConsoleAnswerPartMsg AnswerPart(string labelKey, params string[] args)
	{
		log.LogDebug("[HealReset] answer part: {Line}", localization.Format(labelKey, args));
		var key = labelKey;
		return new ConsoleAnswerPartMsg { Key = key, Args = [.. args] };
	}

	/// <summary>
	/// One refused request, as the answer the member's console renders: the KEY plus its arguments, so the reader's own
	/// language wins (the answer half of the localization rule). The text is the same key rendered here.
	/// </summary>
	internal HealResetResultMsg Refused((ConsoleAnswerPartMsg Part, string Line) answer)
	{
		log.LogWarning("[HealReset] refused: {Reason}", answer.Line);
		return new HealResetResultMsg
		{
			Applied = false,
			Answer = answer.Line,
			Parts = [answer.Part],
		};
	}

	/// <summary>The parenthetical facts a successful reset reports, each as its own catalogue part so both languages own them.</summary>
	internal void AddResetClauses(List<ConsoleAnswerPartMsg> parts, LocalBodyResetOutcome outcome)
	{
		if (outcome.WasDead)
		{
			parts.Add(AnswerPart("heal.answer.clause_revived"));
		}

		if (outcome.RestoredLimbs > 0)
		{
			parts.Add(AnswerPart("heal.answer.clause_limbs_restored", Argument(outcome.RestoredLimbs)));
		}

		if (outcome.HollowCleared)
		{
			parts.Add(AnswerPart("heal.answer.clause_hollow_cleared"));
		}
	}

	/// <summary>The host reset its own body: the sentence names the target and every clause the reset produced, or the refusal.</summary>
	internal static string Applied(ulong target, LocalBodyResetOutcome outcome)
	{
		if (!outcome.Applied)
		{
			return RefusedFor(target, outcome.Refusal);
		}

		var clauses = new List<string>(3);
		if (outcome.WasDead)
		{
			clauses.Add("revived from death");
		}

		if (outcome.RestoredLimbs > 0)
		{
			clauses.Add(FormattableString.Invariant($"{outcome.RestoredLimbs} severed limb(s) restored"));
		}

		if (outcome.HollowCleared)
		{
			clauses.Add("hollow cleared");
		}

		return FormattableString.Invariant($"Reset {target}: full heal applied{Clauses(clauses)}");
	}

	/// <summary>The host instructed a member's own client to reset, naming whether that body was dead when the instruction left.</summary>
	internal static string SentToMember(ulong target, bool wasDead) =>
		FormattableString.Invariant($"Reset {target}: sent to that player's own client, which applies the full reset and re-reports the state")
		+ (wasDead ? " — it was dead, so it revives." : ".");

	/// <summary>The host instructed a member's own client to kill; a kill instruction must not read as a reset instruction.</summary>
	internal static string SentKillToMember(ulong target) =>
		FormattableString.Invariant($"Sent the kill instruction to {target}; its own client applies it and reports the body back.");

	/// <summary>The host killed its own body: the native write's before-value, or the ground when there was nothing to kill.</summary>
	internal static string Killed(ulong target, LocalBodyKillOutcome outcome) =>
		outcome.Applied
			? FormattableString.Invariant($"Killed {target}: brain health {outcome.BrainHealthBefore:0.#} -> 0.")
			: RefusedFor(target, outcome.Refusal ?? "the body could not be killed.");

	/// <summary>The target's own console, after the host's reset instruction landed on this client.</summary>
	internal static string Notice(ulong operatorSteamId, LocalBodyResetOutcome outcome)
	{
		if (!outcome.Applied)
		{
			return FormattableString.Invariant($"A full reset from {operatorSteamId} could not be applied here: {outcome.Refusal}");
		}

		var clauses = new List<string>(3);
		if (outcome.WasDead)
		{
			clauses.Add("revived from death");
		}

		if (outcome.RestoredLimbs > 0)
		{
			clauses.Add(FormattableString.Invariant($"{outcome.RestoredLimbs} severed limb(s) restored"));
		}

		if (outcome.HollowCleared)
		{
			clauses.Add("hollow cleared");
		}

		return FormattableString.Invariant($"{operatorSteamId} fully reset your state{Clauses(clauses)}");
	}

	/// <summary>One target's refusal, in the sentence every answer part builds on.</summary>
	internal static string RefusedFor(ulong target, string reason) =>
		FormattableString.Invariant($"Refused for {target}: {reason}");

	/// <summary>A number as one answer value, invariant so the same fact reads the same in every catalogue render.</summary>
	internal static string Argument(ulong value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>The same for a count (severed limbs restored), which no target id shares a type with.</summary>
	internal static string Argument(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>The parenthetical clause list both sentences use, so the target's line and the operator's answer read the same.</summary>
	private static string Clauses(IReadOnlyList<string> clauses)
	{
		if (clauses.Count == 0)
		{
			return ".";
		}

		var builder = new StringBuilder(" (");
		for (var i = 0; i < clauses.Count; i++)
		{
			if (i > 0)
			{
				builder.Append("; ");
			}

			builder.Append(clauses[i]);
		}

		builder.Append(").");
		return builder.ToString();
	}
}
