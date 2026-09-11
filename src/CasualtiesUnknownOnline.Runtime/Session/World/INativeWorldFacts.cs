using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world facts that only the Game Adapter can read back and re-apply: the
/// DECIDED values a layer's generation produced but the Runtime cannot see —
/// keypad codes and geyser liquid types. Both are captured with their entity's
/// world position, which is the identity (both sides regenerate the same object
/// at the same place).
///
/// It is the native half of <see cref="IWorldFactSource"/> and is deliberately
/// OPTIONAL: a build that registers no implementation captures and restores no
/// native fact, which is the layer-end path (a layer-end cut writes no in-layer
/// fact at all, so nothing is lost silently). A caller that HAS one captures it
/// into the cut and applies it after the Runtime facts.
///
/// The capture/apply split follows the game's own lifetime: those tables exist
/// only while a world object does, so a restore cannot put them back at the
/// Continue click — the adapter applies them at the seam where the native save
/// used to restore them, before generation consumes them (S3.4 of the save work).
/// Until that seam exists, an implementation must keep the handed-over values and
/// apply them there; it must NOT try to write them while the world object is
/// absent, because there is nothing to write to.
///
/// The game's own <c>WorldGeneration.world.blockDamages</c> list is deliberately
/// NOT part of this port yet: its rows would carry the same <c>block-damage</c>
/// kind as CUO's accumulated table, so a restore could not tell them apart and
/// would merge both into CUO's bounded registry. S3.2 owns that capture together
/// with the discriminator and the native applier.
/// </summary>
public interface INativeWorldFacts
{
	/// <summary>Every keypad code the host decided for this layer.</summary>
	IReadOnlyList<KeypadEntryMsg> CaptureKeypadCodes();

	/// <summary>Every geyser's decided liquid type.</summary>
	IReadOnlyList<GeyserStateEntryMsg> CaptureGeysers();

	/// <summary>Host only: apply the restored keypad codes absolutely (replace, never merge).</summary>
	void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes);

	/// <summary>Host only: apply the restored geyser liquid types absolutely.</summary>
	void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers);
}
