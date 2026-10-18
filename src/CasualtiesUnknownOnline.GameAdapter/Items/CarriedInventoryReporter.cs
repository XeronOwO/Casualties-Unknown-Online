using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The carried inventory with instance ids, captured on THIS client — both sides of a
/// session run this pump:
/// <list type="bullet">
/// <item>A GUEST: once the local world generation finished, every id-less carried item —
/// the starting supplies and the worn items — gets a self-assigned instance id (ids are
/// (counter &lt;&lt; 32) | SteamId, so the guest allocates without host round-trips; the
/// watermark keeps a crashed-and-rejoined counter from reusing ids the host still holds)
/// and the full list is reported. The host registers it in the guest's transfer table —
/// the authoritative record that makes the guest's use/slot reports arbitrate normally
/// (before this, a starting-supply item had no host-side record and the report's evidence
/// was broadcast instead).</item>
/// <item>A HOST: the same capture runs at the same world-entry edge and the RUNTIME
/// writes it into the host's own authority (<c>ItemService.SendCarriedInventory</c>'s
/// host branch), which is the only place its rows can come from — before this a host
/// item the player had not used, moved or dropped yet had neither a row nor an id, so
/// no data verb and no member's view could name it.</item>
/// </list>
///
/// <para>
/// The guest's report is ABSOLUTE and REPEATABLE (sync-coverage row I8): one frame can
/// be lost in the lazy-P2P swallow window without either side noticing, so this reporter
/// only opens the registration window at the edges that make one meaningful (the
/// generation-finished edge below, and the host's id-watermark grant on join/reconnect —
/// the session binding forwards it) and then RE-CAPTURES the current carried set whenever
/// the runtime's cadence says a report is due (<see cref="CarriedInventoryReportSchedule"/>).
/// Re-capturing rather than replaying the first frame is what keeps a repeat honest: an
/// item the guest destroyed, dropped or handed over is simply absent from the next capture.
/// The host's own registration is a one-shot for the same reason it cannot be lost: it is
/// a local write, so a repeat would only re-broadcast every item to every member.
/// </para>
/// </summary>
internal sealed class CarriedInventoryReporter(
	IItemControl items,
	ItemIdAllocator ids,
	ISessionControl session,
	LocalCharacterRestoreQueue restore)
{
	private readonly IItemControl _items = items;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ISessionControl _session = session;
	private readonly LocalCharacterRestoreQueue _restore = restore;

	private bool _generating; // last frame's IsGenerating — the falling edge is the generation-finished moment

	/// <summary>The last frame's host-session state — its rising edge is the host's second arming point: a world entered before the session existed (a solo world turned lobby) never sees a generation edge under that session.</summary>
	private bool _hostSessionActive;

	/// <summary>Pump: detect the generation-finished falling edge and open the
	/// registration opportunity — the report itself goes out on the next frame, when
	/// the runtime's cadence asks for it (that extra frame keeps the enumeration
	/// immune to start-order jitter: corpse loot spawns in CorpseScript.Start,
	/// the same rationale as GeneratedItemAuthority). After the dense window
	/// closes, this pump keeps re-asserting the guest's registration on the steady
	/// cadence, which is how an id self-assigned later (a crafted product)
	/// converges too. The host's own registration is spent by its first due frame.</summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (generating)
		{
			_generating = true;
			return;
		}

		if (_generating)
		{
			_generating = false;
			_items.ArmCarriedInventoryRegistration("the local generation finished");
			return;
		}

		var hostSessionActive = _session.Role == SessionRole.Host && _session.SessionActive;
		if (hostSessionActive && !_hostSessionActive)
		{
			_items.ArmCarriedInventoryRegistration("the host's session became active on the world it is already in");
		}

		_hostSessionActive = hostSessionActive;

		if (_items.IsCarriedInventoryRegistrationDue())
		{
			// No body is NOT an empty carried set: a statement made from a body that cannot
			// be read yet would tell the host "this member carries nothing", and the host
			// treats that as the statement it judges (a report for an item the statement did
			// not carry is then refused). The step is left unspent instead, so the next frame
			// tries again — the window is still open.
			if (PlayerCamera.main?.body == null) // Unity object — ==
			{
				return;
			}

			if (_restore.HasPending || _restore.WipePending)
			{
				// A local character restore is in flight, so this body is not what the player ends
				// up carrying: the respawn layer gets its body a frame after the generation edge and
				// its items two frames later through the wipe/apply, so a capture taken now would
				// state an empty set (and, for the host, spend the ONE registration it will not get
				// again). The guest heals that with its dense window; the host must not spend it.
				return;
			}

			_items.SendCarriedInventory(CaptureItems());
		}
	}

	private List<CharacterItemMsg> CaptureItems()
	{
		var items = new List<CharacterItemMsg>();
		var body = PlayerCamera.main?.body; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return items; // nothing to capture yet — the window still spends the step
		}

		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null || item.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==; display proxies are never authoritative guest items
			{
				continue;
			}

			// EVERY authoritative item of the body belongs to the absolute set, including one
			// that already carries an instance id — a snapshot id the host knows, an id an
			// earlier report of THIS reporter stamped, or a product CraftingSync/ContainerItemSync
			// stamped. EnsureId returns an existing id unchanged, so a repeat states the same
			// set, and the host drops the ids it already has. Skipping bound items is exactly
			// how the re-report goes inert: the first capture stamps an id on every item it
			// reports, so every later capture would come back empty and send nothing at all.
			if (_ids.EnsureId(item) == 0)
			{
				continue; // no id to state: still generating, or a remote kill zeroed the id
			}

			EnsureContentsAreNamed(item);
			items.Add(ItemStateCodec.CaptureItem(item, slot));
		}

		// Worn items (limb children — the wear encoding, ItemStateCodec.SlotOf).
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i].transform;
			for (var c = 0; c < limb.childCount; c++)
			{
				var worn = limb.GetChild(c).GetComponent<Item>();
				if (worn == null || worn.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==; display proxies are never authoritative guest items
				{
					continue;
				}

				if (_ids.EnsureId(worn) == 0)
				{
					continue; // no id to state (same rule as the slot loop)
				}

				EnsureContentsAreNamed(worn);
				items.Add(ItemStateCodec.CaptureItem(worn, -(i + 2)));
			}
		}

		return items;
	}

	/// <summary>
	/// A carried container's CONTENTS are part of the statement's fact set, so every one of them has to be
	/// nameable: the capture walks the tree and the host adopts only the children an instance id names
	/// (<c>ItemArbitration.AdoptCarriedContents</c> skips id 0), which before this left anything that had
	/// sat inside a carried container since it was generated unnameable — a report about it (the trader
	/// give above all, whose credit the host judges against the item's own row) named no instance at all.
	/// Stamped here, at the one place the statement is captured, so the id exists before any later report
	/// needs it; an existing id is returned unchanged, so a repeat states the same set.
	/// </summary>
	private void EnsureContentsAreNamed(Item item)
	{
		var container = item.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return;
		}

		for (var i = 0; i < container.transform.childCount; i++)
		{
			var child = container.transform.GetChild(i).GetComponent<Item>();
			if (child == null || child.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			if (_ids.EnsureId(child) == 0)
			{
				continue;
			}

			EnsureContentsAreNamed(child);
		}
	}
}
