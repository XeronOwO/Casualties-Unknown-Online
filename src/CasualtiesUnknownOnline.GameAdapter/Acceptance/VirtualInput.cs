using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The acceptance channel's VIRTUAL POINTER: a Harmony postfix on <c>UnityEngine.Input.mousePosition</c> that answers a staged position while a batch has
/// one, and the real pointer otherwise. It exists because the alternative — OS-level input — costs the user a permission every batch, and because a row
/// that must see the pointer in a place the batch cannot reach with a real mouse (over a UI element, across a window, on several clients at once) has no
/// other instrument.
///
/// <para>
/// WHY THE POINTER AND NOT THE BUTTONS: this build's <c>UnityEngine.InputLegacyModule</c> declares <c>mousePosition</c> as a MANAGED getter
/// (<c>Input.get_mousePosition_Injected(out Vector3)</c>), so it has an IL body and Harmony can patch it, while <c>GetMouseButton</c>,
/// <c>GetMouseButtonDown</c> and <c>GetMouseButtonUp</c> are <c>extern</c> + <c>[MethodImpl(InternalCall)]</c> — no body, nothing to detour. Buttons
/// therefore need the game's own managed call sites patched one by one, which is a separate work item; the pointer is the lever available today.
/// </para>
///
/// <para>
/// OFF BY DEFAULT AND NEVER A PRODUCT FEATURE: nothing calls <see cref="Install"/> except the acceptance driver's recipe, <see cref="Uninstall"/> puts
/// the real pointer back, and the postfix answers the real value whenever no batch staged one. A player's session never reaches this type.
/// </para>
/// </summary>
internal static class VirtualInput
{
	private const string HarmonyId = "CasualtiesUnknownOnline.GameAdapter.VirtualInput";

	private static Harmony? _harmony;
	private static Vector3? _pointer;

	/// <summary>True while a staged pointer is in force.</summary>
	internal static bool Enabled => _pointer.HasValue;

	/// <summary>True once the patch is installed.</summary>
	internal static bool Installed => _harmony != null;

	/// <summary>
	/// Installs the pointer postfix. Answers why not when it cannot: a missing getter (another Unity version) or a body Harmony refuses are the two
	/// shapes worth naming, because they decide whether the channel works at all.
	/// </summary>
	internal static string Install(out bool installed)
	{
		installed = false;
		if (_harmony != null)
		{
			installed = true;
			return "already installed";
		}

		var getter = AccessTools.PropertyGetter(typeof(Input), nameof(Input.mousePosition));
		if (getter == null)
		{
			return "no getter: UnityEngine.Input.mousePosition is not a property on this build";
		}

		if (getter.GetMethodBody() == null)
		{
			return "no body: UnityEngine.Input.mousePosition has no IL on this build";
		}

		try
		{
			_harmony = new Harmony(HarmonyId);
			_harmony.Patch(getter, postfix: new HarmonyMethod(typeof(VirtualInput), nameof(Postfix)));
			installed = true;
			return "patched " + getter.DeclaringType!.FullName + "." + getter.Name;
		}
		catch (Exception exception)
		{
			_harmony = null;
			return "patch failed: " + exception.GetType().Name + ": " + exception.Message;
		}
	}

	/// <summary>Removes the patch and the staged pointer in one step; the real pointer answers again immediately.</summary>
	internal static void Uninstall()
	{
		_pointer = null;
		if (_harmony == null)
		{
			return;
		}

		_harmony.UnpatchSelf();
		_harmony = null;
	}

	/// <summary>Stages the pointer at a screen position (the game's own coordinates: origin bottom-left).</summary>
	internal static void MoveTo(float x, float y) => _pointer = new Vector3(x, y, 0f);

	/// <summary>Drops the staged pointer, so the real one answers without uninstalling the patch.</summary>
	internal static void Clear() => _pointer = null;

	/// <summary>The staged pointer, when a batch has one.</summary>
	internal static bool TryGetStaged(out Vector3 position)
	{
		if (_pointer is { } staged)
		{
			position = staged;
			return true;
		}

		position = default;
		return false;
	}

	/// <summary>The postfix Harmony calls: replaces the getter's answer with the staged one while a batch has staged one.</summary>
	private static void Postfix(ref Vector3 __result)
	{
		if (_pointer is { } staged)
		{
			__result = staged;
		}
	}
}
