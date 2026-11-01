// eval declaration: lockpress3
// declares: AcceptanceLockPress3
// serves: lockpick-pain-reaches-no-peer (getting the minigame into its stuck state on purpose)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceLockPress3
{
	public static int Frames;
	public static int Presses;
	public static bool Running;
	private static bool Pressed;

	public static string Start(float x, float y, int frames)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		camera.StartCoroutine(Pump(x, y, frames));
		return "started";
	}

	// `LockpingMinigame.clickingInside` is written ONLY on the frame `Input.GetKeyDown(attack)` fires, and the stuck accumulator lives inside that same
	// branch — so a single staged press decides the whole run on whatever the hand happened to be doing that frame (measured 2026-11-01: the hand settled at
	// magnitude 195..232, inside the 195..247 band, and the press still read inside=false). This pumps press/release pairs with the pointer parked on the
	// ring, so at least one edge lands while the hand is inside.
	public static IEnumerator Pump(float x, float y, int frames)
	{
		Running = true;
		var virtualInput = FindVirtualInput();
		var move = virtualInput == null ? null : virtualInput.GetMethod("MoveTo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		var pressKey = virtualInput == null ? null : virtualInput.GetMethod("PressKey", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		var releaseKey = virtualInput == null ? null : virtualInput.GetMethod("ReleaseKey", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (move == null || pressKey == null || releaseKey == null) { Running = false; yield break; }

		for (var frame = 0; frame < frames; frame++)
		{
			// The ring is centred on the minigame surface, which moves with the camera — a fixed screen point stops hitting the band as soon as the surface does
			// (measured 2026-11-01: the same 795,360 point answered magnitude 230 in one frame and 741 in another). Read the surface's own screen centre each frame.
			var surface = MinigameBase.main == null ? null : MinigameBase.main.spawnedMiniGame;
			if (surface != null) {
				var rect = surface.transform as RectTransform;
				var canvas = surface.GetComponentInParent<Canvas>();
				var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
				var centre = rect == null ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
				x = centre.x + 150f;
				y = centre.y;
			}
			move.Invoke(null, new object[] { x, y });

			// ONE call per edge. `PressKey` stamps `DownFrame = frameCount + 1`, so calling it on several frames in a row keeps moving the edge forward and it
			// never lands (measured 2026-11-01: a pump that pressed on consecutive frames read clickingInside=false on every attempt).
			// Press only while the HAND is inside the band the minigame checks (195..247), because `clickingInside` is written on the down-edge frame alone.
			var inside = Minigame.game != null && Minigame.game.handPos.magnitude > 200f && Minigame.game.handPos.magnitude < 242f;
			if (inside && !Pressed && (frame % 3) == 0)
			{
				pressKey.Invoke(null, new object[] { KeyCode.Mouse0 });
				Presses++;
			}
			else if ((frame % 4) == 2)
			{
				releaseKey.Invoke(null, new object[] { KeyCode.Mouse0 });
			}

			Frames++;
			yield return null;
		}

		Running = false;
	}

	private static System.Type FindVirtualInput()
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			System.Type found = null;
			try { found = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Acceptance.VirtualInput", false); } catch { found = null; }
			if (found != null) { return found; }
		}

		return null;
	}
}
