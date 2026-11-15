using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The session view of the native character picker. The game gates the two
/// later characters on <c>wonchar0</c>/<c>wonchar1</c> in
/// <c>PreRunScript</c>; a co-op session is not the single-player progression,
/// so this view makes every portrait pickable while a session is active and
/// refuses the session win's solo unlock write. The solo store itself is only
/// read, never written here.
/// </summary>
internal sealed class CharacterUnlockView(
	ISessionControl session,
	ILogger<CharacterUnlockView> log) : ICharacterUnlockPatchPort
{
	private bool? _lastInSession;
	private int _lastPickableMask = -1;

	public bool InSession => session.SessionActive
		&& session.Role is SessionRole.Host or SessionRole.Guest;

	/// <summary>Re-apply the view after the game's own per-frame portrait paint.</summary>
	public void ApplyPicker(PreRunScript pre)
	{
		if (pre?.charImages is null)
		{
			return;
		}

		var inSession = InSession;
		var mask = 0;
		for (var index = 0; index < pre.charImages.Count; index++)
		{
			var image = pre.charImages[index];
			if (image == null) // Unity object — ==
			{
				continue;
			}

			var soloUnlocked = SoloUnlocked(index);
			var pickable = CharacterUnlockPolicy.CanPick(inSession, soloUnlocked);
			if (pickable)
			{
				mask |= 1 << index;
			}

			var button = image.GetComponent<Button>();
			if (button != null) // Unity object — ==
			{
				button.interactable = pickable;
			}

			var tooltip = image.GetComponent<UITooltip>();
			if (tooltip != null) // Unity object — ==
			{
				tooltip.enabled = !pickable;
			}

			// The native paint runs just before this postfix and darkens a solo-locked
			// portrait. A co-op session may pick it, so repaint the same palette the game
			// uses for an unlocked portrait. Solo keeps the native lock untouched.
			if (inSession && !soloUnlocked)
			{
				ApplySessionPalette(pre, index, image);
			}
		}

		if (_lastInSession != inSession || _lastPickableMask != mask)
		{
			_lastInSession = inSession;
			_lastPickableMask = mask;
			log.LogInformation(
				"[Character] picker view: session {InSession}, pickable mask {Mask}, wonchar0 {WonChar0}, wonchar1 {WonChar1}.",
				inSession,
				mask,
				PlayerPrefs.GetInt("wonchar0", 0),
				PlayerPrefs.GetInt("wonchar1", 0));
		}
	}

	/// <summary>True when the native win write must be skipped: this is a session, not the solo progression.</summary>
	public bool SuppressWinWrite()
	{
		if (!InSession)
		{
			return false;
		}

		log.LogInformation("[Character] a session win does not write the solo character unlock.");
		return true;
	}

	/// <summary>Mirror the native unlocked palette for a portrait a session may pick but solo has not unlocked.</summary>
	private static void ApplySessionPalette(PreRunScript pre, int index, Image image)
	{
		if (pre.charInnerImages is null || index >= pre.charInnerImages.Count)
		{
			return;
		}

		var inner = pre.charInnerImages[index];
		if (inner == null) // Unity object — ==
		{
			return;
		}

		var dt = Time.unscaledDeltaTime * 5f;
		if (index == PreRunScript.characterSelected)
		{
			image.color = Color.Lerp(image.color, new Color(1f, 1f, 1f), dt);
			inner.color = Color.Lerp(inner.color, new Color(1f, 1f, 1f), dt);
			if (pre.charPodiums is not null && index < pre.charPodiums.Count && pre.charPodiums[index] != null)
			{
				var pulse = Mathf.PingPong(Time.unscaledTime * 0.5f, 0.5f);
				pre.charPodiums[index].color = Color.Lerp(
					pre.charPodiums[index].color, new Color(1f, 1f, 1f, pulse), dt);
			}

			return;
		}

		image.color = Color.Lerp(image.color, new Color(0.5f, 0.5f, 0.5f), dt);
		inner.color = Color.Lerp(inner.color, new Color(0.3f, 0.3f, 0.3f), dt);
		if (pre.charPodiums is not null && index < pre.charPodiums.Count && pre.charPodiums[index] != null)
		{
			pre.charPodiums[index].color = Color.Lerp(
				pre.charPodiums[index].color, new Color(1f, 1f, 1f, 0f), dt);
		}
	}

	private static bool SoloUnlocked(int index) =>
		index == 0 || PlayerPrefs.GetInt("wonchar" + (index - 1), 0) != 0;
}
