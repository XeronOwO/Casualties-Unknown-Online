using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The console <c>/heal</c> cheat verb applied to THIS client's own body: the one place the full
/// reset's structural half lives — the field table (<see cref="HealResetState"/>) covers the
/// values, while the severed-limb restore, the hollow clear, the splint/tourniquet and medication
/// component removals, and the refresh of the two body fields Mapster cannot see are this layer's.
///
/// <para>
/// The target's own client is the only place the body exists (the judgment-ownership rule), so the
/// host instructs and this class applies; the resulting character snapshot is re-reported straight
/// away, which is what carries the reset to the host and every peer without a second wire shape.
/// </para>
///
/// <para>
/// Three ordering decisions are load-bearing. The components and the hollow overlay go through
/// <c>DestroyImmediate</c> (see <see cref="HollowClear.ApplyImmediate"/>) because the re-report is
/// captured in this same frame. The native <c>MendBone</c>/<c>UnDislocate</c> and the limb restore
/// run BEFORE the state is applied, so the visuals and the rest of the body agree with the flags
/// the message carries. And the whole application runs under the remote-apply scope the
/// host-authoritative heal result already uses: it is a host-ordered local mutation, and the scope
/// keeps the item/sound hooks quiet so the explicit re-report below is the only report.
/// </para>
/// </summary>
internal sealed class LocalBodyReset(CharacterDataSync characterDataSync, ILogger<LocalBodyReset> log) : ILocalBodyReset
{
	private readonly CharacterDataSync _characterDataSync = characterDataSync;

	/// <inheritdoc />
	public LocalBodyResetOutcome Reset()
	{
		var camera = PlayerCamera.main;
		var body = camera == null ? null : camera.body; // Unity objects — ==
		if (body == null) // Unity object — ==
		{
			log.LogWarning("[HealReset] no local body to reset: the world is still loading, or this client is not in one.");
			return LocalBodyResetOutcome.Refused("this client has no local body (not in a world, or still loading it).");
		}

		var wasDead = !body.alive;
		if (_characterDataSync.RestorePending)
		{
			// A restore still waiting (or half-way through its two-frame wipe) owns this body: the
			// reset would be written here and then overwritten by the restore's snapshot, and the
			// explicit re-report below is suppressed while a restore is pending. Refusing by name is
			// the honest answer; "try again once the restore has landed" is what the player can do.
			log.LogInformation("[HealReset] refused: a character restore is still pending on this body; a reset now would be overwritten by it.");
			return LocalBodyResetOutcome.Refused("this client is still applying its character restore, so a reset now would be overwritten; try again once the restore has landed.");
		}

		var restoredLimbs = LimbRestore.CountMissing(body);
		var health = _characterDataSync.CaptureHealth(body);
		var limbs = _characterDataSync.CaptureLimbs(body);
		HealResetState.ApplyTo(health, limbs);

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hollowCleared = HollowClear.ApplyImmediate(body);
			RemoveComponent<Painkillers>(body);
			RemoveComponent<SleepingPills>(body);
			RemoveComponent<Antidepressants>(body);

			// The native clear calls run before the state is written so their own side effects
			// (the broken-bone sprite going away, the happiness they add) are overwritten by the
			// heal's values rather than left standing on top of them.
			foreach (var limb in body.limbs)
			{
				if (limb == null) // Unity object — ==
				{
					continue;
				}

				if (limb.broken)
				{
					limb.MendBone();
				}

				if (limb.dislocated)
				{
					limb.UnDislocate();
				}
			}

			// Before the mapping: LimbRestore reads the real dismembered flags, which the applied
			// message would clear first (and then have nothing to reactivate).
			LimbRestore.Apply(body);
			_characterDataSync.ApplyHealState(body, health, limbs);

			// Not on the wire, so the table's values are written here: the two additive happiness
			// terms the destroyed components fed, and KrokMP's control-reversal clear.
			body.opiateHappiness = HealResetState.OpiateHappiness;
			body.antidepressantHappiness = HealResetState.AntidepressantHappiness;
			body.reversedControls = HealResetState.ReversedControls;

			// The game's own timed-operation pump is part of "reset": a queued operation from
			// before the heal must not fire onto the healthy body a second later.
			CoUtils.instance.CancelAll();

			_characterDataSync.ReportInventoryChanged(body);
			log.LogInformation(
				"[HealReset] local body reset: wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}.",
				wasDead, restoredLimbs, hollowCleared);
			return LocalBodyResetOutcome.AppliedNow(wasDead, restoredLimbs, hollowCleared);
		}
	}

	/// <summary>
	/// Destroy one medication component now, not at the end of the frame: the re-report below runs
	/// in this frame and would otherwise capture the component (with its pre-reset amount) and send
	/// it back out.
	/// </summary>
	private void RemoveComponent<T>(Body body)
		where T : Component
	{
		var component = body.GetComponent<T>();
		if (component != null) // Unity object — ==
		{
			Object.DestroyImmediate(component);
		}
	}
}
