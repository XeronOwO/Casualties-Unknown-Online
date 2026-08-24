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
	internal const byte None = 0;
	internal const byte Pushups = 1;
	internal const byte Squats = 2;
	internal const byte Plank = 3;

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
