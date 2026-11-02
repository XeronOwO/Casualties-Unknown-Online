using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The drag gesture's own key read, replaced at its call site.
///
/// <para>
/// Why it is a file of its own: <see cref="VirtualInput"/>'s replacement reaches the call sites INSIDE
/// <c>PlayerCamera.HandleInput</c> (and the minigame's hand read), and the drag is not one of them —
/// <c>HandleInput</c> calls <c>HandleDragging(uiCasts, ref usableObject)</c> as a separate method, and THAT
/// method is where the game asks for the binding:
/// <c>Input.GetKeyDown(KeyBinds.GetBind("iteminteract"))</c> to start and <c>Input.GetKeyUp(...)</c> to release
/// (<c>PlayerCamera.cs</c>, <c>HandleDragging</c>). A staged press therefore starts no drag however the pointer
/// is placed, which is what made two rows unreadable (measured 2026-11-02: with <c>dragItem</c> armed and the
/// pointer over a wound-view limb image, the release reached nothing and no wound item was applied).
/// </para>
///
/// <para>
/// It is the same shape as <see cref="KeybindCapturePatch"/>: a narrow method that patches ONE method and
/// reports what it did, so <see cref="VirtualInput"/>'s install report names every patched site in one place.
/// </para>
/// </summary>
internal static class DragInputPatch
{
	/// <summary>
	/// Patches <c>PlayerCamera.HandleDragging</c> with the staged-input replacement and returns the line the
	/// install report carries. Never throws: a failure is a sentence in that report, because a batch must be able
	/// to see WHY a drag row cannot be staged.
	/// </summary>
	internal static string Install(Harmony harmony)
	{
		var handleDragging = AccessTools.Method(typeof(PlayerCamera), "HandleDragging");
		if (handleDragging == null)
		{
			return "PlayerCamera.HandleDragging: no method on this build (" + DescribeDeclaredMethods() + ")";
		}

		try
		{
			VirtualInput.ResetReplacementCount();
			harmony.Patch(handleDragging, transpiler: new HarmonyMethod(typeof(VirtualInput), nameof(VirtualInput.TranspileHandleInput)));
			return "PlayerCamera.HandleDragging: patched, "
				+ VirtualInput.TakeReplacementCount().ToString(CultureInfo.InvariantCulture)
				+ " call(s) replaced";
		}
		catch (Exception exception)
		{
			return "PlayerCamera.HandleDragging: FAILED " + exception.GetType().Name + ": " + exception.Message;
		}
	}

	private static string DescribeDeclaredMethods()
	{
		var names = new List<string>();
		foreach (var method in typeof(PlayerCamera).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
		{
			if (method.Name.IndexOf("Drag", StringComparison.Ordinal) >= 0)
			{
				names.Add(method.Name);
			}
		}

		return names.Count == 0 ? "no *Drag* method either" : string.Join(", ", names);
	}
}
