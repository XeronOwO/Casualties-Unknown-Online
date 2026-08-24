namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 42; // v42: Workout/exercise animation rides the 20 Hz player entity stream (EntityStateMsg.WorkoutType)

}
