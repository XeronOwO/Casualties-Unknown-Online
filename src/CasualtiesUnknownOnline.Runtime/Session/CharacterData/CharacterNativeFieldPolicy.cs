using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The rules for a character snapshot's NATIVE fields — <c>Body.lastHappiness</c>,
/// <c>PlayerCamera.caloriesConsumed</c> and the wound window's <c>cInfo</c> (the
/// height/age/id/version the native <c>SetCharDetails</c> writes,
/// <c>WoundView.cs:54-68</c>). These are exactly the character-level fields the
/// native save carried and CUO no longer reads (decision 165): the snapshot has to
/// carry them instead, and the local restore path writes them back (decision 166,
/// S3.4b).
///
/// The rules live here rather than in the adapter because they are accounting:
/// which counts a snapshot may carry, what a failed read reports, and which value a
/// restore must refuse BY NAME instead of writing a truncated or invented one
/// (§6: no silent default, in either direction). The adapter reads the live scene
/// and performs the writes; this type decides, from the snapshot alone, whether
/// each field is writable at all. That split is also what makes the rules
/// testable: everything here is plain values, no game types.
///
/// <see cref="CharacterDataMsg.NativeFields"/> is null for a snapshot that carries
/// none of them (an old sender, a build that predates the capture, or a scene that
/// was not there to be read); <see cref="Missing"/> is how such a snapshot is NAMED
/// in the restore report.
/// </summary>
internal static class CharacterNativeFieldPolicy
{
	/// <summary>The happiness window the game itself uses (<c>Body.cs:4283</c>, <c>new float[10]</c>) — and therefore the only history length a snapshot may carry.</summary>
	internal const int HappinessWindowLength = 10;

	/// <summary>The native <c>SetCharDetails</c> arity: height, age, id, version.</summary>
	internal const int CharacterDetailCount = 4;

	/// <summary>The one reason a read can fail with every object present: the live scene did not supply both the camera and the wound window.</summary>
	private const string MissingScene = "the run scene's player camera or wound window is not there";

	/// <summary>The field names as the restore report spells them.</summary>
	private const string FieldNames = "lastHappiness, caloriesConsumed, WoundView.cInfo";

	/// <summary>
	/// Whether one read of the live scene describes a whole character's native
	/// fields. False — with <paramref name="failure"/> saying what was missing —
	/// when the three fields do not belong to one coherent instant: no camera or no
	/// wound window, a happiness window that is not the game's own length, or a
	/// wound window whose details are not the native four. A failed read reports WHY
	/// instead of returning zeros: zero is a real value here (a new character's
	/// window IS 0 cm / 0 y / #0), so a restore built from a failed read would be
	/// indistinguishable from a genuine one.
	/// </summary>
	/// <param name="happinessLength">The body's happiness window length, or 0 when the body has none.</param>
	/// <param name="liveScene">True = the live scene supplied both the camera and the wound window.</param>
	/// <param name="characterInfoLength">The wound window's details length, or 0 when the scene read failed.</param>
	/// <param name="failure">Why no whole snapshot could be read; null when one could.</param>
	internal static bool TryCapture(
		int happinessLength,
		bool liveScene,
		int characterInfoLength,
		out string? failure)
	{
		if (!liveScene)
		{
			failure = MissingScene;
			return false;
		}

		if (happinessLength != HappinessWindowLength)
		{
			failure = $"the body's happiness window holds {happinessLength} value(s), not the game's {HappinessWindowLength}";
			return false;
		}

		if (characterInfoLength != CharacterDetailCount)
		{
			failure = $"the wound window's character details hold {characterInfoLength} value(s), not the native {CharacterDetailCount}";
			return false;
		}

		failure = null;
		return true;
	}

