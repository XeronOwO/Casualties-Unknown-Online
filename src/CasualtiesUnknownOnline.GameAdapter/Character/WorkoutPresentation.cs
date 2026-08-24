namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure mapping from the wire's workout type byte to the game's animator clip
/// names. The game's <c>Body.DoWorkout</c> (Body.cs:368-435) plays
/// <c>ExperimentPushups</c>/<c>ArmsPushups</c>,
/// <c>ExperimentSquats</c>/<c>ArmsSquats</c> or
/// <c>ExperimentPlank</c>/<c>ArmsPlank</c>; the render proxy replays the same
/// clips when it receives a workout fact. Keeping the mapping pure gives the
/// visual rule an L0 test face without touching Unity objects.
/// </summary>
internal static class WorkoutPresentation
{
	/// <summary>Wire value: not working out (kept separate from the game enum's zero-based Pushups).</summary>
	internal const byte None = 0;
	internal const byte Pushups = 1;
	internal const byte Squats = 2;
	internal const byte Plank = 3;

	/// <summary>
	/// Translates the game's zero-based <c>Body.WorkoutType</c> values
	/// (Pushups=0, Squats=1, Plank=2) into the wire codes (1/2/3) so wire 0
	/// can mean "not exercising" without colliding with Pushups.
	/// </summary>
	internal static byte FromGameValue(byte gameWorkoutType) => gameWorkoutType switch
	{
		0 => Pushups,
		1 => Squats,
		2 => Plank,
		_ => None,
	};

	internal static bool IsWorkout(byte workoutType) =>
		workoutType is Pushups or Squats or Plank;

	internal static string? BodyClip(byte workoutType) => workoutType switch
	{
		Pushups => "ExperimentPushups",
		Squats => "ExperimentSquats",
		Plank => "ExperimentPlank",
		_ => null,
	};

	internal static string? ArmsClip(byte workoutType) => workoutType switch
	{
		Pushups => "ArmsPushups",
		Squats => "ArmsSquats",
		Plank => "ArmsPlank",
		_ => null,
	};
}
