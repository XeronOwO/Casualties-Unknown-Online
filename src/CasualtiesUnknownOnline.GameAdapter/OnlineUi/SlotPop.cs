using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The little pop a slot plays when a carried item lands in it. The game's own container drop
/// scales the hit slot to 1.5x and eases it back (PlayerCamera.cs:1561, :1871); the remote
/// backpack's drop has no native branch to run, so the same numbers are replayed here.
/// </summary>
internal sealed class SlotPop : MonoBehaviour
{
	private const float Peak = 1.5f;

	/// <summary>How fast the slot returns to its normal size — the game's own rate (PlayerCamera.cs:1871).</summary>
	private const float RestorePerSecond = 8f;

	/// <summary>Slots this client has already drawn once — a first render is not a landing.</summary>
	private static readonly HashSet<Transform> Seen = [];

	/// <summary>Pop only when the slot was already showing something — i.e. an item just landed.</summary>
	internal static void PlayOnLanding(Transform slot)
	{
		if (slot == null) // Unity object — ==
		{
			return;
		}

		if (Seen.Add(slot))
		{
			return;
		}

		Play(slot);
	}

	/// <summary>Start the pop on the slot that just received an item.</summary>
	internal static void Play(Transform slot)
	{
		if (slot == null) // Unity object — ==
		{
			return;
		}

		slot.localScale = Vector3.one * Peak;
		if (slot.GetComponent<SlotPop>() == null) // Unity object — ==
		{
			slot.gameObject.AddComponent<SlotPop>();
		}
	}

	private void Update()
	{
		transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.unscaledDeltaTime * RestorePerSecond);
		if (Mathf.Approximately(transform.localScale.x, 1f))
		{
			transform.localScale = Vector3.one;
			Destroy(this);
		}
	}
}
