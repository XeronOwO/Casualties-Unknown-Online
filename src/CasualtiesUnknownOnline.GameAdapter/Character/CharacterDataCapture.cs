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
internal static class CharacterDataCapture
{
	internal static CharacterDataMsg Capture(IMapper mapper, Body body)
	{
		var health = mapper.Map<CharacterHealthMsg>(body);
		CloneFacePresentation.Capture(body, health);
		CharacterComponentSync.Capture(body, health);

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

		// Limb has no Index field — Mapster maps the rest, the loop assigns it.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			limbMsg.IsHead = body.limbs[i].isHead;
			limbMsg.IsVital = body.limbs[i].isVital;
			limbMsg.Components = LimbComponentStateCodec.Capture(body.limbs[i]);
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
