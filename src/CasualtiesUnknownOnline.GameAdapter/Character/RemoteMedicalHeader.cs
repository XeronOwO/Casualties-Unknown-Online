using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using static CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalHeaderState;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The identity header of the native wound window while a remote medical focus is open: the
/// adapter half that performs the writes, over the rule in <see cref="RemoteMedicalHeaderState"/>.
///
/// <para>
/// The header names the CHARACTER the panel shows (<c>"&lt;height&gt;cm | &lt;age&gt;y | #&lt;id&gt;"</c>
/// and <c>"UNI-HEALTH v2.0&lt;ver&gt;"</c>, both written only by <c>WoundView.SetCharDetails</c>,
/// <c>WoundView.cs:54-68</c>), and nothing in the native per-frame pass touches it again. The
/// viewed player's four values arrive on the character snapshot the remote focus already reads —
/// <c>CharacterDataMsg.NativeFields.CharacterInfo</c>, the same sub-message the reconnect restore
/// uses (decision 171) — read off the OWNER's own <c>WoundView.view.cInfo</c> at its 1 Hz report.
/// So the panel takes them over through the game's own entry point, and gives the viewer's own
/// values back when the focus ends; no text is drawn by CUO.
/// </para>
///
/// <para>
/// The viewer's own four values are not read from the panel while the focus holds it — the panel
/// is no longer their carrier then — which is why the borrow also answers
/// <see cref="CharacterNativeFields.LiveSystem"/>: the local character's 1 Hz snapshot must keep
/// reporting the viewer's identity, not the one the panel currently displays (otherwise viewing a
/// peer would report the peer's identity as this client's own, and it would spread to every other
/// peer's view of this player). The same seam takes the local restore's own write
/// (<see cref="TryAdoptOwn"/>) so a restore that lands mid-focus updates the remembered copy and
/// never flashes the viewer's identity onto the borrowed panel.
/// </para>
/// </summary>
internal static class RemoteMedicalHeader
{
	private static readonly RemoteMedicalHeaderState State = new();

	/// <summary>
	/// The focus's logger. The coordinator hands it on every refresh; the close paths have no
	/// logger of their own (the native panel toggle patch, the remote backpack open) but still owe
	/// the restore its line, so the focus keeps the one it was pumped with.
	/// </summary>
	private static ILogger? _log;

	/// <summary>
	/// One frame of the open focus: show the viewed player's identity, blank the labels when the
	/// snapshot carries none, or do nothing when the panel already shows it. Idempotent — the
	/// native write happens only when the values change, so the per-frame call costs a comparison.
	/// </summary>
	internal static void ShowViewedIdentity(IReadOnlyList<int>? viewed, ILogger log)
	{
		var view = WoundView.view;
		if (view == null) // Unity object — ==
		{
			return;
		}

		_log = log;
		var write = State.Refresh(view.cInfo, viewed);
		if (write.Kind == WriteKind.Show)
		{
			Apply(view, write.Values);
			log.LogInformation(
				"[MedicalView] panel identity header shows the viewed player's own values ({Height}cm | {Age}y | #{Id} | UNI-HEALTH v2.0{Version}).",
				write.Values[0], write.Values[1], write.Values[2], write.Values[3]);
		}
		else if (write.Kind == WriteKind.Blank)
		{
			Blank(view);
			log.LogWarning(
				"[MedicalView] the viewed player's snapshot carries no character identity; the panel's identity header is blank rather than showing the viewer's own values.");
		}
	}

	/// <summary>
	/// The viewer's own four values, whichever object currently carries them — the remembered copy
	/// while a focus holds the panel, the panel itself otherwise. The local character snapshot reads
	/// this instead of <c>WoundView.view.cInfo</c> directly.
	/// </summary>
	internal static int[] OwnIdentity(int[] panelInfo) => State.OwnIdentity(panelInfo);

