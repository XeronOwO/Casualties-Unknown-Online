using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The keypad codes as one table: read every keypad's code (generating it
/// host-side when the game has not done so yet — <c>Openable</c> lazy-generates
/// on first use per side, <c>Openable.cs:19</c>, so each side would otherwise
/// decide its own code), and write a decided set back by world position.
///
/// Both the live broadcast (<see cref="WorldEventSync"/>) and the save restore
/// (<see cref="NativeWorldFacts"/>) read and write the SAME table, so the host's
/// authority has one implementation: the entries are position-keyed because both
/// sides regenerate the same keypad at the same place, and the 3 m tolerance
/// absorbs the placement jitter of a regenerated entity.
/// </summary>
internal static class KeypadCodeTable
{
	/// <summary>Position-key radius — a regenerated keypad is at the same place on both sides, within placement jitter.</summary>
	private const float PositionTolerance = 3f;

	/// <summary>Every keypad's code, generating it host-side when unset (the caller decides whether to broadcast).</summary>
	internal static List<KeypadEntryMsg> Capture()
	{
		var codes = new List<KeypadEntryMsg>();
		foreach (var openable in Object.FindObjectsOfType<Openable>())
		{
			if (!openable.isKeypad)
			{
				continue;
			}

			var pos = openable.transform.position;
			codes.Add(new KeypadEntryMsg
			{
				Position = new NetVector2(pos.x, pos.y).ToNetVector2Msg(),
				Code = EnsureCode(openable),
			});
		}

		return codes;
	}

	/// <summary>
	/// Read the Openable's code, generating it host-side if unset (the game's
	/// lazy generation is per side; the host's Random stream is the authority).
	/// The runtime-creation channel also uses this at relay time: a created
	/// keypad's code is creation-time data, carried in its EntitySpawnedMsg.
	/// </summary>
	internal static string EnsureCode(Openable openable)
	{
		var codeField = Traverse.Create(openable).Field("code");
		var existing = codeField.GetValue<string>();
		if (string.IsNullOrEmpty(existing))
		{
			existing = KeypadMinigame.GenerateCode(); // host authority — its Random stream decides
			codeField.SetValue(existing);
		}

		return existing;
	}

	/// <summary>
	/// Guest: the host's decided codes arrived — write them onto the local
	/// keypads. A code already set (a local first use raced the broadcast) is
	/// left alone; the 60 s cycle re-sends, so a fill-only apply converges.
	/// </summary>
	internal static int ApplyWhereUnset(IReadOnlyList<KeypadEntryMsg> codes) =>
		Apply(codes, overwrite: false);

	/// <summary>
	/// Host restore: the snapshot is authoritative — every matched keypad carries
	/// the restored code, including one the game generated before the replay ran
	/// (writing the restored value is the whole point of capturing it).
	/// </summary>
	internal static int ApplyAbsolute(IReadOnlyList<KeypadEntryMsg> codes) =>
		Apply(codes, overwrite: true);

	private static int Apply(IReadOnlyList<KeypadEntryMsg> codes, bool overwrite)
	{
		var applied = 0;
		foreach (var openable in Object.FindObjectsOfType<Openable>())
		{
			if (!openable.isKeypad)
			{
				continue;
			}

			var pos = openable.transform.position;
			var match = codes.FirstOrDefault(c =>
				Vector2.Distance(new Vector2(c.Position.X, c.Position.Y), new Vector2(pos.x, pos.y)) < PositionTolerance);
			if (match is null)
			{
				continue;
			}

			// MATCHED: the live Openable exists, and that is what the caller counts.
			// Counting at the WRITE instead would call a restored code that happens
			// to equal the rolled one "not applied" — the restored replay reads this
			// as "the world has a home for this row".
			applied++;
			var codeField = Traverse.Create(openable).Field("code");
			if (!overwrite && !string.IsNullOrEmpty(codeField.GetValue<string>()))
			{
				continue;
			}

			if (codeField.GetValue<string>() != match.Code)
			{
				codeField.SetValue(match.Code);
			}
		}

		return applied;
	}
}
