// eval declaration: shrapnel-break
// declares: AcceptanceShrapnelBreak2
// serves: remote-medical-stage-2-shrapnel-multiplayer row 7 (all pieces removed), which needs actions INSIDE the minigame's
//         lifetime: a driver call is >= 0.4 s apart and this session's minigame is gone by then (measured: start answered
//         started=true, shrapnel=3, and a break 0.4 s later answered currentMinigame is null).
//
// Why a declaration rather than a recipe: the pieces must be grabbed, reported and broken across FRAMES the game itself runs, and
// a recipe's statements cannot wait without stalling that clock. The starter returns at once; the coroutine waits for the
// minigame to appear, then per piece posts the ownership report a grab must send, sets the held slot, waits a frame, and calls
// the native BreakGrasp(). Everything is reached by NAME through reflection (the CUO assemblies are not compile-time references
// here), which is the same door grip-slip-drive proved works.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceShrapnelBreak2
{
	const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

	/// <summary>Where the coroutine stopped, or how far it got: the reading a batch needs when nothing broke.</summary>
	public static string Trace = "idle";

	public static string Start(int maxPieces)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		Trace = "started";
		camera.StartCoroutine(BreakPieces(maxPieces));
		return "started";
	}

	public static IEnumerator BreakPieces(int maxPieces)
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		System.Type updateType = null;
		System.Type handlerType = null;
		for (var i = 0; i < assemblies.Length; i++)
		{
			if (updateType == null) { try { updateType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.ShrapnelPieceUpdate", false); } catch { updateType = null; } }
			if (handlerType == null) { try { handlerType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalOperationHandler", false); } catch { handlerType = null; } }
		}

		if (updateType == null || handlerType == null) { Trace = "no-types:" + (updateType == null) + "/" + (handlerType == null); yield break; }
		var report = handlerType.GetMethod("ReportShrapnelUpdate", Flags);
		if (report == null) { Trace = "no-report-method"; yield break; }
		var objectsField = typeof(ShrapnelMinigame).GetField("objects", Flags);
		var heldField = typeof(ShrapnelMinigame).GetField("currentlyHeld", Flags);
		if (objectsField == null || heldField == null) { Trace = "no-minigame-fields"; yield break; }
		var pieceIndexProperty = updateType.GetProperty("PieceIndex");
		var grabbedProperty = updateType.GetProperty("Grabbed");
		var ownershipProperty = updateType.GetProperty("OwnershipChange");
		if (pieceIndexProperty == null || grabbedProperty == null || ownershipProperty == null) { Trace = "no-grab-properties"; yield break; }

		for (var piece = 0; piece < maxPieces; piece++)
		{
			// Wait for the session's minigame to exist: the start and the first tick are separate frames.
			for (var wait = 0; wait < 180; wait++)
			{
				var probe = MinigameBase.main != null ? MinigameBase.main.currentMinigame as ShrapnelMinigame : null;
				if (probe != null) { break; }
				yield return null;
			}

			var minigame = MinigameBase.main != null ? MinigameBase.main.currentMinigame as ShrapnelMinigame : null;
			if (minigame == null) { Trace = "no-minigame-piece-" + piece; yield break; }

			var objects = objectsField.GetValue(minigame) as List<RectTransform>;
			if (objects == null || piece >= objects.Count) { Trace = "no-objects-piece-" + piece + ":count=" + (objects == null ? -1 : objects.Count); yield break; }

			var grab = System.Activator.CreateInstance(updateType);
			pieceIndexProperty.SetValue(grab, piece, null);
			grabbedProperty.SetValue(grab, true, null);
			ownershipProperty.SetValue(grab, true, null);
			report.Invoke(null, new object[] { grab });
			heldField.SetValue(minigame, objects[piece]);

			// The patch reads the held slot on its own frame; breaking in the same frame would break nothing.
			yield return null;
			yield return null;

			var current = MinigameBase.main != null ? MinigameBase.main.currentMinigame as ShrapnelMinigame : null;
			if (current == null) { Trace = "session-gone-after-grab-" + piece; yield break; }
			current.BreakGrasp();
			Trace = "broke-piece-" + piece;
			yield return null;
			yield return null;
		}
	}
}
