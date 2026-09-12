using System;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using MapsterMapper;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure character-snapshot capture: body → wire <see cref="CharacterDataMsg"/>.
/// Split out of <see cref="CharacterDataSync"/> when that coordinator reached
/// the architecture line gate; the capture uses only the mapper and the
/// codec/component helpers, with no session or restore state.
/// </summary>
/// <param name="nativeSystem">
/// The live-scene port the native character fields are read through
/// (<see cref="CharacterNativeFields"/>). Null — the default for a caller that has
/// no game scene — captures a snapshot without them, which is exactly the state
/// the restore path NAMES rather than defaults.
/// </param>
internal static class CharacterDataCapture
{
	/// <param name="nativeFailure">Why the native character fields are not on the snapshot, or null when they are — the caller logs it, because "no native fields" alone cannot tell a missing camera from an old sender.</param>
	internal static CharacterDataMsg Capture(IMapper mapper, Body body, out string? nativeFailure, ICharacterNativeSystem? nativeSystem = null)
	{
		var health = mapper.Map<CharacterHealthMsg>(body);
		RemoteCharacterDisplayProjection.Capture(body, health);

		var msg = new CharacterDataMsg
		{
			Skills = mapper.Map<CharacterSkillsMsg>(body.skills),
			Health = health,
			// Wire encoding is handSlot + 1 (0 = none) — protobuf-net omits
			// 0-valued ints, and hand slot 0 is valid (see CharacterDataMsg.HandSlot).
			HandSlot = body.handSlot + 1,
			// The reconnect restore returns the character to its LEAVE spot, not
			// the fresh world's landing spot.
			Position = new NetVector2Msg(body.transform.position.x, body.transform.position.y),
			// The cross-player interaction service needs the live slot layout to
			// pick a concrete empty slot before a transfer.
			SlotCount = body.slots.Length,
		};

		// The native character fields the game's own save carried for this character
		// (S3.4b): read at the same instant as everything else, and read from the
		// LIVE scene rather than this body alone — they live on three objects, and a
		// read that met no camera describes no coherent instant (see
		// CharacterNativeFields.TryCapture). A null result is the NAMED gap the
		// restore reports; capturing nothing is never silently "zero", and the reason
		// is handed back so the caller's log names it.
		nativeFailure = null;
		if (nativeSystem is null)
		{
			nativeFailure = "no live-scene port was wired for this capture";
		}
		else
		{
			msg.NativeFields = CharacterNativeFields.TryCapture(nativeSystem, body, out nativeFailure);
		}

		// Limb has no Index field — Mapster maps the rest, the loop assigns it.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			limbMsg.IsHead = body.limbs[i].isHead;
			limbMsg.IsVital = body.limbs[i].isVital;
			limbMsg.Components = LimbComponentStateCodec.Capture(body.limbs[i]);
			limbMsg.ConnectedLimbIndices = [.. body.limbs[i].connectedLimbs
				.Select(l => Array.IndexOf(body.limbs, l))
				.Where(idx => idx >= 0)];
			limbMsg.DistanceToHeart = body.limbs[i].distanceToHeart;
			msg.Limbs.Add(limbMsg);
		}

		// Items: id ↔ ItemId is a rename, not a case variant — keep it manual.
		// Capture is recursive: container contents ride inside the parent item
		// (Contents), and [Saveable] component state (liquids, batteries, ammo,
		// …) rides along — the wire form of the official save's SavedItem +
		// component dictionaries (SaveSystem.SaveGame), so a restore is complete.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null || item.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==; display proxies are never authoritative local inventory
			{
				continue;
			}

			msg.Items.Add(ItemStateCodec.CaptureItem(item, slot));
		}

		// Wearables: items worn on body parts (mouth/hat/back/eyes… —
		// WearWearable parents them to the limb, Body.cs:1508), which are NOT
		// backpack slots — without this pass a worn item (e.g. a plastic chunk
		// held in the mouth) shows on the peer's clone as "still carried".
		// SlotIndex encodes the limb: -(limbIndex + 2) — negative, so it can
		// never collide with a real slot.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i].transform;
			for (var c = 0; c < limb.childCount; c++)
			{
				var worn = limb.GetChild(c).GetComponent<Item>();
				if (worn != null && worn.GetComponent<RemoteCloneRender>() == null) // Unity objects — ==
				{
					msg.Items.Add(ItemStateCodec.CaptureItem(worn, -(i + 2)));
				}
			}
		}

		return msg;
	}
}
