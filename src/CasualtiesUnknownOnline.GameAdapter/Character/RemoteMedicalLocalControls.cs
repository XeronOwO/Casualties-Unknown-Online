using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Hides the local-only action surfaces while a remote medical focus is open
/// (user ruling 2026-09-21): the nap control (<c>napbutton</c> with its
/// <c>sleepImage</c> state icon), the workout list (<c>workoutList</c>) and the
/// HUD main/off-hand switch control (<c>handSwapImage</c>). Every one of them is
/// an action the VIEWER performs on their own body, so a read-only view of
/// somebody else loses nothing: the sleep-quality readout rides the nap control
/// and is not even synced (<c>curSleep</c>/<c>canTakeNap</c> never reach a remote
/// display body), and the armor/health view toggle
/// (<c>WoundView.modeToggleImage</c>) is deliberately NOT hidden because it reads
/// the displayed body.
/// </summary>
/// <remarks>
/// The hide is re-asserted on every frame the focus is open — the native
/// <c>WoundView</c> keeps writing <c>napbutton.interactable</c>, and a prefab
/// animation can re-activate a surface — and every surface is restored to the
/// visibility it had when the focus opened, so the local panel is untouched
/// afterwards. The close half has one entry point,
/// <see cref="RemoteMedicalView.Close"/>.
/// </remarks>
internal static class RemoteMedicalLocalControls
{
	/// <summary>The native surfaces this class hides: <c>WoundView.napbutton</c>,
	/// <c>WoundView.sleepImage</c>, <c>WoundView.workoutList</c> and
	/// <c>PlayerCamera.handSwapImage</c>. A contract test pins every name against
	/// the game assembly.</summary>
	internal static readonly string[] NativeSurfaceFields =
	[
		"napbutton",
		"sleepImage",
		"workoutList",
		"handSwapImage",
	];

	private static readonly SurfaceMemory NapButton = new();
	private static readonly SurfaceMemory SleepImage = new();
	private static readonly SurfaceMemory WorkoutList = new();
	private static readonly SurfaceMemory HandSwapImage = new();

	/// <summary>Hide every local-only surface. Idempotent: call it once per frame
	/// while <see cref="RemoteMedicalView.IsOpen"/>.</summary>
	internal static void Hide(WoundView view)
	{
		HideObject(view.napbutton != null ? view.napbutton.gameObject : null, NapButton); // Unity object — ==
		HideObject(view.sleepImage != null ? view.sleepImage.gameObject : null, SleepImage); // Unity object — ==
		HideObject(view.workoutList, WorkoutList); // Unity object — ==

		var camera = PlayerCamera.main;
		if (camera != null && camera.handSwapImage != null) // Unity object — ==
		{
			// Only the swap control's own graphic goes dark: an object that also
			// carries the hand slots keeps them.
			camera.handSwapImage.enabled = HandSwapImage.Hide(camera.handSwapImage.enabled);
		}
	}

	/// <summary>Restore each surface to the visibility it had when the focus
	/// opened. Safe when nothing was hidden and when the objects are gone (scene
	/// reload): every memory is consumed exactly once whether or not its object
	/// still exists — a surface that is gone has nothing to write, and a memory
	/// kept past the focus would be written onto a later session's surface.</summary>
	internal static void Restore()
	{
		var view = WoundView.view;
		var napButton = view != null && view.napbutton != null ? view.napbutton.gameObject : null; // Unity object — ==
		var sleepImage = view != null && view.sleepImage != null ? view.sleepImage.gameObject : null; // Unity object — ==
		var workoutList = view != null ? view.workoutList : null; // Unity object — ==

		RestoreObject(napButton, NapButton);
		RestoreObject(sleepImage, SleepImage);
		RestoreObject(workoutList, WorkoutList);

		var camera = PlayerCamera.main;
		var swapImage = camera != null ? camera.handSwapImage : null; // Unity object — ==
		if (HandSwapImage.Restore() is { } wasEnabled && swapImage != null) // Unity object — ==
		{
			swapImage.enabled = wasEnabled;
		}
	}

	private static void HideObject(GameObject? surface, SurfaceMemory memory)
	{
		// A surface that is not there cannot be remembered: leaving the memory
		// empty keeps a later Restore from writing a state it never saw.
		if (surface == null) // Unity object — ==
		{
			return;
		}

		surface.SetActive(memory.Hide(surface.activeSelf));
	}

	private static void RestoreObject(GameObject? surface, SurfaceMemory memory)
	{
		if (memory.Restore() is not { } wasActive)
		{
			return;
		}

		if (surface != null) // Unity object — ==
		{
			surface.SetActive(wasActive);
		}
	}

	/// <summary>
	/// Pure memory for ONE native surface: the visibility it had when the focus
	/// opened. The first <see cref="Hide"/> call remembers the original value on
	/// the hide-pass value argument, every later call only re-asserts the hidden
	/// state, and <see cref="Restore"/> gives the original back exactly once.
	/// Unity-free, so the rule is unit-testable.
	/// </summary>
	internal sealed class SurfaceMemory
	{
		private bool? _remembered;

		/// <summary>The visibility to write for one hide pass. The argument is read on
		/// the FIRST pass only: it becomes the remembered original, and every later
		/// pass keeps that original.</summary>
		internal bool Hide(bool currentlyVisible)
		{
			_remembered ??= currentlyVisible;
			return false;
		}

		/// <summary>The visibility to write back when the focus ends, or null when
		/// nothing was hidden. Clears the memory either way.</summary>
		internal bool? Restore()
		{
			var remembered = _remembered;
			_remembered = null;
			return remembered;
		}
	}
}
