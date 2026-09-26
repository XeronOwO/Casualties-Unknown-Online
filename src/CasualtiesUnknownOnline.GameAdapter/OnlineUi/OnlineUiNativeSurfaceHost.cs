using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The uGUI host the Online UI's native surfaces live on (ticket "Online UI art and controls", S1): a
/// canvas of CUO's own parented under the game's main canvas, into which the game's own control
/// prefabs are instantiated. Parenting under the game's canvas is what makes a CUO surface read as the
/// game — the game's canvas scale (<c>PlayerCamera.uiScale</c> IS that canvas's local scale) and its
/// sorting come with the parent instead of being guessed.
///
/// <para>
/// Two hard rules from the native side. First, the hierarchy is created INACTIVE and stays that way:
/// the probe only reads serialized component state, so nothing may render, take input or join the
/// game's EventSystem — and a prefab instantiated under an inactive parent keeps its own
/// Awake/OnEnable dormant. Second, the settings menu prefab (<c>Special/SettingsMenu</c>) is NEVER
/// instantiated: its <c>Start</c> calls <c>SelectTab(Video)</c>, which spawns the whole settings
/// screen and takes the static <c>SettingsMenu.instance</c> that the game's own <c>OpenMenu</c>
/// returns early on (<c>SettingsMenu.cs</c>), so a probe copy would build a screen nobody asked for
/// AND block the player's own settings menu until it was destroyed; its <c>Close</c> and
/// <c>ResetToDefault</c> are what write the settings file. Only the row prefabs are safe to spawn,
/// and the game itself adds their listeners after creating them.
/// </para>
/// </summary>
internal sealed class OnlineUiNativeSurfaceHost : IDisposable
{
	/// <summary>The name CUO's canvas carries in the scene, so a reading can be tied to an object.</summary>
	internal const string RootName = "CUO Online UI Native Host";

	/// <summary>The game's own settings-row prefabs the host instantiates. Both are rows of the game's
	/// settings screen: the dropdown (a <c>TMP_Dropdown</c> row) and the keybind input (a button row).</summary>
	internal static readonly string[] RowPrefabPaths = ["Special/GameSettingDropdown", "Special/GameSettingInput"];

	/// <summary>Where CUO's canvas sorts: above the game's own UI, so an Online UI surface is on top of it.</summary>
	internal const int SortingOrder = 30_000;

	private readonly List<GameObject> _rows = [];
	private GameObject? _root;

	private OnlineUiNativeSurfaceHost(GameObject root)
	{
		_root = root;
	}

	/// <summary>The settings rows this host instantiated, in <see cref="RowPrefabPaths"/> order. A prefab
	/// the game moved is simply absent — the reading reports the missing part instead of failing.</summary>
	internal IReadOnlyList<GameObject> Rows => _rows;

	/// <summary>The scene path of CUO's canvas.</summary>
	internal string RootPath => _root == null ? string.Empty : PathOf(_root.transform);

	/// <summary>The subtree the chrome sweep must skip: CUO's own surfaces are not the game's chrome.</summary>
	internal Transform? RootTransform => _root == null ? null : _root.transform;

	/// <summary>
	/// Builds the host under the game's main canvas, or returns null when the game has no canvas yet —
	/// the normal state during startup, not a failure.
	/// </summary>
	internal static OnlineUiNativeSurfaceHost? TryCreate()
	{
		var parent = FindMainCanvasTransform();
		if (parent == null)
		{
			return null;
		}

		var root = new GameObject(RootName);
		// Inactive from the object's FIRST statement: nothing in this hierarchy — CUO's own canvas
		// components included — may run Awake/OnEnable, take input or render, and the game's own rows
		// are instantiated further down for the same reason.
		root.SetActive(false);
		root.transform.SetParent(parent, worldPositionStays: false);
		var canvas = root.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = SortingOrder;

		var host = new OnlineUiNativeSurfaceHost(root);
		foreach (var path in RowPrefabPaths)
		{
			var prefab = Resources.Load<GameObject>(path);
			if (prefab == null)
			{
				continue;
			}

			var row = Object.Instantiate(prefab, root.transform);
			row.name = path.Substring(path.LastIndexOf('/') + 1);
			host._rows.Add(row);
		}

		return host;
	}

	/// <summary>
	/// The game's main canvas. The pre-run/menu canvas is tried first because it exists first — the
	/// player camera's canvas only appears once the game is actually being played, and a probe that
	/// waited for it would report nothing on a machine sitting in the menu.
	/// </summary>
	internal static Transform? FindMainCanvasTransform()
	{
		if (PreRunScript.instance != null && PreRunScript.instance.mainCanvas != null)
		{
			return PreRunScript.instance.mainCanvas.transform;
		}

		if (PlayerCamera.main != null && PlayerCamera.main.mainCanvas != null)
		{
			return PlayerCamera.main.mainCanvas.transform;
		}

		return null;
	}

	/// <summary>The scene path of a transform, for a reading that names where a style was found.</summary>
	internal static string PathOf(Transform transform)
	{
		var path = transform.name;
		for (var parent = transform.parent; parent != null; parent = parent.parent)
		{
			path = parent.name + "/" + path;
		}

		return path;
	}

	public void Dispose()
	{
		// Unity object — == (a scene unload may already have destroyed the canvas)
		if (_root != null)
		{
			Object.Destroy(_root);
		}

		_root = null;
		_rows.Clear();
	}
}
