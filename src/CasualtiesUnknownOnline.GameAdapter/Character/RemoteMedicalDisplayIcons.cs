using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The wound-view icons a display-only body shows for the limb components that
/// carry one (a splint, a tourniquet).
///
/// <para>
/// The game draws these from inside the components themselves: <c>SplintLimb.Start</c>
/// and <c>TourniquetScript.Start</c> call <c>WoundView.AddImageToLimb</c>. A
/// display copy must not run that (see <see cref="RemoteMedicalDisplayBody"/>),
/// so its components are inert state holders and CUO draws the same icons from
/// the state instead — through the game's own API, so the panel keeps looking
/// native rather than gaining a parallel overlay. Without this a teammate's
/// splint is invisible in the panel while the same limb reads `splinted` in the
/// facts, which is the one surface where the operator has to see it: the panel's
/// own special action dispatches on the displayed limb's splint/tourniquet state.
/// </para>
///
/// <para>
/// Two lifetimes, both owned here: the state going away destroys the icon, and the
/// icon's own removal predicate — <c>x == null</c> — destroys it when its limb is
/// destroyed. The game's own predicates dereference the limb
/// (<c>!x.GetComponent&lt;SplintLimb&gt;()</c>), which is what turned a rebuilt
/// display body into a per-frame exception storm in batch <c>20261011-b</c>; a
/// predicate that only asks whether the limb is still alive cannot do that.
/// </para>
/// </summary>
internal static class RemoteMedicalDisplayIcons
{
	private const string SplintSprite = "Special/splintIcon";
	private const string TourniquetSprite = "Special/tourniquetIcon";

	private static readonly List<Entry> Entries = [];

	/// <summary>
	/// Make one display limb's icons match the state its components carry. Called
	/// after the limb state is applied, so the components are already the truth
	/// for this frame.
	/// </summary>
	internal static void Sync(Body display, Limb limb)
	{
		SyncOne(display, limb, nameof(SplintLimb), SplintSprite, limb.GetComponent<SplintLimb>() != null);
		SyncOne(display, limb, nameof(TourniquetScript), TourniquetSprite, limb.GetComponent<TourniquetScript>() != null);
	}

	/// <summary>
	/// Drop every icon this class owns. The focus's own teardown destroys the
	/// display body it drew them for, and an icon left behind is a panel child
	/// pointing at a limb that no longer exists.
	/// </summary>
	internal static void Clear()
	{
		foreach (var entry in Entries)
		{
			if (entry.Icon != null) // Unity object — ==
			{
				Object.Destroy(entry.Icon);
			}
		}

		Entries.Clear();
	}

	private static void SyncOne(Body display, Limb limb, string typeName, string spritePath, bool present)
	{
		var entry = Find(limb, typeName);
		if (present)
		{
			if (entry is not null)
			{
				return;
			}

			if (WoundView.view == null) // Unity object — ==
			{
				return;
			}

			var sprite = Resources.Load<Sprite>(spritePath);
			if (sprite == null) // Unity object — ==
			{
				return;
			}

			// The game's own lookup is `Array.IndexOf(WoundView.view.body.limbs, limb)`: an icon asked for
			// a limb the panel is not currently showing indexes out of range. The focus's own patch keeps
			// the panel on the display copy and this re-asserts it at the instant the icon is added.
			WoundView.view.body = display;
			var icon = WoundView.view.AddImageToLimb(limb, sprite, false, (Limb x) => x == null);
			Entries.Add(new Entry(limb, typeName, icon.gameObject));
			return;
		}

		if (entry is not null)
		{
			if (entry.Icon != null) // Unity object — ==
			{
				Object.Destroy(entry.Icon);
			}

			Entries.Remove(entry);
		}
	}

	/// <summary>The tracked icon for one limb and component type, pruned of icons the game's own predicate already destroyed.</summary>
	private static Entry? Find(Limb limb, string typeName)
	{
		for (var i = Entries.Count - 1; i >= 0; i--)
		{
			var entry = Entries[i];
			if (entry.Icon == null) // Unity object — ==
			{
				Entries.RemoveAt(i);
				continue;
			}

			if (entry.Limb == limb && entry.TypeName == typeName)
			{
				return entry;
			}
		}

		return null;
	}

	private sealed class Entry(Limb limb, string typeName, GameObject icon)
	{
		internal Limb Limb { get; } = limb;

		internal string TypeName { get; } = typeName;

		internal GameObject Icon { get; } = icon;
	}
}
