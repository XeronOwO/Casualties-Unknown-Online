using System;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Guest-side enemy health reconciliation (PURE): the guest attacks a frozen
/// enemy copy locally (immediate feedback — the copy's health drops on the
/// spot), but the host's 20 Hz batch then overwrites that health with the
/// host's authoritative value, which does not yet include the in-flight attack
/// report. That overwrite reads as a "flash revert" (drop, jump back up, then
/// drop again once the host applies the report). This machine preserves the
/// local drop across the overwrites and clears it as the host's health catches
/// up: a host health drop is attributed to the pending local damage, and the
/// display is always host health minus whatever is still pending. No Unity
/// references; the Game Adapter calls it per frozen enemy.
/// </summary>
internal sealed class EnemyHealthReconcile
{
	private float _pendingDamage;
	private bool _hasBaseline;
	private float _lastHostHealth;

	/// <summary>Record one local attack's damage (not yet reflected in the host's stream).</summary>
	internal void RecordLocalDamage(float damage) => _pendingDamage += damage;

	/// <summary>
	/// Reconcile one host batch's authoritative health against the pending local
	/// damage, returning the health to display. The host's drop since the
	/// previous batch clears that much pending damage (the host applied it); the
	/// display is the host health minus whatever is still pending.
	/// </summary>
	internal float Reconcile(float hostHealth)
	{
		if (_hasBaseline)
		{
			var drop = _lastHostHealth - hostHealth;
			if (drop > 0f)
			{
				_pendingDamage = Math.Max(0f, _pendingDamage - drop);
			}
		}

		_lastHostHealth = hostHealth;
		_hasBaseline = true;
		return hostHealth - _pendingDamage;
	}
}
