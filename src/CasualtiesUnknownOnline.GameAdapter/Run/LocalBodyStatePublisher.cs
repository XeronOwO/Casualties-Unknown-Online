using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Publishes the LOCAL body's 20 Hz state: the position anchor the game's own ragdoll rules imply (torso for a collapsed body, the root for a carried rider), the
/// look target and its override, the pose flags (sitting, sleeping, climbing, working out, napping, wall slide, exact limb poses) and the vitals-model inputs the
/// host pass reads. Its own type because every rule in it is a reading rule that follows the GAME's body behaviour, never the run's phase machine.
/// </summary>
internal sealed class LocalBodyStatePublisher(
	IEntitySyncControl entities,
	ISessionControl session,
	IPlayerInteractionControl playerInteraction)
{
	internal void Publish(Body body)
	{
		// Ragdoll/dead/unconscious: track the upper-torso world position, not
		// the Body transform. The visible limbs are not reliably centered on the
		// Body transform; KrokMP's inspected sync packet uses limbs[1] as the
		// ragdoll position anchor. Keeping the stream anchor on the torso also
		// makes the remote clone root/UI follow the collapsed body.
		// A CARRIED rider is the deliberate exception: it is placed by the
		// shared ride-pose path at the carrier's back using its body-root
		// transform, not a ragdoll. Publishing the torso anchor would make the
		// rider's own/carrier-side views agree with each other while every
		// third-party clone is offset differently — the reported vertical
		// placement asymmetry. Carried bodies always publish the body root.
		var upperTorso = !body.standing
			&& !CarriedBodyPose.ShouldPublishBodyRoot(CarriedBodyDriver.IsCarrying(body))
			&& body.limbs.Length > 1
			? body.limbs[1]
			: null;
		var pos = !body.standing && upperTorso != null // Unity object — ==
			? upperTorso.transform.position
			: body.transform.position;
		var look = body.targetLookPos;
		var vel = body.rb.velocity;
		// LookTarget/CorpseScript drive Body.overrideLookPos/Time on the local
		// player (LookTarget.cs:12-13, CorpseScript.cs:87-88); carry the
		// override target and the scared-face timer with the 20 Hz state so a
		// remote clone visibly looks at the same enemy/corpse and shows the
		// same facial expression.
		var lookOverridePos = body.overrideLookTime > 0f
			? new NetVector2(body.overrideLookPos.x, body.overrideLookPos.y)
			: (NetVector2?)null;
		// Pose flags mirror the game's own pose rules:
		// - sitting: idle sit condition (Body.cs:3162), minus movingAllowed
		//   (private; sleeping is covered by the sleeping flag), and never while
		//   the local body is either half of a carry relation — the ride/carry
		//   presentation replaces the native sit state and must not leak onto
		//   the wire.
		// - sleeping: Body.cs:3961.
		// - climbing: currentClimbable (Body.cs:470).
		var isCarryParticipant = playerInteraction.TryGetCarrier(session.LocalSteamId, out _)
			|| playerInteraction.TryGetCarried(session.LocalSteamId, out _);
		var isCarried = CarriedBodyDriver.IsCarrying(body);
		var sitting = CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant,
			body.idleTime > 12f,
			body.exercising);
		// Workout/exercise: Body.DoWorkout is a coroutine that does not expose
		// the active workout type as a public field, so BodyWorkoutPatch stores
		// the requested WorkoutType on a tiny local-body tracker. The
		// Body.exercising flag is the authoritative "currently working out"
		// gate — a failed DoWorkout guard or a stopped coroutine never sends a
		// stale workout pose.
		var workoutType = body.exercising
			&& body.TryGetComponent<LocalWorkoutTracker>(out var workoutTracker)
				? workoutTracker.WorkoutType
				: (byte)0;
		// Nap variant: TakeANap selects the standard or sick/alt coroutine; the
		// tracker records the exact choice (0 = standard, 1 = alt). Forced sleep
		// (body.sleeping without a nap tracker) stays 0 = standard lay-down.
		var napVariant = body.sleeping
			&& body.TryGetComponent<LocalNapTracker>(out var napTracker)
				? napTracker.NapVariant
				: (byte)0;
		// Dog shake is a public continuous body field; the clone's HandleVisuals
		// reads it when BodyPatches runs the render-only path.
		var dogShakeIntensity = body.dogShakeIntensity;
		// Wall slide is continuous body state written by HandleGroundedState
		// from the local move input + side raycasts (Body.cs:2600-2601); the
		// render proxy needs the same flags to replay the Wall clip/particle.
		var slidingLeft = Traverse.Create(body).Field("slidingLeft").GetValue<bool>();
		var slidingRight = Traverse.Create(body).Field("slidingRight").GetValue<bool>();
		// Exact limb poses ride the same 20 Hz state stream while the body is
		// in a non-standing, non-sleeping lying pose (ragdoll/dead/unconscious).
		// The remote clone is frozen, so this is the only way its visible limbs
		// can match the owner's physics-driven pose. A carried rider is not a
		// ragdoll on the visual path: it must keep the carry mount/visual-standing
		// presentation so the carrier-side rider clone's limbs stay attached.
		var limbPoses = CarriedBodyPose.ShouldPublishExactLimbPoses(isCarried, body.alive, body.conscious)
			? LimbPoseCapture.Capture(body)
			: null;
		// The host clone's vitals-model inputs, read off the owner's own body (HostVitalsInputs owns
		// the walk threshold, the workout flag and the climb state; the receiver is the host pass).
		var vitals = HostVitalsInputs.FromBody(body);
		entities.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			lookOverridePos, body.overrideLookTime, body.eyeScareTime,
			body.eyePanicTime, body.eyeCloseTime,
			sitting, body.sleeping, body.currentClimbable != null, // Unity object — ==
			workoutType, napVariant, dogShakeIntensity,
			slidingLeft, slidingRight,
			limbPoses, vitals.InWater, vitals.Moving, vitals.Exercising);
	}
}
