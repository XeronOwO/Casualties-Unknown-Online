using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The hollow debuff, cleared on the body that owns it — the piece the game's own <c>heal</c> command leaves
/// behind. The clear is the COMPONENT going away, not the icon:
/// <para>
/// <c>MoodleManager</c> draws the "hollow" moodle every frame from <c>body.mindWipe &amp;&amp;
/// body.mindWipe.active</c>, so destroying the icon achieves nothing, and the manager's only removal member
/// clears every moodle rather than one. The same component also forces effective happiness to zero from the
/// body's own getter, so the debuff a player feels is the component's presence as much as its active flag.
/// </para>
/// <para>
/// Two more things have to go with the component. The body CACHES it in <c>mindWipe</c>, and although both
/// readers test that handle with Unity's destroyed-object falsiness, clearing it closes the window before
/// <c>Object.Destroy</c> takes effect at the end of the frame. And the component INSTALLS a full-screen vignette
/// on the local canvas when its wipe starts or completes — nothing in the game ever destroys that object, so a
/// heal that removed only the component would leave a healed player still looking through the hollow overlay.
/// </para>
/// </summary>
internal static class HollowClear
{
	private const string VignetteName = "Mindwipe";

	/// <summary>True when the body carried the component and it is now gone.</summary>
	internal static bool Apply(Body body)
	{
		var mindwipe = body.GetComponent<MindwipeScript>();
		if (mindwipe == null) // Unity object — ==
		{
			return false;
		}

		Object.Destroy(mindwipe);
		body.mindWipe = null;
		RemoveVignettes();
		return true;
	}

	/// <summary>
	/// Removes the overlay the component installed. The prefab is loaded from <c>Special/MindwipeVignette</c> and
	/// parented to the player's main canvas, so its clone carries that name. The children are collected BEFORE
	/// any of them is destroyed, because destroying during the walk would renumber the rest.
	/// </summary>
	private static void RemoveVignettes()
	{
		var camera = PlayerCamera.main;
		var canvas = camera == null ? null : camera.mainCanvas; // Unity objects — ==
		if (canvas == null)
		{
			return;
		}

		var doomed = new List<GameObject>();
		var parent = canvas.transform;
		for (var i = 0; i < parent.childCount; i++)
		{
			var child = parent.GetChild(i);
			if (child != null && child.name.Contains(VignetteName))
			{
				doomed.Add(child.gameObject);
			}
		}

		foreach (var overlay in doomed)
		{
			Object.Destroy(overlay);
		}
	}
}
