// eval declaration: bandage-ring
// declares: AcceptanceBandageRing
// serves: remote-medical-treatment-operations — the completing pass: eighteen wraps inside ONE operation, driven from the client
//         itself so the cadence stops being a process round trip.
//
// Why inside the client: `BandageMinigame.PhysicsUpdate` accepts a step only under 20 degrees and advances its own angle between
// frames the game runs, and a driver that is one process call per step takes ~0.6 s per step — eighteen wraps need ~90 s of it,
// during which a tool round trip outlives the minigame (measured 2026-11-01: batches stalled at 8-17 wraps).
//
// How it moves the hand: through the acceptance channel's OWN staged pointer, by reflection — `VirtualInput.MoveTo(x, y)` and
// `VirtualInput.PressKey(KeyCode.Mouse0)`, the same calls the `virtual-input` recipe makes. Nothing here writes the minigame's
// fields: the game computes the hand from `Input.mousePosition` and the attack bind every frame, so a staged pointer is the only
// honest input. The counters are static fields a recipe reads back.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceBandageRing
{
	public static int Frames;
	public static int Moves;
	public static int Actions;
	public static bool Running;
	public static string Kind = "";

	public static string Start(int frames)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		camera.StartCoroutine(Ring(frames));
		return "started";
	}

	public static IEnumerator Ring(int frames)
	{
		Running = true;
		var virtualInput = FindVirtualInput();
		if (virtualInput == null)
		{
			Kind = "no-VirtualInput";
			Running = false;
			yield break;
		}

		var move = virtualInput.GetMethod("MoveTo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		var pressKey = virtualInput.GetMethod("PressKey", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (move == null || pressKey == null)
		{
			Kind = "no-move";
			Running = false;
			yield break;
		}

		var held = false;
		for (var frame = 0; frame < frames; frame++)
		{
			Frames++;
			var main = MinigameBase.main;
			if (main == null) { yield return null; continue; }

			var current = main.currentMinigame as BandageMinigame;
			if (current == null) { yield return null; continue; }

			Kind = "BandageMinigame";
			if (!held)
			{
				// The hand must be on the ring BEFORE the button goes down: `holdingBandage` is computed on the very frame the game sees
				// the press, from the pointer's direction against `bandageAngle`.
				move.Invoke(null, new object[] { 640f, 510f });
				pressKey.Invoke(null, new object[] { KeyCode.Mouse0 });
				held = true;
				yield return null;
				continue;
			}

			// Lead the minigame's own angle by a few degrees: enough for `num2` to pass its 20-degree action step after a couple of
			// frames, small enough that `num` stays under 20 (the guard that discards a bigger jump).
			var angle = current.bandageAngle + 7f;
			var radians = angle * 0.017453292f;
			move.Invoke(null, new object[] { 640f + 150f * Mathf.Sin(radians), 360f + 150f * Mathf.Cos(radians) });
			Moves++;
			Actions = current.lastActionAngle / 20;
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
