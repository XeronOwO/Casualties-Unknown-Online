using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Tiny local-body-only marker carrying the last requested
/// <c>Body.DoWorkout</c> type. The game's <c>Body.exercising</c> flag exposes
/// whether a workout is running, but not WHICH clip was started; this tracker
/// keeps that piece of local presentation state with the body so
/// <c>RunCoordinator.PublishBodyState</c> can put it on the 20 Hz player
/// entity stream. It is never added to a render clone (only
/// <c>BodyWorkoutPatch</c> adds it to a local body).
/// </summary>
internal sealed class LocalWorkoutTracker : MonoBehaviour
{
	/// <summary>The last WorkoutType requested on this body (0 = none, 1 = pushups, 2 = squats, 3 = plank).</summary>
	public byte WorkoutType;
}
