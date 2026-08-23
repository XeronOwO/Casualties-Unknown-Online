using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Clone-inventory presentation for item components whose original script is
/// owner-local. A remote clone is a display proxy: it must show the owner's
/// state without running per-owner gameplay logic on the local machine.
/// GrapplingHook's fired/latched/pulling flags are captured by
/// <see cref="ItemStateCodec"/> and rendered here as sprite state; the
/// original GrapplingHook/WatchScript/AutoPump scripts are disabled on the
/// clone so their local-body side effects never run.
/// Pure conversion helpers are separated from the Unity writes so the state
/// decision has an L0 test face (the display path's test seam).
/// </summary>
internal static class RemoteItemPresentation
{
	internal static void Apply(Item item, CharacterItemMsg data)
	{
		var grapple = item.GetComponent<GrapplingHook>();
		if (grapple != null) // Unity object — ==
		{
			// The original script expects a live hook Rigidbody2D and the
			// owner's barrel/aim. A clone has neither, so running it would NRE
			// the moment the restored fired flag is true. Disable it and apply
			// the visual directly.
			grapple.enabled = false;

			var spriteRenderer = item.GetComponent<SpriteRenderer>();
			if (spriteRenderer != null) // Unity object — ==
			{
				spriteRenderer.sprite = IsGrapplingHookFired(data)
					? grapple.firedSprite
					: grapple.normSprite;
			}

			// The rope itself stays a local projection (no hook transform is
			// carried); never draw the original line with a null hook.
			var line = item.GetComponent<LineRenderer>();
			if (line != null) // Unity object — ==
			{
				line.enabled = false;
			}
		}

		// Owner-local behaviour: WatchScript's timers talk to the LOCAL body,
		// AutoPump's worn flag drives the LOCAL blood pressure. A clone copy
		// must not act on whoever is playing on this machine.
		var watch = item.GetComponent<WatchScript>();
		if (watch != null) // Unity object — ==
		{
			watch.enabled = false;
		}

		var pump = item.GetComponent<AutoPump>();
		if (pump != null) // Unity object — ==
		{
			pump.enabled = false;
		}

		// Dynamite's lit-fuse visual is the owner-local child sprite enabled by
		// the native use action. It rides the synthetic CustomItemBehaviour
		// fuse field, so the clone shows the fuse from the moment the owner
		// lights it instead of only appearing at the explosion. The fuse audio
		// is played once by the one-shot replay marker.
		ApplyDynamiteFuse(item, data);
	}

	/// <summary>
	/// Applies the dynamite lit-fuse child sprite + one-shot audio marker to a
	/// remote copy (clone inventory item or corrected world item). No scripts
	/// are disabled here — that is <see cref="Apply"/>'s clone-only concern.
	/// </summary>
	internal static void ApplyDynamiteFuse(Item item, CharacterItemMsg data)
	{
		if (item.id != CustomItemDataState.DynamiteItemId)
		{
			return;
		}

		var fuseLit = IsDynamiteFuseLit(data);

		var sr = item.transform.childCount > 0
			? item.transform.GetChild(0).GetComponent<SpriteRenderer>()
			: null;
		if (sr != null) // Unity object — ==
		{
			sr.enabled = fuseLit;
		}

		if (fuseLit && item.GetComponent<DynamiteFuseAudioReplay>() == null) // Unity object — ==
		{
			item.gameObject.AddComponent<DynamiteFuseAudioReplay>();
		}
	}

	/// <summary>Whether the snapshot says the owner's grappling hook is fired.
	/// The private bool rides the component-state wire as kind Bool.</summary>
	internal static bool IsGrapplingHookFired(CharacterItemMsg data) =>
		data.Components.Any(c => c.TypeName == nameof(GrapplingHook)
			&& c.Fields.Any(f => f.Name == "fired" && f.Kind == SaveableFieldKind.Bool && f.BoolValue));

	/// <summary>Whether the snapshot says the owner's dynamite has a lit fuse.
	/// The synthetic bool rides the CustomItemBehaviour component digest.</summary>
	internal static bool IsDynamiteFuseLit(CharacterItemMsg data) =>
		data.Components.Any(c => c.TypeName == nameof(CustomItemBehaviour)
			&& c.Fields.Any(f => f.Name == CustomItemDataState.DynamiteFuseFieldName
				&& f.Kind == SaveableFieldKind.Bool && f.BoolValue));
}
