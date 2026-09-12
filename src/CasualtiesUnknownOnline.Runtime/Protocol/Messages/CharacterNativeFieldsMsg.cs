using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The native CHARACTER fields a character snapshot has to carry, in one
/// sub-message: the happiness history, the calorie counter and the wound
/// window's character details.
///
/// They are exactly the character-level fields the native <c>SaveSystem</c>
/// write/read pair carries (<c>SaveSystem.cs:172-180</c> / <c>:438-441</c>), which
/// CUO stopped reading when it stopped reading <c>save.sv</c> (decision 165) —
/// the archive and the reconnect hand-over have to carry them instead, and the
/// local restore path writes them back. Their run-level siblings
/// (<c>savedRunTime</c>, the recipe unlock table, the two rarity multipliers) live
/// in the run baseline; these three belong to ONE character and travel with its
/// snapshot.
///
/// <see cref="CharacterDataMsg.NativeFields"/> is null for a snapshot that does
/// not carry them — an older sender, or a build that predates the capture — and
/// the restore NAMES that gap instead of quietly leaving the game's defaults
/// (§6: a continued character must never silently lose the fields the native save
/// would have restored).
/// </summary>
[ProtoContract]
public sealed class CharacterNativeFieldsMsg
{
	/// <summary>The body's happiness history (<c>Body.lastHappiness</c>, <c>float[10]</c>, shifted once per frame by <c>LastHappinessUpdater</c>) — the value the game's pause tooltip and its last-chance evaluation read.</summary>
	[ProtoMember(1)]
	public List<float> LastHappiness { get; set; } = [];

	/// <summary>The per-run calorie counter (<c>PlayerCamera.caloriesConsumed</c>) — shown on the death-stats screen.</summary>
	[ProtoMember(2)]
	public int CaloriesConsumed { get; set; }

	/// <summary>The wound window's character details, in the native <c>SetCharDetails</c> order: `[height, age, id, version]` (<c>WoundView.cInfo</c>, written by <c>WoundView.SetCharDetails</c>, <c>WoundView.cs:54-68</c>). A restore re-enters through that same method, so the window's text and the two statics it updates (<c>firmwareVer</c>, <c>specimenId</c>) stay in step with the value a SaveSystem load would have produced.</summary>
	[ProtoMember(3)]
	public List<int> CharacterInfo { get; set; } = [];
}
