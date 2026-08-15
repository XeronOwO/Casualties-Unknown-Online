namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 9; // v9: dedicated EnemyEffect events for ElderThornback/Xaloris/GrabberPlant proximity side effects + host save-merge of enemy terminal state — a v8 peer would leave the events unhandled and drop the terminal state, so mixed-version sessions are refused instead of silently degrading
}
