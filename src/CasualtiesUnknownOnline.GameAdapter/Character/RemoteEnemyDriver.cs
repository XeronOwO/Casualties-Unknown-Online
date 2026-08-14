using System.Reflection;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Log = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks an enemy as remote-managed: a frozen render copy on the guest. Its
/// AI/update scripts are skipped (the freeze patches check this marker, same
/// pattern as <see cref="RemoteBodyDriver"/> for the player clones) and its
/// position/rotation/health are driven from the host's snapshot each frame.
///
/// A frozen spider also owns its bite cooldown here: the game decrements
/// <c>SpiderHandler.biteCooldown</c> inside <c>SpiderHandler.Update</c>
/// (SpiderHandler.cs:39), which the freeze patch skips — left alone, the copy
/// bites ONCE and never again (the cooldown stays at <c>biteCoolToSet</c> = 5 s,
/// gate-checked in OnCollisionStay2D/OnCollisionEnter2D, SpiderHandler.cs:138
/// and :235). The driver replicates that one decrement every frame, so the
/// frozen copy keeps biting a touching guest on the game's own 5 s gate — the
/// bite still travels as the dedicated EnemyBite event (accept-first, no
/// validation). This is guest-only: only the guest freezes its copies, so the
/// host's spider keeps decrementing in its own Update (no double-decrement).
/// </summary>
internal sealed class RemoteEnemyDriver : MonoBehaviour
{
	/// <summary>The protected cooldown field the freeze patch stops decrementing —
	/// resolved once; name + <c>float</c> type guarded by GameFieldContractTests.</summary>
	private static readonly FieldInfo? BiteCooldownField =
		typeof(SpiderHandler).GetField("biteCooldown", BindingFlags.Instance | BindingFlags.NonPublic);

	private static Log? _log;
	private static bool _missingLogged;

	private SpiderHandler? _spider;

	/// <summary>Late-bound logger — the component is created by AddComponent (outside DI), so the adapter passes its own logger in once at startup.</summary>
	internal static void BindLog(Log log) => _log = log;

	private void Awake()
	{
		_spider = GetComponentInChildren<SpiderHandler>();
		if (_spider != null && BiteCooldownField == null && !_missingLogged) // Unity object — ==
		{
			_missingLogged = true;
			_log?.LogError("[Enemy] SpiderHandler.biteCooldown field not found — the guest bite cooldown cannot tick (each copy bites once).");
		}
	}

	private void Update()
	{
		if (_spider == null || BiteCooldownField == null) // Unity object — ==
		{
			return;
		}

		// Replicate the decrement the freeze patch skips (SpiderHandler.cs:39).
		var cooldown = (float)BiteCooldownField.GetValue(_spider);
		if (cooldown > 0f)
		{
			BiteCooldownField.SetValue(_spider, Mathf.Max(0f, cooldown - Time.deltaTime));
		}
	}
}
