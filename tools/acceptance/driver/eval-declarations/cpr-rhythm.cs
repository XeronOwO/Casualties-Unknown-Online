// eval declaration: cpr-rhythm
// declares: AcceptanceCprRhythm
// serves: remote-medical-cpr's `Good` rhythm residual - no press the driver could produce landed inside the band,
//         because one driver call is >= 1.0 s apart and same-frame presses measure ~0.01 s.
//
// Why a declaration is the only shape that answers it: the control measures the gap from REAL game time, so the presses
// must be separated by FRAMES the game itself runs. A recipe cannot wait between its own statements without stalling the
// main thread (which freezes that clock), so this declaration owns a coroutine: the starter returns immediately, the
// coroutine presses once per interval on successive frames, and the operator's own log lines carry the verdicts. Nothing
// here forces a verdict - it performs the same `onClick` a player's click performs, only timed by the game's clock.
//
// The evaluator compiles one input at a time and refuses a trailing expression, so this file is a declaration of its own:
// `drive-in-process.ps1 -Action declare -Declare cpr-rhythm` sends it once per client, and the `cpr-rhythm` recipe then
// starts it by name.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class AcceptanceCprRhythm
{
	public static string Start(int count, float intervalSeconds, string control)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		camera.StartCoroutine(Press(count, intervalSeconds, control));
		return "started";
	}

	public static IEnumerator Press(int count, float intervalSeconds, string control)
	{
		var wanted = control == "compress" ? "CUO CPR Control" : "CUO CPR Breath";
		for (var n = 0; n < count; n++)
		{
			var view = WoundView.view;
			if (view != null && view.specialUseButton != null)
			{
				var parent = view.specialUseButton.transform.parent;
				for (var i = 0; i < parent.childCount; i++)
				{
					var child = parent.GetChild(i);
					if (child.gameObject.name != wanted) { continue; }
					var button = child.GetComponent<Button>();
					if (button != null && button.gameObject.activeInHierarchy && button.interactable)
					{
						button.onClick.Invoke();
					}
					break;
				}
			}
			yield return new WaitForSeconds(intervalSeconds);
		}
	}
}
