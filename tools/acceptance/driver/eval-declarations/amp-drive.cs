// eval declaration: amp-drive
// declares: AcceptanceAmpDrive
// serves: suppressed-native-call-sounds-stay-unheard row 4b (the gore sound of an amputation that completes)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceAmpDrive
{
	public static int Frames;
	public static bool Running;

	public static string Start(int frames, float amplitude)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		camera.StartCoroutine(Drive(frames, amplitude));
		return "started";
	}

	// Per-frame alternation: `AmputationMinigame.PhysicsUpdate` adds `|handVelocity.x| * 5E-05f` to `cutProgress` on every frame it is cutting, and a
	// staged move that happens once per batch call leaves the hand still on the frames between calls, so the progress never accumulates. The pointer is
	// moved through the acceptance channel's own `VirtualInput.MoveTo`, reached by reflection the way `bandage-ring2` does.
	public static IEnumerator Drive(int frames, float amplitude)
	{
		Running = true;
		var virtualInput = FindVirtualInput();
		var move = virtualInput == null ? null : virtualInput.GetMethod("MoveTo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (move == null) { Running = false; yield break; }

		var centreX = Screen.width * 0.5f;
		// `AmputationMinigame.cutting` is true only while `handPos.y <= maxHandPos + 10`, and `maxHandPos` starts at 120 and lerps to -380 with progress,
		// so the sawing motion has to happen near the BOTTOM of the minigame surface: at the screen centre the cut never starts (measured 2026-11-01).
		var centreY = 100f;
		for (var frame = 0; frame < frames; frame++)
		{
			move.Invoke(null, new object[] { frame % 2 == 0 ? centreX + amplitude : centreX - amplitude, centreY });
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