	/// <summary>
	/// What the restore path must write per field, in the snapshot's own order: one
	/// entry each for the happiness history, the calorie counter and the wound
	/// window's details. A refused entry is a value the native contract cannot hold
	/// — the adapter names it and leaves the live value alone, never a truncation
	/// and never a silent skip (§6).
	///
	/// The counts are the whole decision, so the adapter cannot reach a different
	/// one by reading the message again: it walks this list and writes what it is
	/// told. A snapshot with no native fields at all plans nothing — the caller
	/// names that whole absence instead (see <see cref="Missing"/>).
	/// </summary>
	internal static List<NativeFieldWrite> Plan(CharacterDataMsg data)
	{
		if (data.NativeFields is not { } fields)
		{
			return [];
		}

		// A null list is the shape a hand-edited or corrupt JSON row arrives in
		// (protobuf never produces one): it is not a value to write, so it takes the
		// same named refusal a short one does — never a throw inside the restore.
		var happiness = fields.LastHappiness ?? [];
		var details = fields.CharacterInfo ?? [];

		var plan = new List<NativeFieldWrite>(3);

		if (happiness.Count != HappinessWindowLength)
		{
			// Exactly the game's own window or nothing. A short row would only fill
			// the first slots of the ten-value array, and the game reads ALL of them:
			// AverageHappiness averages the whole window (Body.cs:643-650) and the
			// last-chance evaluation reads slot 9 (Body.cs:957) — so a prefix write
			// silently mixes a restored history with the fresh body's zeros.
			plan.Add(NativeFieldWrite.Refused(
				NativeFieldKind.HappinessHistory,
				happiness.Count == 0
					? "the happiness history (the snapshot carries none)"
					: $"the happiness history ({happiness.Count} value(s), not the game's {HappinessWindowLength}-value window)",
				happiness.Count == 0
					? "the snapshot's native fields carry no happiness history"
					: $"the happiness history holds {happiness.Count} values, not the game's whole {HappinessWindowLength}-value window"));
		}
		else
		{
			plan.Add(NativeFieldWrite.Writable(NativeFieldKind.HappinessHistory, $"the happiness history ({HappinessWindowLength} value(s))"));
		}

		plan.Add(NativeFieldWrite.Writable(NativeFieldKind.CaloriesConsumed, $"caloriesConsumed ({fields.CaloriesConsumed})"));

		if (details.Count != CharacterDetailCount)
		{
			plan.Add(NativeFieldWrite.Refused(
				NativeFieldKind.CharacterInfo,
				$"WoundView.cInfo ({details.Count} value(s), not the native {CharacterDetailCount})",
				$"the character details hold {details.Count} value(s), not the native {CharacterDetailCount}"));
		}
		else
		{
			plan.Add(NativeFieldWrite.Writable(NativeFieldKind.CharacterInfo, "WoundView.cInfo (through SetCharDetails)"));
		}

		return plan;
	}

	/// <summary>
	/// The damage lines one restored character contributes to the restore report:
	/// EMPTY only when the snapshot is fully usable, and otherwise every field the
	/// plan refuses, by name. The key is the archive's own <c>steam-&lt;id&gt;</c> /
	/// <c>name-&lt;name&gt;</c> form — it is what the file is named after, so the
	/// report names a file a reader can open.
	///
	/// It is derived from <see cref="Plan"/> so the report and the write agree by
	/// construction: a snapshot whose non-null fields are malformed (a short
	/// happiness row, a details array that is not four) is damaged exactly as much
	/// as one that carries nothing, and neither is a quiet default (§6).
	/// </summary>
	internal static List<string> Missing(string playerKey, CharacterDataMsg character)
	{
		var plan = Plan(character);
		if (plan.Count == 0)
		{
			// Nothing at all: one sentence about the whole field set reads better than
			// three separate refusals, and it names the field names a reader can look up.
			return
			[
				$"the character snapshot of {playerKey} carries no native character fields ({FieldNames}), so that character continues with the game's defaults for them"
			];
		}

		var refused = new List<string>();
		foreach (var write in plan)
		{
			if (!write.Applied)
			{
				refused.Add(write.Description);
			}
		}

		return refused.Count == 0
			? []
			: [$"the character snapshot of {playerKey} cannot restore {string.Join(" and ", refused)}"];
	}
}
