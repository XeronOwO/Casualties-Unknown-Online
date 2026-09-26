using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The LIVE Online UI surface (ticket online-ui-art-and-controls-overhaul, S2a): CUO's own canvas
/// parented under the game's canvas, with the game's own controls on it — the counterpart of
/// <see cref="OnlineUiNativeSurfaceHost"/>, which is the read-only probe and stays inactive.
///
/// <para>
/// This one IS active: it renders, takes input and takes part in the game's EventSystem, because the
/// player clicks it. Parenting it under the game's own canvas is what makes it read as the game — the
/// game's UI scale IS that canvas's local scale, and its sorting comes with the parent (which is why the
/// sort order here is only a floor: high enough to sit above the game's own UI, inherited scale and all).
/// </para>
///
/// <para>
/// The surface is driven by frames: the Runtime pushes what to show, the surface puts it on the game's
/// controls and reports what the player did back. It owns the game objects and nothing else — no
/// judgement, no state that outlives a frame beyond the live widget values and the intent queue.
/// </para>
///
/// <para>
/// Everything it creates is disposable and dies with the adapter. If the canvas it was parented under is
/// destroyed or deactivated (a scene change, a run ending), the surface rebuilds itself under whichever
/// game canvas is live then; a frame that arrives while the game has no canvas at all is dropped, which
/// is the normal state for the first seconds of a launch.
/// </para>
/// </summary>
internal sealed class OnlineUiSurfaceHost : IDisposable
{
	/// <summary>The name CUO's canvas carries in the scene, so a log line can be tied to an object.</summary>
	internal const string RootName = "CUO Online UI Surface";

	/// <summary>Where CUO's canvas sorts: above the game's own UI.</summary>
	internal const int SortingOrder = 30_000;

	/// <summary>The game's own UI click, the sound its settings rows play on a press.</summary>
	internal const string ClickSound = "miniClick";

	private readonly ILogger _log;
	private readonly Queue<OnlineUiIntent> _intents = new();

	private GameObject? _root;
	private OnlineUiLauncherView? _launcher;
	private bool _reportedMissingCanvas;

	internal OnlineUiSurfaceHost(ILogger log)
	{
		_log = log;
	}

	/// <summary>Shows one frame. The frame is dropped when the game has no canvas to hang the surface on
	/// yet — the surface appears by itself on the first frame that has one.</summary>
	internal void Push(OnlineUiFrame frame)
	{
		EnsureSurface();
		if (_launcher is null)
		{
			return;
		}

		_launcher.Apply(frame.LauncherLabel, frame.LauncherAlpha);
		_launcher.PollHover(_intents);
	}

	/// <summary>Takes the oldest queued intent (a click, a hover flip), oldest first.</summary>
	internal bool TryDequeueIntent(out OnlineUiIntent intent)
	{
		if (_intents.Count == 0)
		{
			intent = default;
			return false;
		}

		intent = _intents.Dequeue();
		return true;
	}

	private void EnsureSurface()
	{
		// Unity object — == (a scene change destroys the canvas this was parented under, and takes this
		// with it; the same check catches a parent that merely went inactive, which would hide the
		// surface just as effectively).
		if (_root != null && _root.activeInHierarchy && _launcher != null)
		{
			return;
		}

		if (_root != null)
		{
			_log.LogInformation("Online UI surface: the canvas it was parented under is gone — rebuilding it under the live one.");
			DestroySurface();
		}

		var parent = FindCanvasTransform();
		if (parent == null)
		{
			if (!_reportedMissingCanvas)
			{
				_reportedMissingCanvas = true;
				_log.LogDebug("Online UI surface: the game has no active canvas yet — the launcher waits for one.");
			}

			return;
		}

		_reportedMissingCanvas = false;

		var root = new GameObject(RootName);
		root.transform.SetParent(parent, worldPositionStays: false);
		var canvas = root.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = SortingOrder;
		root.AddComponent<GraphicRaycaster>();
		EnsureEventSystem(root.transform);

		var launcher = OnlineUiLauncherView.TryCreate(root.transform, OnLauncherClicked);
		if (launcher is null)
		{
			_log.LogWarning("Online UI surface: the launcher could not be built (no RectTransform on the game's row prefab) — the Online UI is unreachable this session.");
			Object.Destroy(root);
			return;
		}

		_root = root;
		_launcher = launcher;
		if (!launcher.UsesGamePrefab)
		{
			_log.LogWarning(
				"Online UI surface: the game's own button prefab `{Prefab}` is missing — the launcher falls back to a plain button instead of reporting the game's style.",
				OnlineUiLauncherView.PrefabPath);
		}

		_log.LogInformation("Online UI surface created under {Parent}.", OnlineUiNativeSurfaceHost.PathOf(parent));
	}

	private void OnLauncherClicked()
	{
		// The fact is queued BEFORE the sound: a click must never be lost to a side effect.
		_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherToggled));
		// The game's own UI click, at the game's own settings-row volume and pitch. Sound.Play ignores a
		// clip the game does not have, so a renamed sound is silent rather than fatal.
		PlayerCamera.PlayUISound(ClickSound);
	}

	/// <summary>
	/// uGUI input needs an EventSystem. The game has one for its own UI, and CUO creates its own ONLY
	/// when the scene has none — the game's input stack is never duplicated or fought over, and CUO's copy
	/// is a child of CUO's surface, so it dies with it.
	/// </summary>
	private static void EnsureEventSystem(Transform parent)
	{
		if (EventSystem.current != null)
		{
			return;
		}

		var holder = new GameObject("CUO Online UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
		holder.transform.SetParent(parent, worldPositionStays: false);
	}

	/// <summary>
	/// The game's canvas to parent under: the player camera's while a run is loaded (its
	/// <c>uiScale</c> IS the scale CUO's surface should use), the pre-run/menu canvas before that — the
	/// same two canvases the probe reads, in the order a LIVE surface needs them.
	/// </summary>
	private static Transform? FindCanvasTransform()
	{
		if (PlayerCamera.main != null
			&& PlayerCamera.main.mainCanvas != null
			&& PlayerCamera.main.mainCanvas.gameObject.activeInHierarchy)
		{
			return PlayerCamera.main.mainCanvas.transform;
		}

		if (PreRunScript.instance != null
			&& PreRunScript.instance.mainCanvas != null
			&& PreRunScript.instance.mainCanvas.gameObject.activeInHierarchy)
		{
			return PreRunScript.instance.mainCanvas.transform;
		}

		return null;
	}

	public void Dispose()
	{
		DestroySurface();
		_intents.Clear();
	}

	private void DestroySurface()
	{
		// Unity object — == (Dispose may run after the scene unloaded the whole hierarchy)
		if (_root != null)
		{
			Object.Destroy(_root);
		}

		_root = null;
		_launcher = null;
	}
}
