using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The unlocked recipe set of the SENDER — every index whose recipe currently
/// draws no INT requirement (<c>Recipe.INT == 0</c>, the state the game's own
/// blueprint use writes at <c>Item.cs:4284</c>). It is the absolute form of
/// <see cref="RecipeUnlockMsg"/> and exists because that message is one-shot:
/// the recipe table is a per-process static, so a swallowed report/relay or a
/// member that joined after the unlock had nothing to heal it.
///
/// Direction: guest → host report of the guest's set (the swallowed-report
/// fallback, re-sent on the shared cadence until the host's set carries it);
/// host → guest the host's authoritative set (world entry / 60 s repair).
/// The host merges a guest's set — an unlock is monotonic and irreversible on
/// the side that spent the blueprint, so a union is the only merge that can lose
/// nothing — and relays the indices it did not hold through the ordinary
/// unlock path, which is what keeps the host the authority over the set.
///
/// The set is UNLOCK-ONLY: an empty list (or a missing index) means "nothing
/// more is unlocked here", never "lock this back". Index 0 is a valid index
/// (blueprints roll <c>RecipeRange(0, Count)</c>).
/// </summary>
[ProtoContract]
public sealed class RecipeUnlockSnapshotMsg
{
	[ProtoMember(1)]
	public List<int> RecipeIndexes { get; set; } = [];
}
