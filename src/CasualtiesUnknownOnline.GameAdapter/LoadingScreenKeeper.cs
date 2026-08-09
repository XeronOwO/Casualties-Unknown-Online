using System;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Keeps the generation loading screen visible while the start gate holds.
/// The game hides it at generation end (WorldGeneration.cs:3637) AFTER its
/// fade-out. A SetActive-based undo inside that call is rejected by Unity
/// ("GameObject is already being activated or deactivated"), so this keeper
/// runs in LateUpdate — the frame order (Update → coroutines → LateUpdate →
/// render) lets it re-show the screen in the SAME frame the game hid it,
/// before anything renders a black frame.
///
/// Lives on the world object (always alive), never on the loading screen
/// itself (which gets deactivated and would silence its own LateUpdate).
/// </summary>
internal sealed class LoadingScreenKeeper : MonoBehaviour
{
	/// <summary>While true, an inactive loading screen is re-shown in LateUpdate. Null = passive.</summary>
	public Func<bool>? ShouldKeep;

	/// <summary>The current loading screen to keep (read each frame — a scene switch replaces it).</summary>
	public Func<GameObject?>? Loading;

	/// <summary>One-shot log hook — the keeper's first re-show (observable in the peer logs).</summary>
	public Action? OnFirstKeep;

	private bool _kept;

	private void LateUpdate()
	{
		if (ShouldKeep is null)
		{
			return;
		}

		var loading = Loading?.Invoke();
		if (ShouldKeep())
		{
			if (loading == null || loading.activeSelf) // Unity object — ==
			{
				return;
			}

			loading.SetActive(true);
			if (!_kept)
			{
				_kept = true;
				OnFirstKeep?.Invoke();
			}
		}
		else if (_kept)
		{
			// Symmetric hand-back: the keeper pulled the loading screen up (the
			// game had already hidden it at generation end) and is the only
			// writer since — on release it must go back down, nobody else does.
			// (The gate path closes its own kept object in StartGateCoordinator;
			// the no-gate path — host starting with nobody to wait for, or the
			// session ending mid-world — has no other closer, this is it. A
			// scene switch's own loading is never touched: ShouldKeep stays
			// true across it, and the game hides its own loading at generation
			// end, so closing here only ever hands back what we pulled up.)
			_kept = false;
			if (loading != null && loading.activeSelf) // Unity object — ==
			{
				loading.SetActive(false);
			}
		}
	}
}