	/// <summary>
	/// The viewer's own identity was re-authored while the panel is borrowed (the local character
	/// restore): remember it for the close and report true, so the caller leaves the borrowed panel
	/// alone. False = nothing is borrowed, so the caller writes the panel as usual.
	/// </summary>
	internal static bool TryAdoptOwn(int[] panelInfo, IReadOnlyList<int> details)
	{
		if (!State.TryAdoptOwn(panelInfo, details))
		{
			return false;
		}

		_log?.LogInformation(
			"[MedicalView] the viewer's own character identity was re-authored while the remote panel is open ({Height}cm | {Age}y | #{Id} | UNI-HEALTH v2.0{Version}); the panel keeps the viewed player's header and the close writes the new values.",
			details[0], details[1], details[2], details[3]);
		return true;
	}

	/// <summary>
	/// A native reader is about to read the identity header and will assume it describes the LOCAL
	/// character. The game's own save is the one such reader — `SaveSystem.SaveGame` copies the four
	/// <c>cHeight</c>/<c>cAge</c>/<c>cId</c>/<c>cVer</c> values straight out of
	/// <c>WoundView.view.cInfo</c> (`SaveSystem.cs:172-175`) — so while a focus holds the panel the
	/// window puts the viewer's own values there for the duration of the read and re-asserts what the
	/// focus displays afterwards. Null when no focus holds the panel: nothing to override.
	/// </summary>
	internal static IDisposable? EnterOwnIdentityCapture()
	{
		var view = WoundView.view;
		if (view == null) // Unity object — ==
		{
			return null;
		}

		var own = State.OwnIdentityForCapture(view.cInfo);
		if (own is null)
		{
			return null;
		}

		Apply(view, own);
		_log?.LogInformation(
			"[MedicalView] a native reader of the identity header ran while the remote panel is open: it saw the viewer's own values ({Height}cm | {Age}y | #{Id} | UNI-HEALTH v2.0{Version}) and the panel's own display is put back afterwards.",
			own[0], own[1], own[2], own[3]);
		return new CaptureWindow(view);
	}

	/// <summary>
	/// The focus ended: give the panel the viewer's own values back, unless the panel no longer
	/// shows what this focus left on it (a newer write stands). Safe with no borrow, with the panel
	/// gone, and after a stale borrow — the state is cleared either way.
	/// </summary>
	internal static void Restore()
	{
		var log = _log;
		_log = null;

		var view = WoundView.view;
		var own = State.End(view == null ? null : view.cInfo); // Unity object — ==
		if (own is null || view == null) // Unity object — ==
		{
			return;
		}

		Apply(view, own);
		log?.LogInformation(
			"[MedicalView] panel identity header returned to the viewer's own values ({Height}cm | {Age}y | #{Id} | UNI-HEALTH v2.0{Version}).",
			own[0], own[1], own[2], own[3]);
	}

	/// <summary>The native entry the game itself writes the header with — the one write path.</summary>
	private static void Apply(WoundView view, int[] values) =>
		view.SetCharDetails(values[0], values[1], values[2], values[3]);

	/// <summary>One native-read capture: puts the focus's own display back when the read returns.</summary>
	private sealed class CaptureWindow(WoundView view) : IDisposable
	{
		private readonly WoundView _view = view;

		public void Dispose()
		{
			if (_view == null) // Unity object — ==
			{
				return;
			}

			var write = State.Reassert();
			if (write.Kind == WriteKind.Show)
			{
				Apply(_view, write.Values);
				if (State.LabelsBlank)
				{
					Blank(_view);
				}
			}
			else if (write.Kind == WriteKind.Blank)
			{
				Blank(_view);
			}
		}
	}

	/// <summary>
	/// The two labels only: <c>cInfo</c> (and the two statics <c>SetCharDetails</c> writes) keeps
	/// describing the viewer's own character, because an unknown viewed identity is not a claim
	/// about the viewer. If the identity arrives on a later snapshot, the next refresh writes it
	/// through the native method and the labels come back complete.
	/// </summary>
	private static void Blank(WoundView view)
	{
		if (view.bodyStatText != null) // Unity object — ==
		{
			view.bodyStatText.text = "";
		}

		if (view.versionText != null) // Unity object — ==
		{
			view.versionText.text = "";
		}
	}
}
