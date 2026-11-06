// eval declaration: shrapnel-break6
// declares: AcceptanceShrapnelBreak6
// serves: remote-medical-stage-2-shrapnel-multiplayer row 7 (all pieces removed; the session ends by itself).
//
// Why a sixth cut: shrapnel-break5 lifted every piece above the game's y=35 line and the display limb read shrapnel 0,
// but the session still stayed open. The host's own end condition is `ShrapnelOperationSessionService`:
// a report whose `Y >= RemoveThresholdY` (35) marks the piece removed, and when the last piece is removed
// `AllPiecesRemoved` terminates the session `Completed`. break5 only sent grab reports, never a position, so the host never
// marked a piece removed. This cut sends, after each break-and-lift, one ordinary position report with Y = 60 — the exact
// reading the host uses to count the piece out.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceShrapnelBreak6
{
	const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

	/// <summary>Where the coroutine stopped, or how far it got: the reading a batch needs when nothing broke.</summary>
	public static string Trace = "idle";

	/// <summary>Human-readable stage list for a recipe that cannot wait; the log carries the same lines.</summary>
	public static readonly List<string> Steps = new List<string>();

	sealed class Handles
	{
		public System.Type UpdateType;
		public MethodInfo Report;
		public FieldInfo Objects;
		public FieldInfo Held;
		public PropertyInfo PieceIndex;
		public PropertyInfo Grabbed;
		public PropertyInfo Ownership;
		public PropertyInfo X;
		public PropertyInfo Y;
		public string Error;
	}

	static void Say(string line)
	{
		Trace = line;
		Steps.Add(line);
		Debug.Log("[ShrapnelBreak] " + line);
	}

	public static string Start(int maxPieces)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { Say("no-camera"); return "no-camera"; }
		Say("start:requested:" + maxPieces);
		try
		{
			camera.StartCoroutine(BreakPieces(maxPieces));
			Say("start:coroutine-accepted");
			return "started";
		}
		catch (System.Exception ex)
		{
			Say("start:threw:" + ex.GetType().Name + ":" + ex.Message);
			return "threw";
		}
	}

	static Handles Prepare()
	{
		var handles = new Handles();
		try
		{
			handles.UpdateType = FindType("CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.ShrapnelPieceUpdate");
			var handlerType = FindType("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalOperationHandler");
			if (handles.UpdateType == null || handlerType == null) { handles.Error = "no-types"; return handles; }
			handles.Report = handlerType.GetMethod("ReportShrapnelUpdate", Flags);
			handles.Objects = typeof(ShrapnelMinigame).GetField("objects", Flags);
			handles.Held = typeof(ShrapnelMinigame).GetField("currentlyHeld", Flags);
			if (handles.Report == null || handles.Objects == null || handles.Held == null) { handles.Error = "no-members"; return handles; }
			handles.PieceIndex = handles.UpdateType.GetProperty("PieceIndex");
			handles.Grabbed = handles.UpdateType.GetProperty("Grabbed");
			handles.Ownership = handles.UpdateType.GetProperty("OwnershipChange");
			handles.X = handles.UpdateType.GetProperty("X");
			handles.Y = handles.UpdateType.GetProperty("Y");
			if (handles.PieceIndex == null || handles.Grabbed == null || handles.Ownership == null || handles.X == null || handles.Y == null) { handles.Error = "no-props"; }
			return handles;
		}
		catch (System.Exception ex)
		{
			handles.Error = "prepare-threw:" + ex.GetType().Name + ":" + ex.Message;
			return handles;
		}
	}

	static List<RectTransform> Pieces(Handles handles, ShrapnelMinigame minigame)
	{
		try { return handles.Objects.GetValue(minigame) as List<RectTransform>; }
		catch { return null; }
	}

	static string Grab(Handles handles, ShrapnelMinigame minigame, RectTransform piece, int index)
	{
		try
		{
			var grab = System.Activator.CreateInstance(handles.UpdateType);
			handles.PieceIndex.SetValue(grab, index, null);
			handles.Grabbed.SetValue(grab, true, null);
			handles.Ownership.SetValue(grab, true, null);
			handles.Report.Invoke(null, new object[] { grab });
			handles.Held.SetValue(minigame, piece);
			return null;
		}
		catch (System.Exception ex)
		{
			return "grab-threw-piece-" + index + ":" + ex.GetType().Name + ":" + ex.Message;
		}
	}

	static string BreakAndLift(ShrapnelMinigame minigame, RectTransform piece, int index)
	{
		try
		{
			minigame.BreakGrasp();
			// The native count reads anchoredPosition.y < 35; anything above that line is "out of the limb".
			piece.anchoredPosition = new Vector2(piece.anchoredPosition.x, 60f);
			return null;
		}
		catch (System.Exception ex)
		{
			return "break-threw-piece-" + index + ":" + ex.GetType().Name + ":" + ex.Message;
		}
	}

	/// <summary>The host's removal reading: an ordinary position report whose Y is at or above the 35 line.</summary>
	static string Remove(Handles handles, int index, float x)
	{
		try
		{
			var move = System.Activator.CreateInstance(handles.UpdateType);
			handles.PieceIndex.SetValue(move, index, null);
			handles.X.SetValue(move, x, null);
			handles.Y.SetValue(move, 60f, null);
			handles.Report.Invoke(null, new object[] { move });
			return null;
		}
		catch (System.Exception ex)
		{
			return "remove-threw-piece-" + index + ":" + ex.GetType().Name + ":" + ex.Message;
		}
	}

	public static IEnumerator BreakPieces(int maxPieces)
	{
		Say("tick:0");
		yield return null;
		Say("tick:1");
		var handles = Prepare();
		Say("prepare:" + (handles.Error ?? "ok"));
		if (handles.Error != null) { yield break; }

		for (var piece = 0; piece < maxPieces; piece++)
		{
			var waited = 0;
			ShrapnelMinigame minigame = null;
			for (; waited < 180; waited++)
			{
				minigame = MinigameBase.main != null ? MinigameBase.main.currentMinigame as ShrapnelMinigame : null;
				if (minigame != null) { break; }
				yield return null;
			}
			Say("piece:" + piece + ":wait=" + waited + ":current=" + Describe(MinigameBase.main));
			if (minigame == null) { Say("stop:no-minigame-piece-" + piece); yield break; }

			var pieces = Pieces(handles, minigame);
			if (pieces == null || piece >= pieces.Count) { Say("stop:no-piece-" + piece + ":count=" + (pieces == null ? "null" : pieces.Count.ToString())); yield break; }

			var error = Grab(handles, minigame, pieces[piece], piece);
			Say("piece:" + piece + ":grab=" + (error ?? "ok"));
			if (error != null) { yield break; }

			yield return null;
			yield return null;

			var current = MinigameBase.main != null ? MinigameBase.main.currentMinigame as ShrapnelMinigame : null;
			if (current == null) { Say("stop:gone-after-grab-" + piece); yield break; }
			var broke = BreakAndLift(current, pieces[piece], piece);
			Say("piece:" + piece + ":break=" + (broke ?? "ok"));
			if (broke != null) { yield break; }

			var removed = Remove(handles, piece, pieces[piece].anchoredPosition.x);
			Say("piece:" + piece + ":remove=" + (removed ?? "ok"));
			if (removed != null) { yield break; }

			yield return null;
			yield return null;
		}

		Say("done");
	}

	static string Describe(MinigameBase basis)
	{
		if (basis == null) { return "no-base"; }
		var active = basis.currentMinigame;
		if (active == null) { return "none"; }
		return active.GetType().Name;
	}

	static System.Type FindType(string name)
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			System.Type found = null;
			try { found = assemblies[i].GetType(name, false); } catch { found = null; }
			if (found != null) { return found; }
		}
		return null;
	}
}
