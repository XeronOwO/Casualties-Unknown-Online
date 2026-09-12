using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The Game Adapter's half of the native character fields (S3.4b): it performs the
/// live reads and writes, and the Runtime's
/// <see cref="CharacterNativeFieldPolicy"/> owns the rules — which counts a
/// snapshot may carry, what a failed read reports, and which value a restore must
/// refuse by name.
///
/// The fields are the character-level siblings of the run fields the run baseline
/// carries (<c>savedRunTime</c>, the recipe unlock table, the two rarity
/// multipliers): <c>Body.lastHappiness</c> (the happiness history the pause
/// tooltip and the last-chance evaluation read),
/// <c>PlayerCamera.caloriesConsumed</c> (the death-stats counter) and
/// <c>WoundView.cInfo</c> (the wound window's height/age/id/version). The native
/// load wrote all three through <c>SaveSystem.TryLoadGame</c>
/// (<c>SaveSystem.cs:438-441</c>), which CUO no longer runs (decision 165) — and
/// because the game skips its own fresh roll when a run is being continued
/// (<c>PlayerCamera.Start</c>, <c>PlayerCamera.cs:726-729</c>), a continued
/// character would otherwise show 0 cm / 0 y / #0 and a zeroed calorie counter.
/// </summary>
internal static class CharacterNativeFields
{
	/// <summary>
	/// Captures the body's native fields for its snapshot, through the live-scene
	/// port. A null result — with <paramref name="failure"/> saying what was not
	/// there — is the NAMED gap the restore path reports instead of storing
	/// defaults.
	/// </summary>
	internal static CharacterNativeFieldsMsg? TryCapture(
		ICharacterNativeSystem system,
		Body body,
		out string? failure)
	{
		var happiness = body?.lastHappiness ?? [];
		var read = system.Read();
		if (!CharacterNativeFieldPolicy.TryCapture(happiness.Length, read is not null, read?.CharacterInfo.Length ?? 0, out failure))
		{
			return null;
		}

		return new CharacterNativeFieldsMsg
		{
			LastHappiness = [.. happiness],
			CaloriesConsumed = read!.Value.CaloriesConsumed,
			CharacterInfo = [.. read.Value.CharacterInfo],
		};
	}

	/// <summary>
	/// Writes a restored snapshot's native fields onto the live character, in the
	/// Runtime's plan order (happiness history, calorie counter, wound window), and
	/// says what each write did. A value the plan refused — or a live object that is
	/// no longer there — keeps its live value and is reported: never silently
	/// skipped (§6).
	/// </summary>
	internal static IReadOnlyList<NativeFieldWrite> Apply(
		ICharacterNativeSystem system,
		Body body,
		CharacterNativeFieldsMsg fields,
		CharacterDataMsg data)
	{
		var writes = new List<NativeFieldWrite>(3);
		foreach (var write in CharacterNativeFieldPolicy.Plan(data))
		{
			writes.Add(write.Applied ? Write(system, body, fields, write) : write);
		}

		return writes;
	}

	private static NativeFieldWrite Write(ICharacterNativeSystem system, Body body, CharacterNativeFieldsMsg fields, NativeFieldWrite write)
	{
		switch (write.Kind)
		{
			case NativeFieldKind.HappinessHistory:
				if (body == null) // Unity object — == (a scene-reload-destroyed body reads as null)
				{
					return NativeFieldWrite.Refused(NativeFieldKind.HappinessHistory, write.Description, "there is no local body to write the happiness history onto");
				}

				// The game's own updater shifts this same array in place, so the write
				// goes into the live array rather than replacing the reference. The
				// plan only marked this writable for a full game-length window, so
				// every slot the game reads is covered.
				for (var i = 0; i < fields.LastHappiness.Count; i++)
				{
					body.lastHappiness[i] = fields.LastHappiness[i];
				}

				return write;

			case NativeFieldKind.CaloriesConsumed:
				return system.TryApplyCalories(fields.CaloriesConsumed)
					? write
					: NativeFieldWrite.Refused(NativeFieldKind.CaloriesConsumed, write.Description, "there is no PlayerCamera in this scene");

			default:
				return system.SetCharacterDetails(fields.CharacterInfo)
					? write
					: NativeFieldWrite.Refused(NativeFieldKind.CharacterInfo, write.Description, "there is no WoundView in this scene");
		}
	}

	/// <summary>The live implementation: the two statics the game itself uses.</summary>
	internal sealed class LiveSystem : ICharacterNativeSystem
	{
		/// <summary>The one instance the composition root wires — it holds no state, only the two lookups.</summary>
		internal static readonly LiveSystem Instance = new();

		public CharacterFieldRead? Read()
		{
			var camera = PlayerCamera.main;
			var woundView = WoundView.view;
			if (camera == null || woundView == null) // Unity objects — ==
			{
				return null;
			}

			return new CharacterFieldRead(camera.caloriesConsumed, woundView.cInfo);
		}

		public bool TryApplyCalories(int caloriesConsumed)
		{
			var camera = PlayerCamera.main;
			if (camera == null) // Unity object — ==
			{
				return false;
			}

			camera.caloriesConsumed = caloriesConsumed;
			return true;
		}

		/// <summary>
		/// The native load's own entry point (<c>SaveSystem.cs:440</c>), so the
		/// window's text and the statics it updates follow one code path with the
		/// save system. The arity is checked by the policy, which owns the refusal;
		/// the null check is the scene-reload guard for the frame between the read
		/// and this write.
		/// </summary>
		public bool SetCharacterDetails(IReadOnlyList<int> details)
		{
			var woundView = WoundView.view;
			if (woundView == null) // Unity object — ==
			{
				return false;
			}

			woundView.SetCharDetails(details[0], details[1], details[2], details[3]);
			return true;
		}
	}
}
